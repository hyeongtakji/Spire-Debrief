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

    public static string Serialize(PathingTelemetryRun telemetry) =>
        JsonSerializer.Serialize(telemetry, Options);

    public static PathingTelemetryRun? DeserializeTelemetry(string json) =>
        JsonSerializer.Deserialize<PathingTelemetryRun>(json, Options);
}
