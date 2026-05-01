using SpireDebrief.SpireDebriefCode.Models;
using SpireDebrief.SpireDebriefCode.Services;
using Xunit;

namespace SpireDebrief.Tests.Exporter;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void Render_includes_final_deck_card_instance_metadata()
    {
        RunDebriefLog log = new()
        {
            FinalState = new FinalRunState
            {
                Deck =
                [
                    Card("Card", upgradeCount: 1, "Stable")
                ]
            }
        };

        string markdown = MarkdownRenderer.Render(log);

        Assert.Contains("## Final Deck", markdown);
        Assert.Contains("- Card+ [Stable]", markdown);
    }

    [Fact]
    public void Render_includes_card_instance_changes_section()
    {
        RunDebriefLog log = new()
        {
            Floors =
            [
                new FloorLog
                {
                    Floor = 6,
                    RoomType = "Event",
                    CardInstanceChanges =
                    [
                        new CardInstanceChangeLog
                        {
                            Floor = 6,
                            Description = "Card gained Stable.",
                            ChangeKind = "modifier"
                        }
                    ]
                }
            ]
        };

        string markdown = MarkdownRenderer.Render(log);

        Assert.Contains("## Card Instance Changes", markdown);
        Assert.Contains("- Floor 6: Card gained Stable.", markdown);
    }

    [Fact]
    public void Render_includes_floor_level_card_modifications()
    {
        RunDebriefLog log = new()
        {
            Floors =
            [
                new FloorLog
                {
                    Floor = 8,
                    RoomType = "Event",
                    CardInstanceChanges =
                    [
                        new CardInstanceChangeLog
                        {
                            Floor = 8,
                            Description = "Card gained Preserve.",
                            ChangeKind = "modifier"
                        }
                    ]
                }
            ]
        };

        string markdown = MarkdownRenderer.Render(log);

        Assert.Contains("### Floor 8 - Event", markdown);
        Assert.Contains("- Card modifications:", markdown);
        Assert.Contains("  - Card gained Preserve.", markdown);
    }

    [Fact]
    public void Render_includes_export_limitations_only_when_present()
    {
        string withoutLimitations = MarkdownRenderer.Render(new RunDebriefLog());
        RunDebriefLog withLimitations = new()
        {
            ExportLimitations =
            [
                "Turn-by-turn combat play order is not captured."
            ]
        };

        string withLimitationsMarkdown = MarkdownRenderer.Render(withLimitations);

        Assert.DoesNotContain("## Export Limitations", withoutLimitations);
        Assert.Contains("## Export Limitations", withLimitationsMarkdown);
        Assert.Contains(
            "- Turn-by-turn combat play order is not captured.",
            withLimitationsMarkdown);
    }

    private static DebriefItem Card(
        string name,
        int? upgradeCount = null,
        params string[] metadataNames)
    {
        return new DebriefItem
        {
            Name = name,
            UpgradeCount = upgradeCount,
            InstanceMetadata = metadataNames
                .Select(name => new CardInstanceMetadata { Name = name })
                .ToList()
        };
    }
}
