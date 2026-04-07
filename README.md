<div align="center">

# [SwiftlyS2] Retakes

[![GitHub Release](https://img.shields.io/github/v/release/a2Labs-cc/SwiftlyS2-Retakes?color=FFFFFF&style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/releases/latest)
[![GitHub Issues](https://img.shields.io/github/issues/a2Labs-cc/SwiftlyS2-Retakes?color=FF0000&style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/issues)
[![GitHub Downloads](https://img.shields.io/github/downloads/a2Labs-cc/SwiftlyS2-Retakes/total?color=blue&style=flat-square)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/releases)
[![GitHub Stars](https://img.shields.io/github/stars/a2Labs-cc/SwiftlyS2-Retakes?style=social)](https://github.com/a2Labs-cc/SwiftlyS2-Retakes/stargazers)<br/>
  <sub>Made by <a href="https://github.com/agasking1337" rel="noopener noreferrer" target="_blank">aga</a></sub>
  <br/>
  <p align="center">
    <a href="https://discord.com/invite/vmUwHYubyp" target="_blank">
      <img src="https://img.shields.io/badge/Join%20Discord-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord">
    </a>
  </p>
</div>

## Overview

**SwiftlyS2-Retakes** is a CS2 retakes game mode for **SwiftlyS2**.

It handles:

- **Round Flow** (automatic site selection, freeze time management, and round cleanup)
- **Automatic Bomb Planting** (bomb is automatically planted at freeze end at the selected site)
- **Instant Plant & Defuse** (instant plant support and smart instant defuse logic with molly/enemy checks)
- **Weapon Allocation** (dynamic weapon, armor, and utility distribution based on round type)
- **Weapon Preferences** (persistent menus for players to choose their preferred loadouts)
- **Map Configurations** (custom per-map JSON configs for spawns and smoke scenarios)
- **Spawn Editor** (comprehensive in-game tools for managing spawns and smokes)
- **Queue System** (supports max player limits, spectator queues, and late joiners)
- **Smoke Scenarios** (pre-defined smokes that can be forced or randomly spawned)
- **AFK Manager** (automatically moves inactive players to spectator or kicks them)
- **Anti Team-Flash** (blocks teammates from being blinded by friendly flashes)
- **Solo Bot System** (automatically spawns a bot to play against when only one human is present)
- **World Breaker** (automatically breaks glass and opens doors at round start for better flow)
- **Damage Reports** (detailed per-opponent damage summary at the end of each round)
- **Clutch Announcements** (notifies players of 1vX clutch situations)
- **Native Message Suppression** (suppresses redundant game messages for a cleaner chat experience)

## Support

Need help or have questions? Join our Discord server:

<p align="center">
  <a href="https://discord.com/invite/vmUwHYubyp" target="_blank">
    <img src="https://img.shields.io/badge/Join%20Discord-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord">
  </a>
</p>

## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbspDownload Latest Plugin Version</strong> ⇢
    <a href="https://github.com/a2Labs-cc/SwiftlyS2-Retakes/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>🍪</code>
    <strong>&nbspDownload Latest Cookies Version</strong> ⇢
    <a href="https://github.com/SwiftlyS2-Plugins/Cookies/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbspDownload Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Dependencies

| Plugin | Required | Purpose |
| :--- | :--- | :--- |
| [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) | **Yes** | Persistent player preferences (AWP toggle, weapon loadouts, spawn selections, etc.) |

> **Note:** Without the Cookies plugin installed and loaded, player preferences will **not** be saved between sessions — all settings will reset on disconnect.

## Installation

1. Download/build the plugin.
2. Install the **[Cookies](https://github.com/SwiftlyS2-Plugins/Cookies)** plugin (see Dependencies above).
3. Copy the published plugin folder to your server:

```
.../game/csgo/addons/swiftlys2/plugins/Retakes/
```

4. Ensure the plugin has its `resources/` folder alongside the DLL (maps, translations, gamedata).
5. Start/restart the server.

## Player Preferences & Persistence

The plugin saves **18 different player preferences** using the [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) plugin for persistent storage across sessions.

### What Gets Saved

All player preferences are automatically saved and persist between disconnects/reconnects:

#### Weapon Preferences
- **AWP Toggle** (`!awp`) — Whether the player wants to receive an AWP when available
- **SSG08 Toggle** — Whether the player wants to receive a Scout when available  
- **AWP Priority** — Whether the player gets priority in AWP allocation
- **Pistol Round Primary** — Preferred primary weapon for pistol rounds (per-team or shared)
- **Half-Buy Primary/Secondary** — Preferred weapons for half-buy rounds (per-team or shared)
- **Full-Buy Primary/Secondary** — Preferred weapons for full-buy rounds (per-team or shared)
- **Default Weapon Loadouts** - Set default weapon selection based on round type.

#### Spawn Preferences
- **Spawn Menu Toggle** (`!spawns`) — Whether the CT spawn selection menu is enabled
- **T Spawn A/B** — Preferred spawn position for T side on each bombsite
- **CT Spawn A/B** — Preferred spawn position for CT side on each bombsite

### How It Works

1. **On Player Connect** — The Cookies plugin automatically loads all saved preferences from the database
2. **On Setting Change** — Any preference change (via `!guns`, `!awp`, `!spawns`, etc.) is immediately saved to Cookies
3. **Auto-Save** — Cookies flushes all changes to the database every 5 seconds
4. **On Disconnect** — All preferences are saved to the database before the player leaves

### Without Cookies Plugin

If the Cookies plugin is **not installed**, the plugin will still function but:
- ⚠️ All preferences reset to defaults when a player disconnects
- ⚠️ Settings like `!awp`, weapon loadouts, and spawn selections are **lost between sessions**
- ⚠️ Players must reconfigure their preferences every time they join

### Technical Details

- **Storage Keys**: All preferences use the `retakes_*` prefix (e.g., `retakes_wants_awp`, `retakes_ct_spawn_a`)
- **Data Types**: Booleans for toggles, integers for spawn IDs, strings for weapon names
- **Per-Team Settings**: Weapon preferences can be configured per-team (T/CT) or shared, based on `config.json` setting `retakes.preferences.usePerTeamPreferences`

## Weapon Allocation

The plugin automatically gives players weapons, armor, and grenades each round. The loadout depends on the **round type** and respects player preferences set via `!guns`.

### Round Types

| Round Type | Armor | Primary | Secondary | Grenades | CT Defuser |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Pistol** | Kevlar | Pistols | ❌ | Flash or smoke | 1 random player |
| **Half-Buy** | Kevlar + Helmet | SMGs/Budget rifles | ✅ Pistol | Smoke + 1-2 more | Everyone |
| **Full-Buy** | Kevlar + Helmet | Rifles (or AWP/Scout) | ✅ Pistol | Smoke + 1-2 more | Everyone |

### How Round Types Are Selected

Set via `retakes.allocation.roundType` in config:

| Mode | Description |
| :--- | :--- |
| `"random"` **(default)** | Mix of all types based on percentages (20% pistol, 30% half, 50% full) |
| `"pistol"` or `"p"` | Pistol rounds only |
| `"half"` or `"h"` | Half-buy rounds only |
| `"full"` or `"f"` | Full-buy rounds only |
| `"sequence"` | Custom sequence (e.g., 3 pistol → 2 half → full forever) |

<details>
<summary><b>Sequence Mode Example</b></summary>

```json
{
  "retakes": {
    "allocation": {
      "roundType": "sequence",
      "roundTypeSequence": [
        { "Type": "Pistol", "Count": 3 },
        { "Type": "HalfBuy", "Count": 2 },
        { "Type": "FullBuy", "Count": 25 }
      ]
    }
  }
}
```
Plays 3 pistol rounds, then 2 half-buy, then full-buy for all remaining rounds.
</details>

### AWP & Scout Allocation

**Only on full-buy rounds.** Players must enable `!awp` or SSG08 in their preferences to be eligible.

#### AWP Settings
- **How many per team:** `awpPerTeam` (default: 1)
- **Low population mode:** If team has ≤ 4 players, there's only a 50% chance AWPs spawn
- **VIP priority:** Players with the `@css/vip` flag (configurable) can get priority AWP picks

#### Scout (SSG08) Settings
- **How many per team:** `ssg08PerTeam` (default: 0 = disabled)
- Players who get an AWP won't get a Scout

### Weapon Selection

- **Human players:** Get their preferred weapons from the `!guns` menu
- **Bots:** Get random weapons from the allowed list
- If a player has no saved preference, the plugin first checks `retakes.weapons.defaults.*`
- If neither a saved preference nor a configured default is available, a random weapon is chosen

### Default Loadouts

You can define default loadouts that are used before a player chooses their own weapons in `!guns`.

```json
      "Defaults": {
        "Pistol": {
          "Primary": {
            "T": "weapon_glock",
            "Ct": "weapon_usp_silencer"
          },
          "Secondary": {
            "T": null,
            "Ct": null
          }
        },
        "HalfBuy": {
          "Primary": {
            "T": "weapon_mac10",
            "Ct": "weapon_mp7"
          },
          "Secondary": {
            "T": "weapon_deagle",
            "Ct": "weapon_deagle"
          }
        },
        "FullBuy": {
          "Primary": {
            "T": "weapon_ak47",
            "Ct": "weapon_m4a1"
          },
          "Secondary": {
            "T": "weapon_deagle",
            "Ct": "weapon_deagle"
          }
        }
      }
```

- `pistol`, `halfBuy`, and `fullBuy` all support `primary`
- `halfBuy` and `fullBuy` also support `secondary`
- Player selections still override these defaults and remain persisted through Cookies

### Grenades

| Round Type | Grenades Given |
| :--- | :--- |
| **Pistol** | 1 flashbang OR 1 smoke (random) |
| **Half-Buy** | 1 smoke + 1 random (flash/HE/molly) |
| **Full-Buy** | 1 smoke + 1 random + 50% chance for a 2nd random |

### Common Settings

| Config | Default | Description |
| :--- | :--- | :--- |
| `retakes.allocation.enabled` | `true` | Enable/disable weapon allocation |
| `retakes.allocation.stripWeapons` | `true` | Remove old weapons before giving new ones |
| `retakes.allocation.givePistolOnRifleRounds` | `true` | Give secondary pistol on half/full-buy rounds |
| `retakes.preferences.usePerTeamPreferences` | `false` | Separate T/CT weapon preferences in `!guns` menu |

## Configuration

The plugin uses SwiftlyS2's JSON config system.

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
- `retakes.preferences.usePerTeamPreferences` — Enable separate weapon preferences for T/CT

### Damage Report

| Config field | Default | Description |
| :--- | :--- | :--- |
| `retakes.damageReport.enabled` | `true` | Enable/disable the per-opponent damage summary shown in chat at round end |

### Instant Defuse Messages

The instant defuse result messages can be targeted per-audience:

| Config field | Default | Description |
| :--- | :--- | :--- |
| `retakes.instantBomb.successfulMessageTarget` | `"all"` | Who sees the "defused successfully" message |
| `retakes.instantBomb.unsuccessfulMessageTarget` | `"all"` | Who sees the "defuse failed" message |

**Accepted values** (must be a lowercase string):

| Value | Behaviour |
| :--- | :--- |
| `"all"` | Sent to every player on the server |
| `"team"` | Sent only to the CT team (the defusing side) |
| `"player"` | Sent only to the player who attempted the defuse |

```json
"instantBomb": {
  "successfulMessageTarget": "all",
  "unsuccessfulMessageTarget": "all"
}
```

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
| `!addsmoke <A/B> [name]` | Adds a smoke scenario at your current position. |
| `!removesmoke <id>` | Removes the smoke scenario with the specified ID. |
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
- All spawns are based on [B3none/cs2-retakes](https://github.com/B3none/cs2-retakes)
- Inspired by [itsAudioo/CS2BombsiteAnnouncer](https://github.com/itsAudioo/CS2BombsiteAnnouncer)
- Inspired by [yonilerner/cs2-retakes-allocator](https://github.com/yonilerner/cs2-retakes-allocator)
