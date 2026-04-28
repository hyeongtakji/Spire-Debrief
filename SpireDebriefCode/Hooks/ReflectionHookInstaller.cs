using System.Reflection;
using HarmonyLib;
using SpireDebrief.SpireDebriefCode;

namespace SpireDebrief.SpireDebriefCode.Hooks;

public static class ReflectionHookInstaller
{
    private static readonly HashSet<MethodBase> Patched = [];
    private static readonly HookSpec[] ExactHooks =
    [
        new("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen", "RefreshOptions"),
        new("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen", "SelectCard"),
        new("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen", "OnAlternateRewardSelected"),
        new("MegaCrit.Sts2.Core.Rewards.RelicReward", "OnSelect"),
        new("MegaCrit.Sts2.Core.Rewards.PotionReward", "OnSelect"),
        new("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", "SetEvent"),
        new("MegaCrit.Sts2.Core.Nodes.Events.NEventLayout", "AddOptions"),
        new("MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton", "OnRelease"),
        new("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCard", "OnSuccessfulPurchase", false),
        new("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantRelic", "OnSuccessfulPurchase", false),
        new("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantPotion", "OnSuccessfulPurchase", false),
        new("MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCardRemoval", "OnSuccessfulPurchase", false),
        new("MegaCrit.Sts2.Core.Nodes.RestSite.NRestSiteButton", "OnRelease"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "SetUpNewSinglePlayer"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "SetUpSavedSinglePlayer"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "SetUpNewMultiPlayer"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "SetUpSavedMultiPlayer"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "SetUpReplay"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "EnterRoomInternal"),
        new("MegaCrit.Sts2.Core.Runs.RunManager", "OnEnded")
    ];

    public static void Install(Harmony harmony)
    {
        int count = 0;
        foreach (Type type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes))
        {
            if (!IsGameType(type)) continue;
            count += PatchRunScreen(harmony, type);
            count += PatchExactHooks(harmony, type);
        }

        MainFile.Logger.Info($"Spire Debrief installed {count} runtime hook candidates.");
    }

    private static int PatchRunScreen(Harmony harmony, Type type)
    {
        string typeName = type.FullName ?? type.Name;
        if (!IsRunExportScreen(typeName))
            return 0;

        int count = 0;
        foreach (MethodInfo method in SafeGetMethods(type))
        {
            if (!IsScreenReadyMethod(method.Name))
                continue;
            count += TryPatch(harmony, method, nameof(RuntimeHooks.RunScreenReadyPostfix), postfix: true) ? 1 : 0;
        }

        return count;
    }

    private static int PatchExactHooks(Harmony harmony, Type type)
    {
        string typeName = type.FullName ?? type.Name;
        HookSpec[] hooks = ExactHooks
            .Where(hook => hook.TypeName.Equals(typeName, StringComparison.Ordinal))
            .ToArray();
        if (hooks.Length == 0) return 0;

        int count = 0;
        foreach (MethodInfo method in SafeGetMethods(type))
        {
            HookSpec? hook = hooks.FirstOrDefault(hook => hook.MethodName.Equals(method.Name, StringComparison.Ordinal));
            if (hook != null)
                count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), hook.Postfix) ? 1 : 0;
        }

        return count;
    }

    private static bool IsRunExportScreen(string typeName)
    {
        if (ContainsAny(typeName, "Button", "Cell", "Entry", "Item", "List", "Row", "Tile"))
            return false;
        return typeName.EndsWith(".RunHistoryScreen.NRunHistory", StringComparison.Ordinal);
    }

    private static bool IsScreenReadyMethod(string methodName) =>
        methodName.Equals("_Ready", StringComparison.Ordinal) ||
        ContainsAny(methodName, "InitScreen", "ShowScreen", "ResizeScreen");

    private static bool TryPatch(Harmony harmony, MethodBase original, string patchName, bool postfix)
    {
        try
        {
            if (!Patched.Add(original)) return false;
            HarmonyMethod patch = new(typeof(RuntimeHooks), patchName);
            if (postfix) harmony.Patch(original, postfix: patch);
            else harmony.Patch(original, prefix: patch);
            return true;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Skipping hook {original.DeclaringType?.FullName}.{original.Name}: {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        catch { return []; }
    }

    private static IEnumerable<MethodInfo> SafeGetMethods(Type type)
    {
        try
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsAbstract && !m.ContainsGenericParameters);
        }
        catch
        {
            return [];
        }
    }

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static bool IsGameType(Type type)
    {
        string assemblyName = type.Assembly.GetName().Name ?? string.Empty;
        string typeName = type.FullName ?? type.Name;
        return assemblyName.Equals("sts2", StringComparison.OrdinalIgnoreCase) ||
               typeName.StartsWith("MegaCrit.Sts2.", StringComparison.Ordinal);
    }

    private sealed record HookSpec(string TypeName, string MethodName, bool Postfix = true);
}
