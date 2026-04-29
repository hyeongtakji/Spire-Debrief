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
            string typeName = type.FullName ?? type.Name;
            if (!typeName.EndsWith(".RunHistoryScreen.NRunHistory", StringComparison.Ordinal))
                continue;

            foreach (MethodInfo method in SafeGetMethods(type))
            {
                if (IsScreenReadyMethod(method.Name))
                    count += TryPatch(harmony, method) ? 1 : 0;
            }
        }

        MainFile.Logger.Info($"Spire Debrief installed {count} run history hook candidates.");
    }

    private static bool IsScreenReadyMethod(string methodName) =>
        methodName.Equals("_Ready", StringComparison.Ordinal) ||
        methodName.Contains("InitScreen", StringComparison.Ordinal) ||
        methodName.Contains("ShowScreen", StringComparison.Ordinal) ||
        methodName.Contains("ResizeScreen", StringComparison.Ordinal);

    private static bool TryPatch(Harmony harmony, MethodBase original)
    {
        try
        {
            if (!Patched.Add(original))
                return false;

            HarmonyMethod patch = new(typeof(RuntimeHooks), nameof(RuntimeHooks.RunScreenReadyPostfix));
            harmony.Patch(original, postfix: patch);
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
            return type.GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly)
                .Where(m => !m.IsAbstract && !m.ContainsGenericParameters);
        }
        catch
        {
            return [];
        }
    }
}
