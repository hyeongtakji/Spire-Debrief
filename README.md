# Spire Debrief

Spire Debrief is a Slay the Spire 2 mod MVP that exports run history
as a post-run Markdown debrief suitable for pasting into an LLM.

## Current Strategy

This repository started as an empty Git repo, so there were no existing mod files or game API wrappers to patch. The scaffold follows the public Slay the Spire 2 C# Godot/BaseLib mod shape:

- `[ModInitializer]` entry point in `SpireDebriefCode/MainFile.cs`
- Harmony runtime patches installed by `SpireDebriefCode/Hooks/ReflectionHookInstaller.cs`
- Defensive screen injection for the compendium run history screen only
- Markdown export from the game's saved `RunHistory` object
- Markdown rendered only when the user exports

The hook installer discovers loaded game types at runtime, but it only
patches the run history screen. The current MVP hook is:

- Export UI: `RunHistoryScreen.NRunHistory` `_Ready`/screen resize hooks

If no run history is loaded on the screen, export fails with a logged
warning. The mod does not record anything while a run is in progress.

## Build

Install the public StS2 mod template prerequisites, then build with the game installed:

```sh
dotnet build
```

Override the game path if auto-discovery cannot find it:

```sh
dotnet build /p:Sts2Path="/path/to/Slay the Spire 2"
```

Or copy `local.props.template` to `local.props` and set `Sts2Path`.
When building from WSL against a Windows Steam install, also set
`Sts2DataDir` to the Windows data directory:

```xml
<Project>
    <PropertyGroup>
        <Sts2Path>/mnt/DRIVE/SteamLibrary/steamapps/common/Slay the Spire 2</Sts2Path>
        <Sts2DataDir>$(Sts2Path)/data_sts2_windows_x86_64</Sts2DataDir>
    </PropertyGroup>
</Project>
```

To install manually, copy the built `SpireDebrief.dll` and the root
`SpireDebrief.json` manifest into:

```text
<Sts2Path>/mods/SpireDebrief/
```

## Output

Exports are written under the installed mod folder:

```text
<Sts2Path>/mods/SpireDebrief/exports/*.md
```

The export button also attempts to copy Markdown to the clipboard.
