<div align="center">

# [SwiftlyS2] Retakes

[![GitHub Release](https://img.shields.io/github/v/release/a2Labs-cc/SwiftlyS2-Retakes?color=FFFFFF&style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/releases/latest)
[![GitHub Issues](https://img.shields.io/github/issues/a2Labs-cc/SwiftlyS2-Retakes?color=FF0000&style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/issues)
[![GitHub Downloads](https://img.shields.io/github/downloads/a2Labs-cc/SwiftlyS2-Retakes/total?color=blue&style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/releases)
[![GitHub Stars](https://img.shields.io/github/stars/a2Labs-cc/SwiftlyS2-Retakes?style=social)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/stargazers)<br/>
  <sub>Made by <a href="https://github.com/agasking1337" rel="noopener noreferrer" target="_blank">aga</a></sub>
  <br/>

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

## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbspDownload Latest Plugin Version</strong> ⇢
    <a href="https://github.com/agasking1337/PluginsAutoUpdate/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbspDownload Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

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

| Command | Description | Permission |
| :--- | :--- | :--- |
| `!forcesite <A/B>` | Forces the game to be played on a specific bombsite. | Root |
| `!forcestop` | Clears the forced bombsite. | Root |
| `!forcesmokes` | Forces smoke scenarios to spawn every round until stopped. | Root |
| `!stopsmokes` | Disables forced smokes (returns to normal/random smoke behavior). | Root |
| `!loadcfg <mapname>` | Loads a specific map configuration. | Root |
| `!listcfg` | Lists all available map configurations. | Root |
| `!reloadcfg` | Reloads the main `config.json`. | Root |
| `!scramble` | Scrambles the teams on the next round. | Admin |

### Spawn Editor (Root)

| Command | Description |
| :--- | :--- |
| `!editspawns [A/B]` | Enters spawn editing mode. Defaults to showing **Both** sites if no argument is provided. |
| `!addspawn <T/CT> [planter] [A/B]` | Adds a spawn at your current position. **Note:** If viewing both sites, you must specify `A` or `B`. |
| `!remove <id>` | Removes the spawn with the specified ID. |
| `!namespawn <id> <name>` | Sets a descriptive name for the spawn. |
| `!gotospawn <id>` | Teleports you to the spawn's position. |
| `!replysmoke <smoke id>` | Instantly deploys/replays the smoke scenario with the specified ID (for testing). Requires spawn edit mode. |
| `!savespawns` | Saves all changes to the map config file. |
| `!stopediting` | Exits spawn editing mode and reloads the map. |

### Player

| Command | Description |
| :--- | :--- |
| `!guns` / `!gun` | Opens the weapon preference menu. |
| `!retake` | Opens the main Retakes menu (spawn preference, AWP, etc.). |
| `!spawns` | Toggles the spawn selection menu. |
| `!awp` | Toggles AWP preference. |
| `!voices` | Toggles voice announcements. |

### Debug

| Command | Description |
| :--- | :--- |
| `!debugqueues` | Prints debug information about the queues. |

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

## Credits
- Readme template by [criskkky](https://github.com/criskkky)
- Release workflow based on [K4ryuu/K4-Guilds-SwiftlyS2 release workflow](https://github.com/K4ryuu/K4-Guilds-SwiftlyS2/blob/main/.github/workflows/release.yml)
- All contributors listed in the [Contributors Section](https://github.com/agasking1337/PluginsAutoUpdate/graphs/contributors)

### References / inspiration

- https://github.com/B3none/cs2-retakes
- https://github.com/itsAudioo/CS2BombsiteAnnouncer
- https://github.com/yonilerner/cs2-retakes-allocator