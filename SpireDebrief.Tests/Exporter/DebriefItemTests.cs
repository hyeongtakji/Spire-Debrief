using SpireDebrief.SpireDebriefCode.Models;
using Xunit;

namespace SpireDebrief.Tests.Exporter;

public sealed class DebriefItemTests
{
    [Fact]
    public void DisplayName_keeps_plain_card_names_unchanged()
    {
        DebriefItem item = new()
        {
            Name = "Strike"
        };

        Assert.Equal("Strike", item.DisplayName);
    }

    [Fact]
    public void DisplayName_renders_upgraded_cards_with_plus()
    {
        DebriefItem item = new()
        {
            Name = "Defend",
            UpgradeCount = 1
        };

        Assert.Equal("Defend+", item.DisplayName);
    }

    [Fact]
    public void DisplayName_renders_card_instance_metadata_inline()
    {
        DebriefItem item = new()
        {
            Name = "Card",
            UpgradeCount = 1,
            InstanceMetadata =
            [
                new CardInstanceMetadata
                {
                    Name = "Stable"
                }
            ]
        };

        Assert.Equal("Card+ [Stable]", item.DisplayName);
    }
}
