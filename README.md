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

The build copies `SpireDebrief.dll` and `SpireDebrief.json` into the game's `mods/SpireDebrief/` folder.

## Output

Structured logs and exports are written under the Godot user data directory:

```text
<Godot user data>/SpireDebrief/runs/*.json
<Godot user data>/SpireDebrief/exports/*.md
```

The export button also attempts to copy Markdown to the clipboard.
