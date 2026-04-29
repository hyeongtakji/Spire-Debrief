# Spire Debrief

Spire Debrief is a Slay the Spire 2 mod that exports a completed run as
LLM-friendly Markdown for post-run review.

The mod adds one `Export Debrief` button to `Compendium > Run History`.
When clicked, it reads the run currently loaded by the game's run history
screen, renders a Markdown debrief, copies it to the clipboard when
possible, and saves it under the installed mod folder.

Spire Debrief does not record turn-by-turn combat details and does not
log decisions while a run is in progress. It uses the game's saved
`RunHistory` data after the run appears in Run History.

## Exported Data

The Markdown export includes:

- Run metadata: character, ascension, seed, game version, mod version,
  date, result, and final location
- Final state: HP, gold, potions, final deck, and relics
- Floor-by-floor history: room type, encounter, damage taken when
  available, card choices, picked/skipped rewards, relics, potions,
  event choices, shop purchases/removals, and rest-site choices
- Summary counts for picked cards, skipped card rewards, removals,
  upgrades, relics acquired, shops, and elites
- A ready-to-paste review prompt

If the run history screen does not have a loaded `RunHistory` object,
export fails and the button changes to `Export Failed`.

## Usage

1. Install the mod.
2. Open Slay the Spire 2.
3. Go to `Compendium > Run History`.
4. Navigate to the run you want to review.
5. Click `Export Debrief`.
6. Paste the copied Markdown into an LLM, or open the saved `.md` file.

Exports are saved to:

```text
<Sts2Path>/mods/SpireDebrief/exports/
```

## Installation

Spire Debrief depends on BaseLib. Install and enable
[BaseLib](https://github.com/Alchyr/BaseLib-StS2) before loading this
mod.

Download the
[latest release](https://github.com/hyeongtakji/Spire-Debrief/releases/tag/v0.1.0).

Copy the release files into the mod folder:

```text
<Sts2Path>/mods/SpireDebrief/SpireDebrief.dll
<Sts2Path>/mods/SpireDebrief/SpireDebrief.json
```

## Build From Source

Build with:

```sh
dotnet build -c Release
```

The release DLL is written to:

```text
.godot/mono/temp/bin/Release/SpireDebrief.dll
```

The project has default install paths for Windows, Linux, and macOS
Steam installs. If those defaults do not match your setup, pass the path
explicitly:

```sh
dotnet build -c Release /p:Sts2Path="/path/to/Slay the Spire 2"
```

You can also copy `local.props.template` to `local.props` and set local
paths there. `local.props` is ignored by Git.

When building from WSL against a Windows Steam install, set both
`Sts2Path` and `Sts2DataDir`:

```xml
<Project>
    <PropertyGroup>
        <Sts2Path>/mnt/DRIVE/SteamLibrary/steamapps/common/Slay the Spire 2</Sts2Path>
        <Sts2DataDir>$(Sts2Path)/data_sts2_windows_x86_64</Sts2DataDir>
    </PropertyGroup>
</Project>
```

## Development Notes

The runtime patch is intentionally narrow. It only patches the run
history screen so it can add the export button. Export data comes from
the game's loaded `RunHistory`; there are no card reward, event, shop,
rest-site, combat, or run lifecycle hooks.
