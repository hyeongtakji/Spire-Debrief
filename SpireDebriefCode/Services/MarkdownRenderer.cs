using System.Text;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class MarkdownRenderer
{
    private const string ReviewPromptResourceName = "SpireDebrief.ReviewPrompt.md";
    private static readonly string ReviewPrompt = LoadReviewPrompt();

    public static string Render(RunDebriefLog log)
    {
        StringBuilder md = new();
        md.AppendLine("# Spire Debrief");
        md.AppendLine();
        AppendRunSummary(md, log.Metadata);
        AppendFinalState(md, log.FinalState);
        AppendItems(md, "Final Deck", log.FinalState.Deck);
        AppendItems(md, "Relics", log.FinalState.Relics);
        AppendRunLog(md, log.Floors);
        AppendSummaryCounts(md, log.Summary);
        AppendReviewPrompt(md);
        return md.ToString();
    }

    private static void AppendRunSummary(StringBuilder md, RunMetadata meta)
    {
        md.AppendLine("## Run Summary");
        AppendBullet(md, "Character", meta.Character);
        AppendBullet(md, "Ascension", meta.Ascension ?? meta.Difficulty);
        AppendBullet(md, "Seed", meta.Seed);
        AppendBullet(md, "Game Version", meta.GameVersion);
        AppendBullet(md, "Mod Version", meta.ModVersion);
        AppendBullet(md, "Date", meta.StartedAt);
        AppendBullet(md, "Result", meta.Result);
        AppendBullet(md, "Ended At", FormatEndedAt(meta));
        md.AppendLine();
    }

    private static void AppendFinalState(StringBuilder md, FinalRunState state)
    {
        md.AppendLine("## Final State");
        string? hp = state.CurrentHp.HasValue || state.MaxHp.HasValue ? $"{state.CurrentHp?.ToString() ?? "?"}/{state.MaxHp?.ToString() ?? "?"}" : null;
        AppendBullet(md, "HP", hp);
        AppendBullet(md, "Gold", state.Gold?.ToString());
        AppendBullet(md, "Potions", state.Potions.Count == 0 ? null : string.Join(", ", state.Potions.Select(p => p.DisplayName)));
        md.AppendLine();
    }

    private static void AppendItems(StringBuilder md, string title, IReadOnlyList<DebriefItem> items)
    {
        md.AppendLine($"## {title}");
        if (items.Count == 0)
        {
            md.AppendLine("- Not captured");
        }
        else
        {
            foreach (DebriefItem item in items)
                md.AppendLine($"- {Escape(item.DisplayName)}");
        }
        md.AppendLine();
    }

    private static void AppendRunLog(StringBuilder md, IReadOnlyList<FloorLog> floors)
    {
        md.AppendLine("## Run Log");
        md.AppendLine();

        foreach (FloorLog floor in floors.OrderBy(f => f.Floor))
        {
            md.AppendLine($"### Floor {floor.Floor} - {Escape(floor.RoomType)}");
            AppendBullet(md, "Encounter", floor.Encounter);
            AppendBullet(md, "HP", FormatHp(floor.CurrentHp, floor.MaxHp));
            AppendBullet(md, "Gold", floor.Gold?.ToString());
            AppendBullet(md, "Turns Taken", floor.TurnsTaken?.ToString());
            AppendBullet(md, "Damage Taken", floor.DamageTaken?.ToString());
            AppendBullet(md, "Pathing", floor.PathingChoice);

            foreach (CardRewardDecision reward in floor.CardRewards)
            {
                if (reward.Choices.Count > 0)
                {
                    md.AppendLine("- Card choices:");
                    foreach (DebriefItem choice in reward.Choices)
                        md.AppendLine($"  - {Escape(choice.DisplayName)}");
                }
                if (reward.Picked != null)
                    AppendBullet(md, "Picked", reward.Picked.DisplayName);
                if (reward.Skipped)
                    md.AppendLine("- Skipped card reward");
            }

            if (floor.RelicRewards.Count > 0)
                AppendInlineItems(md, "Relic rewards", floor.RelicRewards);
            if (floor.PotionRewards.Count > 0)
                AppendInlineItems(md, "Potion rewards", floor.PotionRewards);
            if (floor.CardsGained.Count > 0)
                AppendInlineItems(md, "Cards gained", floor.CardsGained);
            if (floor.CardsRemoved.Count > 0 && (floor.Shop?.Removed.Count ?? 0) != floor.CardsRemoved.Count)
                AppendInlineItems(md, "Cards removed", floor.CardsRemoved);
            if (floor.Event != null)
                AppendEvent(md, floor.Event);
            if (floor.Shop != null)
                AppendShop(md, floor.Shop);
            if (floor.RestSite != null)
                AppendRestSite(md, floor.RestSite);
            foreach (string note in floor.Notes)
                md.AppendLine($"- Note: {Escape(note)}");
            md.AppendLine();
        }
    }

    private static void AppendEvent(StringBuilder md, EventDecision evt)
    {
        AppendBullet(md, "Event", evt.Name);
        if (evt.Options.Count > 0)
        {
            md.AppendLine("- Options:");
            foreach (string option in evt.Options)
                md.AppendLine($"  - {Escape(option)}");
        }
        AppendBullet(md, "Chosen", evt.Chosen);
        AppendBullet(md, "Result", evt.Result);
    }

    private static void AppendShop(StringBuilder md, ShopDecision shop)
    {
        if (shop.Purchased.Count > 0)
            AppendInlineItems(md, "Purchased", shop.Purchased);
        if (shop.Removed.Count > 0)
            AppendInlineItems(md, "Removed", shop.Removed);
    }

    private static void AppendRestSite(StringBuilder md, RestSiteDecision rest)
    {
        AppendBullet(md, "Rest site action", rest.Action);
        if (rest.Target != null)
            AppendBullet(md, "Target", rest.Target.DisplayName);
    }

    private static void AppendSummaryCounts(StringBuilder md, SummaryCounts summary)
    {
        md.AppendLine("## Summary Counts");
        AppendBullet(md, "Cards picked", summary.CardsPicked.ToString());
        AppendBullet(md, "Card rewards skipped", summary.CardRewardsSkipped.ToString());
        AppendBullet(md, "Cards removed", summary.CardsRemoved.ToString());
        AppendBullet(md, "Cards upgraded", summary.CardsUpgraded.ToString());
        AppendBullet(md, "Relics acquired", summary.RelicsAcquired.ToString());
        AppendBullet(md, "Shops visited", summary.ShopsVisited.ToString());
        AppendBullet(md, "Elites fought", summary.ElitesFought.ToString());
        md.AppendLine();
    }

    private static void AppendReviewPrompt(StringBuilder md)
    {
        md.AppendLine("## Review Prompt");
        md.AppendLine(ReviewPrompt);
    }

    private static string LoadReviewPrompt()
    {
        using Stream? stream = typeof(MarkdownRenderer).Assembly.GetManifestResourceStream(ReviewPromptResourceName);
        if (stream == null)
            return "Review this Slay the Spire 2 run as a post-run coach.";

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd().TrimEnd();
    }

    private static void AppendBullet(StringBuilder md, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            md.AppendLine($"- {label}: {Escape(value)}");
    }

    private static void AppendInlineItems(StringBuilder md, string label, IReadOnlyList<DebriefItem> items)
    {
        md.AppendLine($"- {label}: {string.Join(", ", items.Select(i => Escape(i.DisplayName)))}");
    }

    private static string FormatEndedAt(RunMetadata meta)
    {
        string? location = meta.FinalFloor.HasValue ? $"Floor {meta.FinalFloor}" : null;
        if (!string.IsNullOrWhiteSpace(meta.FinalAct)) location = location == null ? meta.FinalAct : $"{meta.FinalAct}, {location}";
        if (!string.IsNullOrWhiteSpace(meta.FinalRoom)) location = location == null ? meta.FinalRoom : $"{location}, {meta.FinalRoom}";
        return location ?? meta.EndedAt ?? string.Empty;
    }

    private static string? FormatHp(int? currentHp, int? maxHp) =>
        currentHp.HasValue || maxHp.HasValue ? $"{currentHp?.ToString() ?? "?"}/{maxHp?.ToString() ?? "?"}" : null;

    private static string Escape(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
}
