using System.Text.Json;
using SpireDebrief.SpireDebriefCode.Models;
using SpireDebrief.SpireDebriefCode.Services;
using Xunit;

namespace SpireDebrief.Tests.Pathing;

public sealed class PathingJsonSerializerTests
{
    [Fact]
    public void SerializeCompact_includes_monster_ranges()
    {
        PathingLog pathing = new()
        {
            Source = "live_telemetry",
            Choices =
            [
                new PathChoiceLog
                {
                    Floor = 2,
                    ActIndex = 1,
                    AvailableNodeIds = ["A1:1,0", "A1:1,1"],
                    ChosenNodeId = "A1:1,0",
                    OptionSummaries =
                    [
                        new PathOptionSummary
                        {
                            NodeId = "A1:1,0",
                            MinMonstersReachable = 2,
                            MaxMonstersReachable = 5
                        }
                    ]
                }
            ]
        };

        string json = PathingJsonSerializer.SerializeCompact(pathing);
        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement monsters = document.RootElement
            .GetProperty("decision_choices")[0]
            .GetProperty("option_summaries")[0]
            .GetProperty("monsters");
        Assert.Equal(2, monsters.GetProperty("min").GetInt32());
        Assert.Equal(5, monsters.GetProperty("max").GetInt32());
    }
}
