using System.Reflection;
using Godot;
using SpireDebrief.SpireDebriefCode;
using SpireDebrief.SpireDebriefCode.Models;

namespace SpireDebrief.SpireDebriefCode.Services;

public static class DebriefStorage
{
    public static string BaseDir => ResolveModDirectory();
    public static string ExportsDir => Path.Combine(BaseDir, "exports");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ExportsDir);
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

    private static string FileStem(RunDebriefLog log)
    {
        string date = DateTimeOffset.TryParse(log.Metadata.StartedAt, out DateTimeOffset parsed)
            ? parsed.ToString("yyyyMMdd-HHmmss")
            : DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string character = Safe(log.Metadata.Character ?? "unknown");
        string seed = Safe(log.Metadata.Seed ?? log.RunId[..Math.Min(8, log.RunId.Length)]);
        string result = Safe(log.Metadata.Result ?? "unknown");
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
}

public sealed record ExportResult(string Path, bool Saved, bool Copied, string? Error);
