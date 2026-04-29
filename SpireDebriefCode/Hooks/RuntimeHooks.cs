using Godot;
using SpireDebrief.SpireDebriefCode;
using SpireDebrief.SpireDebriefCode.Models;
using SpireDebrief.SpireDebriefCode.Services;

namespace SpireDebrief.SpireDebriefCode.Hooks;

public static class RuntimeHooks
{
    private const string ExportButtonName = "SpireDebriefExportButton";

    public static void RunScreenReadyPostfix(object __instance)
    {
        if (__instance is not Control control)
            return;
        if (HasExportButtonInScene(control))
            return;

        Button button = new()
        {
            Name = ExportButtonName,
            Text = "Export Debrief",
            CustomMinimumSize = new Vector2(220, 44)
        };

        button.Pressed += () => ExportFromScreen(control, button);
        button.Position = new Vector2(32, 32);
        button.ZIndex = 1000;

        Control parent = FindLikelyButtonContainer(control) ?? control;
        parent.AddChild(button);
        MainFile.Logger.Info($"Added Export Debrief button to {__instance.GetType().FullName}.");
    }

    private static void ExportFromScreen(Control screen, Button button)
    {
        try
        {
            if (!RunHistoryDebriefFactory.TryCreate(screen, MainFile.ModVersion, out RunDebriefLog log))
            {
                button.Text = "Export Failed";
                MainFile.Logger.Warn("Export Debrief failed because no run history is loaded.");
                return;
            }

            string markdown = MarkdownRenderer.Render(log);
            ExportResult result = DebriefStorage.ExportMarkdown(log, markdown);

            button.Text = result.Copied ? "Copied Debrief" : result.Saved ? "Saved Debrief" : "Export Failed";
            MainFile.Logger.Info($"Export Debrief clicked. Saved={result.Saved}, Copied={result.Copied}, Path={result.Path}, Error={result.Error ?? "none"}");
        }
        catch (Exception ex)
        {
            button.Text = "Export Failed";
            MainFile.Logger.Error($"Export Debrief failed: {ex}");
        }
    }

    private static Control? FindLikelyButtonContainer(Control root)
    {
        Queue<Node> queue = new();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            if (current is HBoxContainer or VBoxContainer)
            {
                string name = current.Name.ToString();
                if (ContainsAny(name, "Button", "Footer", "Actions", "Controls", "Top", "Bottom"))
                    return (Control)current;
            }

            foreach (Node child in current.GetChildren())
                queue.Enqueue(child);
        }

        return null;
    }

    private static bool HasDescendantNamed(Node root, string name)
    {
        Queue<Node> queue = new();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            if (current.Name.ToString().Equals(name, StringComparison.Ordinal))
                return true;

            foreach (Node child in current.GetChildren())
                queue.Enqueue(child);
        }

        return false;
    }

    private static bool HasExportButtonInScene(Control control)
    {
        Viewport? root = control.GetTree()?.Root;
        Node searchRoot = root != null ? root : control;
        return HasDescendantNamed(searchRoot, ExportButtonName);
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
