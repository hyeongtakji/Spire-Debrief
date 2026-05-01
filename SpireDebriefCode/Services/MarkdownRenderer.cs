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
        AppendPathing(md, log.Pathing);
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

    private static void AppendPathChoices(StringBuilder md, PathingLog pathing)
    {
        md.AppendLine("### Path Choices");
        md.AppendLine();

        foreach (PathChoiceLog choice in pathing.Choices.OrderBy(choice => choice.Floor))
        {
            md.AppendLine($"#### Floor {choice.Floor}");
            AppendBullet(md, "From", choice.FromNodeId);
            if (choice.AvailableNodeIds.Count > 0)
            {
                md.AppendLine("- Available:");
                foreach (string nodeId in choice.AvailableNodeIds)
                    md.AppendLine($"  - {Escape(FormatNodeLabel(pathing, choice, nodeId))}");
            }
            AppendBullet(md, "Chosen", FormatChosenNode(choice));

            if (choice.OptionSummaries.Count > 0)
            {
                md.AppendLine("- Option summaries:");
                foreach (PathOptionSummary option in choice.OptionSummaries)
                    md.AppendLine($"  - {Escape(FormatOptionSummary(option))}");
            }

            if (choice.Ranks != null)
            {
                List<string> ranks = [];
                AddRank(ranks, "most_elites", choice.Ranks.ChosenRankByMostElites);
                AddRank(ranks, "fewest_elites", choice.Ranks.ChosenRankByFewestElites);
                AddRank(ranks, "most_rests", choice.Ranks.ChosenRankByMostRestSites);
                AddRank(ranks, "shortest_rest", choice.Ranks.ChosenRankByShortestRestDistance);
                AddRank(ranks, "path_count", choice.Ranks.ChosenRankByPathCount);
                if (ranks.Count > 0)
                    md.AppendLine($"- Chosen ranks: {string.Join(", ", ranks)}");
            }

            md.AppendLine();
        }
    }

    private static string FormatChosenNode(PathChoiceLog choice)
    {
        if (string.IsNullOrWhiteSpace(choice.ChosenNodeId))
            return string.Empty;
        return string.IsNullOrWhiteSpace(choice.ChosenNodeType)
            ? choice.ChosenNodeId
            : $"{choice.ChosenNodeId} {choice.ChosenNodeType}";
    }

    private static string FormatNodeLabel(PathingLog pathing, PathChoiceLog choice, string nodeId)
    {
        string? type = choice.OptionSummaries.FirstOrDefault(option => option.NodeId.Equals(nodeId, StringComparison.Ordinal))?.NodeType
            ?? pathing.Acts.SelectMany(act => act.Nodes).FirstOrDefault(node => node.Id.Equals(nodeId, StringComparison.Ordinal))?.MapPointType;
        return string.IsNullOrWhiteSpace(type) ? nodeId : $"{nodeId} {type}";
    }

    private static string FormatOptionSummary(PathOptionSummary option)
    {
        string node = string.IsNullOrWhiteSpace(option.NodeType)
            ? option.NodeId
            : $"{option.NodeId} {option.NodeType}";
        return $"{node}: paths_to_boss={option.ReachablePathCount}, " +
            $"elites=min{option.MinElitesReachable}/max{option.MaxElitesReachable}, " +
            $"monsters=min{option.MinMonstersReachable}/max{option.MaxMonstersReachable}, " +
            $"rests=min{option.MinRestSitesReachable}/max{option.MaxRestSitesReachable}, " +
            $"shops=min{option.MinShopsReachable}/max{option.MaxShopsReachable}, " +
            $"treasures=min{option.MinTreasuresReachable}/max{option.MaxTreasuresReachable}, " +
            $"unknowns=min{option.MinUnknownsReachable}/max{option.MaxUnknownsReachable}, " +
            $"events=min{option.MinEventsReachable}/max{option.MaxEventsReachable}, " +
            $"elite_forced={option.EliteForced.ToString().ToLowerInvariant()}, " +
            $"rest_reachable={option.RestSiteReachable.ToString().ToLowerInvariant()}, " +
            $"nearest_rest={FormatNullable(option.NearestRestDistance)}, " +
            $"nearest_shop={FormatNullable(option.NearestShopDistance)}, " +
            $"nearest_elite={FormatNullable(option.NearestEliteDistance)}, " +
            $"flexibility={option.PathFlexibilityScore}";
    }

    private static void AddRank(List<string> ranks, string label, int? rank)
    {
        if (rank.HasValue)
            ranks.Add($"{label}=#{rank.Value}");
    }

    private static string FormatNullable(int? value) =>
        value?.ToString() ?? "null";

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

    private static void AppendPathing(StringBuilder md, PathingLog? pathing)
    {
        if (pathing == null)
            return;

        md.AppendLine("## Pathing Analysis Data");
        md.AppendLine();
        md.AppendLine($"Source: {Escape(pathing.Source)}");
        md.AppendLine();
        if (!string.IsNullOrWhiteSpace(pathing.Note))
        {
            md.AppendLine(Escape(pathing.Note));
            md.AppendLine();
        }
        if (AllCoordinatesMissing(pathing.ActualPath))
        {
            md.AppendLine("Coordinates: not captured for this export.");
            md.AppendLine();
        }

        md.AppendLine("### Actual Path");
        if (pathing.ActualPath.Count == 0)
        {
            md.AppendLine("- Not captured");
        }
        else
        {
            foreach (ActualPathStepLog step in pathing.ActualPath.OrderBy(step => step.Floor))
                md.AppendLine($"- {FormatActualPathStep(step)}");
        }
        md.AppendLine();

        if (pathing.Choices.Count > 0)
            AppendPathChoices(md, pathing);

        if (pathing.Source.Equals("live_telemetry", StringComparison.OrdinalIgnoreCase) ||
            pathing.Acts.Count > 0 ||
            pathing.Choices.Count > 0)
        {
            md.AppendLine("### Structured Pathing JSON");
            md.AppendLine();
            md.AppendLine("```json");
            md.AppendLine(PathingJsonSerializer.Serialize(pathing));
            md.AppendLine("```");
            md.AppendLine();
        }
    }

    private static string FormatActualPathStep(ActualPathStepLog step)
    {
        string roomType = FirstText(step.MapPointType, step.RoomType, "Unknown");
        if (!string.IsNullOrWhiteSpace(step.NodeId))
            return $"Floor {step.Floor}: chose {Escape(roomType)} at {Escape(step.NodeId)}";
        if (!string.IsNullOrWhiteSpace(step.Coordinate))
            return $"Floor {step.Floor}: chose {Escape(roomType)} at {Escape(step.Coordinate)}";
        return $"Floor {step.Floor}: {Escape(roomType)}";
    }

    private static bool AllCoordinatesMissing(IReadOnlyList<ActualPathStepLog> steps) =>
        steps.Count > 0 &&
        steps.All(step => string.IsNullOrWhiteSpace(step.NodeId) &&
            string.IsNullOrWhiteSpace(step.Coordinate));

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

    private static string FirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
