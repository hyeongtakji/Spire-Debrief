using System.Reflection;
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
        if (__instance is not Control control) return;
        if (HasExportButtonInScene(control)) return;

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

    public static void DecisionPostfix(object? __instance, object?[] __args, MethodBase __originalMethod)
    {
        try
        {
            string typeName = __originalMethod.DeclaringType?.FullName ?? string.Empty;
            string methodName = __originalMethod.Name;
            object? firstArg = __args.FirstOrDefault(arg => arg != null);

            if (ContainsAny(typeName, "RunManager", "RunState"))
            {
                object? source = ContainsAny(methodName, "EnterRoom")
                    ? firstArg ?? __instance
                    : __instance ?? firstArg;
                RecordRunLifecycle(typeName, methodName, source);
                return;
            }

            if (ContainsAny(typeName, "CardReward", "RewardScreen", "RewardPanel"))
            {
                RecordCardReward(methodName, __instance, firstArg);
                return;
            }

            if (ContainsAny(typeName, "RelicReward"))
            {
                DebriefRecorder.RecordRelicReward(firstArg ?? __instance);
                return;
            }

            if (ContainsAny(typeName, "PotionReward"))
            {
                DebriefRecorder.RecordPotionReward(firstArg ?? __instance);
                return;
            }

            if (ContainsAny(typeName, "Event"))
            {
                RecordEvent(methodName, __instance, firstArg);
                return;
            }

            if (ContainsAny(typeName, "Shop", "Merchant"))
            {
                RecordShop(methodName, firstArg ?? __instance);
                return;
            }

            if (ContainsAny(typeName, "RestSite", "Campfire"))
                RecordRestSite(methodName, firstArg);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Spire Debrief hook failed for {__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name}: {ex.Message}");
        }
    }

    private static void ExportFromScreen(Control screen, Button button)
    {
        try
        {
            object? selectedRun = FindSelectedRunObject(screen) ?? screen;
            RunDebriefLog log = DebriefRecorder.CreateExportLog(selectedRun);
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

    private static void RecordRunLifecycle(string typeName, string methodName, object? source)
    {
        if (ContainsAny(methodName, "Start", "Begin"))
            DebriefRecorder.BeginRun(source);
        else if (ContainsAny(methodName, "EnterRoom"))
            DebriefRecorder.EnterRoom(source);
        else if (ContainsAny(methodName, "Victory"))
            DebriefRecorder.CompleteRun(source, "Victory");
        else if (ContainsAny(methodName, "Abandon"))
            DebriefRecorder.CompleteRun(source, "Abandoned");
        else if (ContainsAny(methodName, "Defeat", "Death", "GameOver"))
            DebriefRecorder.CompleteRun(source, "Defeat");
        else if (IsRunCompletionMethod(methodName))
            DebriefRecorder.CompleteRun(source);
    }

    private static bool IsRunCompletionMethod(string methodName)
    {
        if (ContainsAny(methodName, "Save", "Quit", "Exit", "Load", "Resume", "Menu"))
            return false;

        return ContainsAny(methodName, "CompleteRun", "RunComplete", "EndRun", "RunEnd");
    }

    private static void RecordCardReward(string methodName, object? instance, object? firstArg)
    {
        if (ContainsAny(methodName, "Show", "Open", "Init"))
        {
            DebriefRecorder.RecordCardRewardShown(instance);
        }
        else if (ContainsAny(methodName, "Skip"))
        {
            DebriefRecorder.RecordCardRewardSkipped();
        }
        else if (ContainsAny(methodName, "Choose", "Select", "Pick", "Take"))
        {
            DebriefRecorder.RecordCardRewardShown(instance);
            DebriefRecorder.RecordCardPicked(firstArg ?? instance);
        }
    }

    private static void RecordEvent(string methodName, object? instance, object? firstArg)
    {
        if (ContainsAny(methodName, "Show", "Init"))
            DebriefRecorder.RecordEventOptions(instance);
        else if (ContainsAny(methodName, "Option", "Choose", "Select", "Pick"))
            DebriefRecorder.RecordEventChoice(firstArg, instance);
    }

    private static void RecordShop(string methodName, object? source)
    {
        if (ContainsAny(methodName, "Remove", "Purge"))
            DebriefRecorder.RecordCardRemoved(source);
        else if (ContainsAny(methodName, "Buy", "Purchase", "Take"))
            DebriefRecorder.RecordShopPurchase(source);
    }

    private static void RecordRestSite(string methodName, object? source)
    {
        string action = ContainsAny(methodName, "Upgrade", "Smith") ? "Upgrade" :
            ContainsAny(methodName, "Rest") ? "Rest" :
            ContainsAny(methodName, "Dig") ? "Dig" :
            ContainsAny(methodName, "Recall") ? "Recall" :
            methodName;
        DebriefRecorder.RecordRestSiteAction(action, source);
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

    private static object? FindSelectedRunObject(object source)
    {
        string[] candidates =
        [
            "SelectedRun", "CurrentRun", "Run", "RunData", "HistoryRun", "SelectedHistory",
            "_selectedRun", "_currentRun", "_run", "_runData"
        ];

        foreach (string candidate in candidates)
        {
            object? value = ReflectionDataExtractor.TryReadValue(source, candidate);
            if (value != null) return value;
        }

        return null;
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
