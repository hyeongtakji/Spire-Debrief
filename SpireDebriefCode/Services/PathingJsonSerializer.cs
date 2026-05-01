using System.Text.Json;
using System.Text.Json.Serialization;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathingJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static string Serialize(PathingLog pathing) =>
        JsonSerializer.Serialize(pathing, Options);

    public static string SerializeCompact(PathingLog pathing) =>
        JsonSerializer.Serialize(ToCompactExport(pathing), Options);

    public static string Serialize(PathingTelemetryRun telemetry) =>
        JsonSerializer.Serialize(telemetry, Options);

    public static PathingTelemetryRun? DeserializeTelemetry(string json) =>
        JsonSerializer.Deserialize<PathingTelemetryRun>(json, Options);

    private static object ToCompactExport(PathingLog pathing)
    {
        List<PathChoiceLog> consistentChoices = pathing.Choices
            .Where(IsConsistentChoice)
            .OrderBy(choice => choice.Floor)
            .ToList();

        return new
        {
            pathing.SchemaVersion,
            pathing.Source,
            pathing.Note,
            ActualPath = pathing.ActualPath.OrderBy(step => step.Floor),
            Acts = pathing.Acts.OrderBy(act => act.ActIndex).Select(ToActSummary),
            DecisionChoices = consistentChoices
                .Where(choice => choice.AvailableNodeIds.Count > 1)
                .Select(ToCompactChoice),
            ForcedSteps = consistentChoices
                .Where(choice => choice.AvailableNodeIds.Count == 1)
                .Select(ToForcedStep)
        };
    }

    private static object ToActSummary(ActPathGraphSnapshot act)
    {
        HashSet<string> incoming = act.Edges
            .Where(edge => !string.IsNullOrWhiteSpace(edge.ToNodeId))
            .Select(edge => edge.ToNodeId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, int> roomTypeCounts = act.Nodes
            .GroupBy(node => string.IsNullOrWhiteSpace(node.MapPointType) ? "Unknown" : node.MapPointType, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new
        {
            act.ActIndex,
            act.CapturedAt,
            NodeCount = act.Nodes.Count,
            EdgeCount = act.Edges.Count,
            StartNodeIds = act.Nodes
                .Where(node => !incoming.Contains(node.Id))
                .Select(node => node.Id)
                .Order(StringComparer.Ordinal),
            BossNodeIds = act.Nodes
                .Where(node => node.MapPointType.Equals("Boss", StringComparison.OrdinalIgnoreCase))
                .Select(node => node.Id)
                .Order(StringComparer.Ordinal),
            RoomTypeCounts = roomTypeCounts,
            FullGraphIncluded = false,
            FullGraphLocation = "telemetry_json"
        };
    }

    private static object ToCompactChoice(PathChoiceLog choice) =>
        new
        {
            choice.Id,
            choice.Floor,
            choice.ActIndex,
            choice.CapturedAt,
            choice.FromNodeId,
            choice.AvailableNodeIds,
            choice.ChosenNodeId,
            choice.ChosenNodeType,
            choice.ResolvedRoomType,
            choice.PlayerStateBefore,
            choice.PlayerStateAfter,
            OptionSummaries = choice.OptionSummaries.Select(ToCompactOptionSummary),
            choice.Ranks
        };

    private static object ToForcedStep(PathChoiceLog choice) =>
        new
        {
            choice.Floor,
            choice.ActIndex,
            choice.FromNodeId,
            choice.ChosenNodeId,
            choice.ChosenNodeType,
            choice.ResolvedRoomType
        };

    private static object ToCompactOptionSummary(PathOptionSummary option) =>
        new
        {
            option.NodeId,
            option.NodeType,
            PathsToBoss = option.ReachablePathCount,
            Elites = new { Min = option.MinElitesReachable, Max = option.MaxElitesReachable },
            Rests = new { Min = option.MinRestSitesReachable, Max = option.MaxRestSitesReachable },
            Shops = new { Min = option.MinShopsReachable, Max = option.MaxShopsReachable },
            option.EliteForced,
            option.ImmediateRest,
            option.UnknownCombatPossible,
            option.UnknownCombatReason,
            option.ForcedFollowUp,
            option.NearestRestDistance,
            option.NearestShopDistance,
            option.NearestEliteDistance,
            option.RiskNote,
            Flexibility = option.PathFlexibilityScore
        };

    private static bool IsConsistentChoice(PathChoiceLog choice) =>
        !string.IsNullOrWhiteSpace(choice.ChosenNodeId) &&
        choice.AvailableNodeIds.Contains(choice.ChosenNodeId, StringComparer.Ordinal);
}
