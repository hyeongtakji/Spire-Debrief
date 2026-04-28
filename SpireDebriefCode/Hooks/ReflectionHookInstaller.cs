using System.Reflection;
using HarmonyLib;
using SpireDebrief.SpireDebriefCode;

namespace SpireDebrief.SpireDebriefCode.Hooks;

public static class ReflectionHookInstaller
{
    private static readonly HashSet<MethodBase> Patched = [];

    public static void Install(Harmony harmony)
    {
        int count = 0;
        foreach (Type type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes))
        {
            if (!IsGameType(type)) continue;
            count += PatchRunScreen(harmony, type);
            count += PatchDecisionType(harmony, type);
            count += PatchRunLifecycle(harmony, type);
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

    private static int PatchDecisionType(Harmony harmony, Type type)
    {
        string typeName = type.FullName ?? type.Name;
        if (IsNoisyDecisionType(typeName))
            return 0;

        int count = 0;

        foreach (MethodInfo method in SafeGetMethods(type))
        {
            string methodName = method.Name;
            if (IsCardRewardMethod(typeName, methodName))
                count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), postfix: true) ? 1 : 0;
            else if (IsItemRewardMethod(typeName, methodName))
                count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), postfix: true) ? 1 : 0;
            else if (IsEventMethod(typeName, methodName))
                count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), postfix: true) ? 1 : 0;
            else if (IsShopMethod(typeName, methodName))
                count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), postfix: true) ? 1 : 0;
            else if (IsRestSiteMethod(typeName, methodName))
                count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), postfix: true) ? 1 : 0;
        }

        return count;
    }

    private static int PatchRunLifecycle(Harmony harmony, Type type)
    {
        string typeName = type.FullName ?? type.Name;
        if (!typeName.Contains("RunManager", StringComparison.OrdinalIgnoreCase) &&
            !typeName.Contains("RunState", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        int count = 0;
        foreach (MethodInfo method in SafeGetMethods(type))
        {
            string methodName = method.Name;
            if (!ContainsAny(methodName, "Start", "Begin", "EnterRoom", "Complete", "Victory", "Defeat", "End"))
                continue;
            count += TryPatch(harmony, method, nameof(RuntimeHooks.DecisionPostfix), postfix: true) ? 1 : 0;
        }
        return count;
    }

    private static bool IsCardRewardMethod(string typeName, string methodName) =>
        ContainsAny(typeName, "CardReward", "RewardScreen", "RewardPanel") &&
        (ContainsAny(methodName, "Show", "Open", "Init", "Choose", "Select", "Pick", "Take") ||
         IsCardRewardSkipMethod(methodName));

    private static bool IsItemRewardMethod(string typeName, string methodName) =>
        ContainsAny(typeName, "RelicReward", "PotionReward") &&
        ContainsAny(methodName, "Choose", "Select", "Pick", "Take", "Claim", "Obtain");

    private static bool IsEventMethod(string typeName, string methodName) =>
        IsEventRuntimeType(typeName) &&
        ContainsAny(methodName, "Show", "Init", "Option", "Choose", "Chosen", "Select", "Pick");

    private static bool IsShopMethod(string typeName, string methodName) =>
        ContainsAny(typeName, "Shop", "Merchant") &&
        ContainsAny(methodName, "Buy", "Purchase", "Remove", "Purge", "Take");

    private static bool IsRestSiteMethod(string typeName, string methodName) =>
        ContainsAny(typeName, "RestSite", "Campfire") &&
        ContainsAny(methodName, "Rest", "Upgrade", "Smith", "Dig", "Recall");

    private static bool IsRunExportScreen(string typeName)
    {
        if (!ContainsAny(typeName, "RunHistory", "RunResult", "GameOver"))
            return false;
        if (ContainsAny(typeName, "Button", "Cell", "Entry", "Item", "List", "Row", "Tile"))
            return false;
        return typeName.EndsWith("Screen", StringComparison.OrdinalIgnoreCase) ||
               typeName.EndsWith(".RunHistoryScreen.NRunHistory", StringComparison.Ordinal);
    }

    private static bool IsScreenReadyMethod(string methodName) =>
        methodName.Equals("_Ready", StringComparison.Ordinal) ||
        ContainsAny(methodName, "InitScreen", "ShowScreen", "ResizeScreen");

    private static bool IsNoisyDecisionType(string typeName) =>
        ContainsAny(
            typeName,
            "CardCreationOptions",
            "CardCreationResult",
            "HistoryEntry",
            "MerchantCardEntry",
            "MerchantCardHolder",
            "MerchantInventory",
            "MerchantPotion",
            "MerchantRelic",
            "PostAlternateCardRewardAction",
            "PurchaseStatus");

    private static bool IsCardRewardSkipMethod(string methodName) =>
        methodName.Equals("Skip", StringComparison.OrdinalIgnoreCase) ||
        methodName.Equals("SkipReward", StringComparison.OrdinalIgnoreCase) ||
        methodName.Equals("RewardSkipped", StringComparison.OrdinalIgnoreCase) ||
        methodName.Equals("OnSkip", StringComparison.OrdinalIgnoreCase) ||
        methodName.Equals("OnSkipPressed", StringComparison.OrdinalIgnoreCase) ||
        methodName.Equals("OnSkipButtonPressed", StringComparison.OrdinalIgnoreCase);

    private static bool IsEventRuntimeType(string typeName) =>
        typeName.StartsWith("MegaCrit.Sts2.Core.Nodes.Events.", StringComparison.Ordinal) ||
        typeName.Equals("MegaCrit.Sts2.Core.Events.EventOption", StringComparison.Ordinal) ||
        typeName.StartsWith("MegaCrit.Sts2.Core.Events.Custom.", StringComparison.Ordinal);

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
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
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
}
