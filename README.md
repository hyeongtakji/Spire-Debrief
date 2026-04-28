# Spire Debrief

Spire Debrief is a Slay the Spire 2 mod MVP that records high-level run decisions and exports a post-run Markdown debrief suitable for pasting into an LLM.

## Current Hook Strategy

This repository started as an empty Git repo, so there were no existing mod files or game API wrappers to patch. The scaffold follows the public Slay the Spire 2 C# Godot/BaseLib mod shape:

- `[ModInitializer]` entry point in `SpireDebriefCode/MainFile.cs`
- Harmony runtime patches installed by `SpireDebriefCode/Hooks/ReflectionHookInstaller.cs`
- Defensive screen injection for run history, run result, and game over screens
- Structured JSON logs written during play
- Markdown rendered only when the user exports

The hook installer discovers loaded game types by name at runtime. It targets stable screen and decision concepts without referencing private game types directly:

- Run screens: type names containing `RunHistory`, `RunResult`, `GameOver`, `Victory`, `Defeat`, or `Stats`
- Card reward methods: type/method names containing `CardReward`, `Reward`, `Choose`, `Select`, `Pick`, `Skip`, or `Take`
- Event methods: type/method names containing `Event`, `Option`, `Choose`, or `Select`
- Shop methods: type/method names containing `Shop`, `Merchant`, `Buy`, `Purchase`, `Remove`, or `Purge`
- Rest site methods: type/method names containing `RestSite`, `Campfire`, `Rest`, `Upgrade`, `Smith`, `Dig`, or `Recall`
- Run lifecycle methods: `RunManager` methods containing `Start`, `Begin`, `EnterRoom`, `Complete`, `Victory`, `Defeat`, or `End`

If a runtime type or method is absent in a game build, the mod logs the miss and continues.

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
        <Sts2Path>/mnt/c/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2</Sts2Path>
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

Structured logs and exports are written under the installed mod folder:

```text
<Sts2Path>/mods/SpireDebrief/runs/*.json
<Sts2Path>/mods/SpireDebrief/exports/*.md
```

The export button also attempts to copy Markdown to the clipboard.
