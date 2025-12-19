<div align="center">

# [SwiftlyS2] Retakes

[![GitHub Stars](https://img.shields.io/github/stars/a2Labs-cc/SwiftlyS2-Retakes?style=social)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/stargazers)
[![GitHub Issues](https://img.shields.io/github/issues/a2Labs-cc/SwiftlyS2-Retakes?style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/issues)
[![GitHub License](https://img.shields.io/github/license/a2Labs-cc/SwiftlyS2-Retakes?style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/blob/main/LICENSE)

</div>

## Overview

**SwiftlyS2-Retakes** is a CS2 retakes game mode for **SwiftlyS2**.

> [!CAUTION]
> This plugin is currently in **beta**. Expect breaking changes and bugs.

It handles:

- **Round flow** (freeze time, allocations, announcements)
- **Map configs & spawns** (per-map JSON configs in `resources/maps`)
- **Weapon selection** (menus + preferences)
- **Queue system** (max players + late joiners)
- **Utility features** like instant bomb options, anti team-flash, clutch announce
- **End-of-round damage report** (per opponent)

## Requirements

- SwiftlyS2 (CS2)
- .NET runtime as required by SwiftlyS2 managed plugins

## Installation

1. Download/build the plugin.
2. Copy the published plugin folder to your server:

```
.../game/csgo/addons/swiftlys2/plugins/Retakes/
```

3. Ensure the plugin has its `resources/` folder alongside the DLL (maps, translations, gamedata).
4. Start/restart the server.

## Configuration

The plugin uses SwiftlyS2’s JSON config system.

- **File name**: `config.json`
- **Section**: `retakes`

On first run the config will be created automatically. The exact resolved path is logged on startup:

```
Retakes: config.json path: ...
```

Useful config fields (non-exhaustive):

- `retakes.server.freezeTimeSeconds`
- `retakes.server.chatPrefix`
- `retakes.server.chatPrefixColor`
- `retakes.queue.*`
- `retakes.teamBalance.*`
- `retakes.weapons.*`

## Map configs

Map configs live in:

```
plugins/Retakes/resources/maps/*.json
```

Each map file contains the spawns used by the retakes allocator.

## Commands

### Admin / Root

- `!forcesite <A/B>`
- `!forcestop`
- `!loadcfg <mapname>`
- `!listcfg`
- `!reloadcfg`

### Spawn editor (Root)

- `!editspawns <A/B>`
- `!addspawn <T/CT> [planter]`
- `!remove <id>`
- `!namespawn <id> <name>`
- `!gotospawn <id>`
- `!savespawns`
- `!stopediting`

### Player

- `!guns` / `!gun`
- `!retake`
- `!spawns`
- `!awp`
- `!voices`

### Debug

- `!debugqueues`

## Damage report

At **round end**, each player receives a per-opponent summary in chat (damage/hits dealt + taken), using translations:

- `damage.report.header`
- `damage.report.line`

You can edit the message format/colors in:

`resources/translations/en.jsonc`

## Building

```bash
dotnet build
```

Artifacts are emitted into `build/`.

## Publishing

```bash
dotnet publish -c Release
```

This project is configured to package the published output.

## Credits
- Developed by [aga](https://github.com/agasking1337)
- Readme template by [criskkky](https://github.com/criskkky)
- Release workflow based on [K4ryuu/K4-Guilds-SwiftlyS2 release workflow](https://github.com/K4ryuu/K4-Guilds-SwiftlyS2/blob/main/.github/workflows/release.yml)
- All contributors listed in the [Contributors Section](https://github.com/agasking1337/PluginsAutoUpdate/graphs/contributors)

### References / inspiration

- https://github.com/B3none/cs2-retakes
- https://github.com/itsAudioo/CS2BombsiteAnnouncer
- https://github.com/yonilerner/cs2-retakes-allocator