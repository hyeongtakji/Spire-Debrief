using SpireDebrief.SpireDebriefCode;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class PathTelemetryStorage
{
    public static string Save(PathingTelemetryRun telemetry, string? existingPath)
    {
        DebriefStorage.EnsureDirectories();
        telemetry.UpdatedAt = DateTimeOffset.Now.ToString("O");
        string path = string.IsNullOrWhiteSpace(existingPath)
            ? Path.Combine(DebriefStorage.TelemetryDir, $"{FileStem(telemetry)}.json")
            : existingPath;

        try
        {
            File.WriteAllText(path, PathingJsonSerializer.Serialize(telemetry));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Path telemetry save failed: {ex.Message}");
        }

        return path;
    }

    public static List<PathingTelemetryRun> LoadAll()
    {
        List<PathingTelemetryRun> telemetryRuns = [];
        if (!Directory.Exists(DebriefStorage.TelemetryDir))
            return telemetryRuns;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(DebriefStorage.TelemetryDir, "pathing_*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Path telemetry directory read failed: {ex.Message}");
            return telemetryRuns;
        }

        foreach (string file in files)
        {
            try
            {
                PathingTelemetryRun? telemetry = PathingJsonSerializer.DeserializeTelemetry(File.ReadAllText(file));
                if (telemetry != null)
                    telemetryRuns.Add(telemetry);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"Skipping path telemetry file {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return telemetryRuns;
    }

    private static string FileStem(PathingTelemetryRun telemetry)
    {
        string id = telemetry.GameRunId ?? telemetry.Seed ?? telemetry.RunId;
        string timestamp = DateTimeOffset.TryParse(telemetry.StartedAt, out DateTimeOffset started)
            ? started.ToString("yyyyMMdd-HHmmss")
            : DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        return $"pathing_{Safe(id)}_{timestamp}";
    }

    private static string Safe(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch).ToArray());
        while (safe.Contains("--", StringComparison.Ordinal))
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        return safe.Trim('-').ToLowerInvariant();
    }
}
