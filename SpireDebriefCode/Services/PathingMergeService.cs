using SpireDebrief.SpireDebriefCode;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathingMergeService
{
    private static readonly TimeSpan CloseTimestampWindow = TimeSpan.FromHours(24);

    public static void TryMergeTelemetry(RunDebriefLog log)
    {
        try
        {
            List<PathingTelemetryRun> telemetryRuns = PathTelemetryStorage.LoadAll();
            PathingTelemetryRun? telemetry = FindBestMatch(log, telemetryRuns);
            if (telemetry == null)
            {
                MarkTelemetryMissing(log, telemetryRuns.Count);
                return;
            }

            log.Pathing = Merge(log.Pathing, telemetry);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Path telemetry merge skipped: {ex.Message}");
        }
    }

    private static PathingLog Merge(PathingLog? historyPathing, PathingTelemetryRun telemetry)
    {
        SortedDictionary<int, ActualPathStepLog> actualPath = [];
        if (historyPathing != null)
        {
            foreach (ActualPathStepLog step in historyPathing.ActualPath)
                actualPath[step.Floor] = step;
        }

        foreach (ActualPathStepLog telemetryStep in telemetry.ActualPath)
        {
            if (telemetryStep.Floor <= 0)
                continue;

            actualPath[telemetryStep.Floor] = telemetryStep;
        }

        return new PathingLog
        {
            SchemaVersion = 1,
            Source = "live_telemetry",
            Note = "This section contains extracted pathing data for LLM review. It is not a final strategic judgment.",
            ActualPath = actualPath.Values.ToList(),
            Acts = telemetry.Acts,
            Choices = telemetry.Choices
        };
    }

    private static PathingTelemetryRun? FindBestMatch(
        RunDebriefLog log,
        IReadOnlyList<PathingTelemetryRun> telemetryRuns)
    {
        if (telemetryRuns.Count == 0)
            return null;

        string? gameRunId = Normalize(log.Metadata.GameRunId);
        if (!string.IsNullOrWhiteSpace(gameRunId))
        {
            PathingTelemetryRun? exact = telemetryRuns.FirstOrDefault(telemetry =>
                string.Equals(Normalize(telemetry.GameRunId), gameRunId, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;
        }

        string? seed = Normalize(log.Metadata.Seed);
        string? character = Normalize(log.Metadata.Character);
        DateTimeOffset? startedAt = ParseTime(log.Metadata.StartedAt);

        return telemetryRuns
            .Select(telemetry => new
            {
                Telemetry = telemetry,
                Score = ScoreFallbackMatch(telemetry, seed, character, startedAt)
            })
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => ParseTime(item.Telemetry.UpdatedAt) ?? DateTimeOffset.MinValue)
            .Select(item => item.Telemetry)
            .FirstOrDefault();
    }

    private static int ScoreFallbackMatch(
        PathingTelemetryRun telemetry,
        string? seed,
        string? character,
        DateTimeOffset? startedAt)
    {
        int score = 0;
        string? telemetrySeed = Normalize(telemetry.Seed);
        string? telemetryCharacter = Normalize(telemetry.Character);

        if (!string.IsNullOrWhiteSpace(seed))
        {
            if (!seed.Equals(telemetrySeed, StringComparison.OrdinalIgnoreCase))
                return -1;
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(character) && !string.IsNullOrWhiteSpace(telemetryCharacter))
        {
            if (character.Equals(telemetryCharacter, StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
            }
            else if (string.IsNullOrWhiteSpace(seed))
            {
                return -1;
            }
        }

        if (startedAt.HasValue)
        {
            DateTimeOffset? telemetryStarted = ParseTime(telemetry.StartedAt);
            if (telemetryStarted.HasValue)
            {
                TimeSpan diff = (startedAt.Value - telemetryStarted.Value).Duration();
                if (diff > CloseTimestampWindow)
                    return -1;
                score += Math.Max(0, 24 - (int)diff.TotalHours);
            }
        }

        return score > 0 ? score : -1;
    }

    private static void MarkTelemetryMissing(RunDebriefLog log, int telemetryFileCount)
    {
        if (log.Pathing == null)
            return;

        string detail = telemetryFileCount == 0
            ? "No live telemetry files were found for this export."
            : "No matching live telemetry file was found for this export.";
        log.Pathing.Note = string.IsNullOrWhiteSpace(log.Pathing.Note)
            ? detail
            : $"{log.Pathing.Note} {detail}";
    }

    private static DateTimeOffset? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : null;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
