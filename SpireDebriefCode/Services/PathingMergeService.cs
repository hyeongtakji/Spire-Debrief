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
            List<PathingTelemetryRun> matches = FindMatches(log, telemetryRuns);
            if (matches.Count == 0)
            {
                MarkTelemetryMissing(log, telemetryRuns.Count);
                return;
            }

            log.Pathing = Merge(log.Pathing, matches);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Path telemetry merge skipped: {ex.Message}");
        }
    }

    private static PathingLog Merge(PathingLog? historyPathing, IReadOnlyList<PathingTelemetryRun> telemetryRuns)
    {
        SortedDictionary<int, ActualPathStepLog> actualPath = [];
        if (historyPathing != null)
        {
            foreach (ActualPathStepLog step in historyPathing.ActualPath)
                actualPath[step.Floor] = step;
        }

        SortedDictionary<int, ActPathGraphSnapshot> acts = [];
        SortedDictionary<int, PathChoiceLog> choices = [];
        foreach (PathingTelemetryRun telemetry in OrderTelemetry(telemetryRuns))
        {
            foreach (ActualPathStepLog telemetryStep in telemetry.ActualPath)
            {
                if (telemetryStep.Floor <= 0)
                    continue;

                actualPath[telemetryStep.Floor] = actualPath.TryGetValue(telemetryStep.Floor, out ActualPathStepLog? existingStep)
                    ? MergeActualPathStep(existingStep, telemetryStep)
                    : telemetryStep;
            }

            foreach (ActPathGraphSnapshot act in telemetry.Acts)
            {
                if (act.ActIndex <= 0)
                    continue;

                acts[act.ActIndex] = act;
            }

            foreach (PathChoiceLog choice in telemetry.Choices)
            {
                if (choice.Floor <= 0)
                    continue;

                choices[choice.Floor] = choice;
            }
        }

        return new PathingLog
        {
            SchemaVersion = 1,
            Source = "live_telemetry",
            Note = TelemetryNote(telemetryRuns.Count),
            ActualPath = actualPath.Values.ToList(),
            Acts = acts.Values.ToList(),
            Choices = choices.Values.ToList()
        };
    }

    private static ActualPathStepLog MergeActualPathStep(
        ActualPathStepLog existing,
        ActualPathStepLog incoming) =>
        new()
        {
            Floor = incoming.Floor > 0 ? incoming.Floor : existing.Floor,
            ActIndex = incoming.ActIndex ?? existing.ActIndex,
            NodeId = FirstPresent(incoming.NodeId, existing.NodeId),
            Row = incoming.Row ?? existing.Row,
            Column = incoming.Column ?? existing.Column,
            Coordinate = FirstPresent(incoming.Coordinate, existing.Coordinate),
            MapPointType = FirstPresent(incoming.MapPointType, existing.MapPointType),
            RoomType = PreferRoomType(incoming.RoomType, existing.RoomType),
            PreviousNodeId = FirstPresent(incoming.PreviousNodeId, existing.PreviousNodeId),
            PathingChoiceSummary = FirstPresent(incoming.PathingChoiceSummary, existing.PathingChoiceSummary)
        };

    private static List<PathingTelemetryRun> FindMatches(
        RunDebriefLog log,
        IReadOnlyList<PathingTelemetryRun> telemetryRuns)
    {
        if (telemetryRuns.Count == 0)
            return [];

        string? gameRunId = Normalize(log.Metadata.GameRunId);
        if (!string.IsNullOrWhiteSpace(gameRunId))
        {
            List<PathingTelemetryRun> exact = telemetryRuns
                .Where(telemetry => string.Equals(
                    Normalize(telemetry.GameRunId),
                    gameRunId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exact.Count > 0)
                return OrderTelemetry(exact).ToList();
        }

        string? seed = Normalize(log.Metadata.Seed);
        string? character = Normalize(log.Metadata.Character);
        DateTimeOffset? startedAt = ParseTime(log.Metadata.StartedAt);
        int? finalFloor = log.Metadata.FinalFloor ?? (log.Floors.Count > 0 ? log.Floors.Count : null);

        return telemetryRuns
            .Select(telemetry => new
            {
                Telemetry = telemetry,
                Score = ScoreFallbackMatch(telemetry, seed, character, startedAt, finalFloor)
            })
            .Where(item => item.Score >= 0)
            .OrderBy(item => FirstFloor(item.Telemetry) ?? int.MaxValue)
            .ThenBy(item => ParseTime(item.Telemetry.StartedAt) ?? DateTimeOffset.MinValue)
            .ThenBy(item => ParseTime(item.Telemetry.UpdatedAt) ?? DateTimeOffset.MinValue)
            .Select(item => item.Telemetry)
            .ToList();
    }

    private static int ScoreFallbackMatch(
        PathingTelemetryRun telemetry,
        string? seed,
        string? character,
        DateTimeOffset? startedAt,
        int? finalFloor)
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

        if (finalFloor.HasValue)
        {
            int? lastFloor = LastFloor(telemetry);
            if (lastFloor.HasValue && lastFloor.Value > finalFloor.Value)
                return -1;
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

    private static IEnumerable<PathingTelemetryRun> OrderTelemetry(IEnumerable<PathingTelemetryRun> telemetryRuns) =>
        telemetryRuns
            .OrderBy(telemetry => FirstFloor(telemetry) ?? int.MaxValue)
            .ThenBy(telemetry => ParseTime(telemetry.StartedAt) ?? DateTimeOffset.MinValue)
            .ThenBy(telemetry => ParseTime(telemetry.UpdatedAt) ?? DateTimeOffset.MinValue);

    private static int? FirstFloor(PathingTelemetryRun telemetry) =>
        telemetry.ActualPath
            .Where(step => step.Floor > 0)
            .Select(step => (int?)step.Floor)
            .Min();

    private static int? LastFloor(PathingTelemetryRun telemetry) =>
        telemetry.ActualPath
            .Where(step => step.Floor > 0)
            .Select(step => (int?)step.Floor)
            .Max();

    private static string TelemetryNote(int segmentCount)
    {
        string note = "This section contains extracted pathing data for LLM review. It is not a final strategic judgment.";
        return segmentCount > 1
            ? $"{note} Merged {segmentCount} live telemetry segments for this run."
            : note;
    }

    private static string? FirstPresent(string? first, string? fallback) =>
        string.IsNullOrWhiteSpace(first) ? fallback : first;

    private static string PreferRoomType(string incoming, string existing) =>
        string.IsNullOrWhiteSpace(incoming) || incoming.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? existing
            : incoming;

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
