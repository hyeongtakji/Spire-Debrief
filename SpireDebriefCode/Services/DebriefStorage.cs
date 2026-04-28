using System.Reflection;
using System.Text.Json;
using Godot;
using SpireDebrief.SpireDebriefCode;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class DebriefStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string BaseDir => ResolveModDirectory();
    public static string RunsDir => Path.Combine(BaseDir, "runs");
    public static string ExportsDir => Path.Combine(BaseDir, "exports");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RunsDir);
        Directory.CreateDirectory(ExportsDir);
    }

    public static void SaveJson(RunDebriefLog log)
    {
        try
        {
            EnsureDirectories();
            string path = Path.Combine(RunsDir, $"{FileStem(log)}.json");
            DeleteStaleJsonFiles(log.RunId, path);
            File.WriteAllText(path, JsonSerializer.Serialize(log, JsonOptions));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Unable to save Spire Debrief JSON log: {ex.Message}");
        }
    }

    public static RunDebriefLog? LoadLatestJson()
    {
        try
        {
            return LoadFirstLog(_ => true);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Unable to load latest Spire Debrief JSON log: {ex.Message}");
            return null;
        }
    }

    public static RunDebriefLog? LoadLatestInProgressJson()
    {
        try
        {
            return LoadFirstLog(IsInProgress);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Unable to load in-progress Spire Debrief JSON log: {ex.Message}");
            return null;
        }
    }

    public static RunDebriefLog? LoadBestMatchingInProgressJson(object? runSource)
    {
        return LoadBestMatchingJson(runSource, requireInProgress: true);
    }

    public static RunDebriefLog? LoadBestMatchingJson(object? runHistorySource)
    {
        return LoadBestMatchingJson(runHistorySource, requireInProgress: false);
    }

    public static bool IsInProgress(RunDebriefLog log) =>
        log.Metadata.EndedAt == null && log.Metadata.Result == null;

    public static bool HasUsableIdentity(object? source)
    {
        RunIdentity identity = ExtractIdentity(source);
        return identity.GameRunId != null || identity.Seed != null;
    }

    public static bool MatchesIdentity(RunDebriefLog log, object? source)
    {
        RunIdentity sourceIdentity = ExtractIdentity(source);
        if (sourceIdentity.GameRunId == null && sourceIdentity.Seed == null)
            return false;

        return Matches(log, sourceIdentity);
    }

    private static RunDebriefLog? LoadBestMatchingJson(object? runHistorySource, bool requireInProgress)
    {
        RunIdentity identity = ExtractIdentity(runHistorySource);
        if (identity.GameRunId == null && identity.Seed == null)
            return null;

        try
        {
            EnsureDirectories();
            foreach (string path in Directory.EnumerateFiles(RunsDir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                RunDebriefLog? log = LoadJson(path);
                if (log == null) continue;
                if (requireInProgress && !IsInProgress(log)) continue;
                if (Matches(log, identity)) return log;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Unable to match Spire Debrief JSON log: {ex.Message}");
        }

        return null;
    }

    public static ExportResult ExportMarkdown(RunDebriefLog log, string markdown)
    {
        EnsureDirectories();
        string path = Path.Combine(ExportsDir, $"{FileStem(log)}.md");
        bool saved = false;
        bool copied = false;
        string? error = null;

        try
        {
            File.WriteAllText(path, markdown);
            saved = true;
        }
        catch (Exception ex)
        {
            error = $"Save failed: {ex.Message}";
            MainFile.Logger.Warn(error);
        }

        try
        {
            DisplayServer.ClipboardSet(markdown);
            copied = true;
        }
        catch (Exception ex)
        {
            string clipboardError = $"Clipboard copy failed: {ex.Message}";
            error = error == null ? clipboardError : $"{error}; {clipboardError}";
            MainFile.Logger.Warn(clipboardError);
        }

        return new ExportResult(path, saved, copied, error);
    }

    private static RunDebriefLog? LoadJson(string path)
    {
        try
        {
            RunDebriefLog? log = JsonSerializer.Deserialize<RunDebriefLog>(File.ReadAllText(path), JsonOptions);
            if (log == null) return null;
            log.Metadata.ModVersion ??= MainFile.ModVersion;
            log.Floors ??= [];
            log.FinalState ??= new FinalRunState();
            log.Summary ??= new SummaryCounts();
            return log;
        }
        catch
        {
            return null;
        }
    }

    private static RunDebriefLog? LoadFirstLog(Func<RunDebriefLog, bool> predicate)
    {
        EnsureDirectories();
        foreach (string path in Directory.EnumerateFiles(RunsDir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            RunDebriefLog? log = LoadJson(path);
            if (log != null && predicate(log)) return log;
        }

        return null;
    }

    private static RunIdentity ExtractIdentity(object? source)
    {
        return new RunIdentity(
            ReflectionDataExtractor.TryReadString(
                source,
                "GameRunId",
                "RunId",
                "Run.Id",
                "Run.ID",
                "SaveId",
                "SaveKey",
                "SaveData.Id",
                "SaveData.RunId",
                "State.RunId",
                "State.Id",
                "RunGuid",
                "RunUuid",
                "RunUUID"),
            ReflectionDataExtractor.TryReadString(source, "Seed", "RngSeed", "RunSeed", "State.Rng.Seed"),
            ReflectionDataExtractor.TryReadString(
                source,
                "Character.Name",
                "Character.Id",
                "Character",
                "State.Players.0.Character.Name",
                "State.Players.0.Character.Id"));
    }

    private static bool Matches(RunDebriefLog log, RunIdentity identity)
    {
        if (identity.GameRunId != null && log.Metadata.GameRunId != null)
        {
            return string.Equals(
                identity.GameRunId,
                log.Metadata.GameRunId,
                StringComparison.OrdinalIgnoreCase);
        }

        if (identity.Seed == null || log.Metadata.Seed == null)
            return false;

        if (!string.Equals(identity.Seed, log.Metadata.Seed, StringComparison.OrdinalIgnoreCase))
            return false;

        if (identity.Character == null || log.Metadata.Character == null)
            return true;

        return string.Equals(
            identity.Character,
            log.Metadata.Character,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveModDirectory()
    {
        string? assemblyPath = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            string? assemblyDir = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrWhiteSpace(assemblyDir))
                return assemblyDir;
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            return AppContext.BaseDirectory;

        return Directory.GetCurrentDirectory();
    }

    private static void DeleteStaleJsonFiles(string runId, string currentPath)
    {
        foreach (string path in Directory.EnumerateFiles(RunsDir, "*.json"))
        {
            if (Path.GetFullPath(path).Equals(Path.GetFullPath(currentPath), StringComparison.Ordinal))
                continue;

            RunDebriefLog? existing = LoadJson(path);
            if (existing?.RunId != runId) continue;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"Unable to remove stale Spire Debrief JSON log {path}: {ex.Message}");
            }
        }
    }

    private static string FileStem(RunDebriefLog log)
    {
        string date = DateTimeOffset.TryParse(log.Metadata.StartedAt, out DateTimeOffset parsed)
            ? parsed.ToString("yyyyMMdd-HHmmss")
            : DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string character = Safe(log.Metadata.Character ?? "unknown");
        string seed = Safe(log.Metadata.Seed ?? log.RunId[..Math.Min(8, log.RunId.Length)]);
        string result = Safe(log.Metadata.Result ?? "in-progress");
        return $"{date}_{character}_{seed}_{result}";
    }

    private static string Safe(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch).ToArray());
        while (safe.Contains("--", StringComparison.Ordinal))
            safe = safe.Replace("--", "-", StringComparison.Ordinal);
        return safe.Trim('-').ToLowerInvariant();
    }

    private sealed record RunIdentity(string? GameRunId, string? Seed, string? Character);
}

public sealed record ExportResult(string Path, bool Saved, bool Copied, string? Error);
