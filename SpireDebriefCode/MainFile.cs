using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SpireDebrief.SpireDebriefCode.Hooks;
using SpireDebrief.SpireDebriefCode.Services;

namespace SpireDebrief.SpireDebriefCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireDebrief";
    public const string ModVersion = "v0.1.0";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        try
        {
            DebriefStorage.EnsureDirectories();
            DebriefRecorder.Initialize(ModVersion);

            Harmony harmony = new(ModId);
            ReflectionHookInstaller.Install(harmony);

            Logger.Info($"{ModId} {ModVersion} initialized.");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize {ModId}: {ex}");
        }
    }
}
