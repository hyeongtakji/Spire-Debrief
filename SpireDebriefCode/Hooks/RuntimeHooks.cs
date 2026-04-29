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

            if (typeName.Equals("MegaCrit.Sts2.Core.Runs.RunManager", StringComparison.Ordinal))
            {
                object? source = methodName.Equals("EnterRoomInternal", StringComparison.Ordinal)
                    ? firstArg ?? __instance
                    : __instance ?? firstArg;
                RecordRunLifecycle(methodName, source, firstArg);
                return;
            }

            if (typeName.Equals("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen", StringComparison.Ordinal))
            {
                RecordCardReward(methodName, __instance, firstArg);
                return;
            }

            if (typeName.Equals("MegaCrit.Sts2.Core.Rewards.RelicReward", StringComparison.Ordinal))
            {
                DebriefRecorder.RecordRelicReward(firstArg ?? __instance);
                return;
            }

            if (typeName.Equals("MegaCrit.Sts2.Core.Rewards.PotionReward", StringComparison.Ordinal))
            {
                DebriefRecorder.RecordPotionReward(firstArg ?? __instance);
                return;
            }

            if (typeName.Equals("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", StringComparison.Ordinal) ||
                typeName.Equals("MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton", StringComparison.Ordinal))
            {
                RecordEvent(methodName, __instance, firstArg);
                return;
            }

            if (typeName.StartsWith("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchant", StringComparison.Ordinal))
            {
                RecordShop(methodName, __instance, firstArg);
                return;
            }

            if (typeName.Equals("MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteButton", StringComparison.Ordinal))
                RecordRestSite(__instance);
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
            object? selectedRun = FindSelectedRunObject(screen);
            if (selectedRun == null && DebriefStorage.HasUsableIdentity(screen))
                selectedRun = screen;

            RunDebriefLog? log = selectedRun == null
                ? null
                : DebriefRecorder.CreateExportLog(selectedRun);
            if (log == null)
            {
                button.Text = "Select Run First";
                MainFile.Logger.Warn("Export Debrief skipped because no matching selected run could be found.");
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

    private static void RecordRunLifecycle(string methodName, object? source, object? firstArg)
    {
        if (methodName.StartsWith("SetUp", StringComparison.Ordinal))
            DebriefRecorder.BeginRun(source);
        else if (methodName.Equals("EnterRoomInternal", StringComparison.Ordinal))
            DebriefRecorder.EnterRoom(source);
        else if (methodName.Equals("OnEnded", StringComparison.Ordinal))
        {
            string? result = firstArg is bool victory
                ? victory ? "Victory" : "Defeat"
                : null;
            DebriefRecorder.CompleteRun(source, result);
        }
    }

    private static void RecordCardReward(string methodName, object? instance, object? firstArg)
    {
        if (methodName.Equals("RefreshOptions", StringComparison.Ordinal))
        {
            DebriefRecorder.RecordCardRewardShown(instance);
        }
        else if (methodName.Equals("OnAlternateRewardSelected", StringComparison.Ordinal))
        {
            string? action = ReflectionDataExtractor.ResolveString(firstArg) ?? firstArg?.ToString();
            if (action != null &&
                action.Contains("DismissScreenAndRemoveReward", StringComparison.Ordinal))
            {
                DebriefRecorder.RecordCardRewardSkipped(methodName);
            }
        }
        else if (methodName.Equals("SelectCard", StringComparison.Ordinal))
        {
            DebriefRecorder.RecordCardPicked(firstArg ?? instance);
        }
    }

    private static void RecordEvent(string methodName, object? instance, object? firstArg)
    {
        if (methodName.Equals("SetEvent", StringComparison.Ordinal) ||
            methodName.Equals("AddOptions", StringComparison.Ordinal))
        {
            DebriefRecorder.RecordEventOptions(instance);
        }
        else if (methodName.Equals("OnRelease", StringComparison.Ordinal))
        {
            if (ReflectionDataExtractor.TryReadBool(instance, "Option.IsLocked") == true)
                return;

            DebriefRecorder.RecordEventChoice(
                ReflectionDataExtractor.TryReadValue(instance, "Option"),
                instance);
        }
    }

    private static void RecordShop(string methodName, object? instance, object? firstArg)
    {
        if (!methodName.Equals("OnSuccessfulPurchase", StringComparison.Ordinal))
            return;

        string typeName = instance?.GetType().FullName ?? string.Empty;
        if (typeName.EndsWith(".NMerchantCardRemoval", StringComparison.Ordinal))
            DebriefRecorder.RecordCardRemoved(firstArg ?? instance);
        else
            DebriefRecorder.RecordShopPurchase(instance ?? firstArg);
    }

    private static void RecordRestSite(object? source)
    {
        if (ReflectionDataExtractor.TryReadBool(source, "_isUnclickable") == true)
            return;

        object? option = ReflectionDataExtractor.TryReadValue(source, "Option");
        string action = ResolveRestSiteAction(option);
        DebriefRecorder.RecordRestSiteAction(action, option ?? source);
    }

    private static string ResolveRestSiteAction(object? option)
    {
        string typeName = option?.GetType().Name ?? string.Empty;
        string? title = ReflectionDataExtractor.TryReadString(option, "Title", "Name", "Id");
        string value = $"{typeName} {title}";

        if (ContainsAny(value, "Smith", "Upgrade")) return "Upgrade";
        if (ContainsAny(value, "Heal", "Rest")) return "Rest";
        if (ContainsAny(value, "Dig")) return "Dig";
        if (ContainsAny(value, "Recall")) return "Recall";
        return title ?? typeName;
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
