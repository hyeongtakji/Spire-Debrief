using SpireDebrief.SpireDebriefCode.Models;
using SpireDebrief.SpireDebriefCode.Services;
using Xunit;

namespace SpireDebrief.Tests.Pathing;

public sealed class PathChoiceAnalyzerTests
{
    [Fact]
    public void AnalyzeOptions_records_forced_follow_up_for_linear_path()
    {
        ActPathGraphSnapshot graph = new()
        {
            Nodes =
            [
                Node("unknown_1", "Unknown"),
                Node("rest_2", "Rest"),
                Node("monster_3", "Monster"),
                Node("boss_4", "Boss")
            ],
            Edges =
            [
                Edge("unknown_1", "rest_2"),
                Edge("rest_2", "monster_3"),
                Edge("monster_3", "boss_4")
            ]
        };

        PathOptionSummary summary = Assert.Single(
            PathChoiceAnalyzer.AnalyzeOptions(graph, ["unknown_1"]));

        Assert.Equal(
            ["rest_2 Rest", "monster_3 Monster", "boss_4 Boss"],
            summary.ForcedFollowUp);
    }

    [Fact]
    public void ApplyRuntimeContext_marks_unknown_combat_impossible()
    {
        ActPathGraphSnapshot graph = new()
        {
            Nodes =
            [
                Node("unknown_1", "Unknown"),
                Node("rest_2", "Rest"),
                Node("boss_3", "Boss")
            ],
            Edges =
            [
                Edge("unknown_1", "rest_2"),
                Edge("rest_2", "boss_3")
            ]
        };
        List<PathOptionSummary> summaries =
            PathChoiceAnalyzer.AnalyzeOptions(graph, ["unknown_1"]);
        PlayerStateSnapshot playerState = new()
        {
            UnknownRoomOdds = new UnknownRoomOddsSnapshot
            {
                MonsterOdds = 0f
            }
        };

        PathChoiceAnalyzer.ApplyRuntimeContext(summaries, playerState);

        PathOptionSummary summary = Assert.Single(summaries);
        Assert.False(summary.UnknownCombatPossible);
        Assert.Contains("MonsterOdds=0", summary.UnknownCombatReason);
        Assert.Contains("normal combat was not possible", summary.RiskNote);
    }

    private static PathNodeLog Node(string id, string mapPointType)
    {
        return new PathNodeLog
        {
            Id = id,
            MapPointType = mapPointType
        };
    }

    private static PathEdgeLog Edge(string fromNodeId, string toNodeId)
    {
        return new PathEdgeLog
        {
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId
        };
    }
}
