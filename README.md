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

- **Round Flow** — automatic site selection, freeze time management, and round cleanup
- **Automatic Bomb Planting** — bomb is planted at freeze end at the selected site
- **Instant Plant & Defuse** — instant plant support and smart instant defuse logic with molly/enemy checks
- **Weapon Allocation** — dynamic weapon, armor, and utility distribution based on round type
- **Weapon Preferences** — persistent menus for players to choose their preferred loadouts
- **Grenade Allocation** — configurable per-round-type grenade distribution (random, fixed, or damage-based dynamic)
- **Map Configurations** — custom per-map JSON configs for spawns and smoke scenarios
- **Spawn Editor** — comprehensive in-game tools for managing spawns and smokes
- **Queue System** — max player limits, spectator queues, and late joiner handling
- **Smoke Scenarios** — pre-defined smokes that can be forced or randomly spawned
- **AFK Manager** — automatically moves inactive players to spectator or kicks them
- **Anti Team-Flash** — blocks teammates from being blinded by friendly flashes
- **Solo Bot System** — automatically spawns a bot when only one human is present
- **World Breaker** — automatically breaks glass and opens doors at round start
- **Damage Reports** — per-opponent damage summary at the end of each round
- **Clutch Announcements** — notifies players of 1vX clutch situations
- **Native Message Suppression** — suppresses redundant game messages for a cleaner chat

## Support

<p align="center">
  <a href="https://discord.com/invite/vmUwHYubyp" target="_blank">
    <img src="https://img.shields.io/badge/Join%20Discord-5865F2?logo=discord&logoColor=white&style=for-the-badge" alt="Discord">
  </a>
</p>

## Download Shortcuts

<ul>
  <li><code>📦</code> <strong>Download Latest Plugin Version</strong> ⇢ <a href="https://github.com/a2Labs-cc/SwiftlyS2-Retakes/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a></li>
  <li><code>🍪</code> <strong>Download Latest Cookies Version</strong> ⇢ <a href="https://github.com/SwiftlyS2-Plugins/Cookies/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a></li>
  <li><code>⚙️</code> <strong>Download Latest SwiftlyS2 Version</strong> ⇢ <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a></li>
</ul>

## Dependencies

| Plugin | Required | Purpose |
| :--- | :--- | :--- |
| [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) | **Yes** | Persistent player preferences (AWP toggle, weapon loadouts, spawn selections, etc.) |

> **Note:** Without the Cookies plugin, player preferences will not be saved between sessions — all settings reset on disconnect.

## Installation

1. Download or build the plugin.
2. Install the **[Cookies](https://github.com/SwiftlyS2-Plugins/Cookies)** plugin.
3. Copy the published plugin folder to your server:
   ```
   .../game/csgo/addons/swiftlys2/plugins/Retakes/
   ```
4. Ensure the `resources/` folder is alongside the DLL (maps, translations, gamedata).
5. Start or restart the server.

---

## Player Preferences

The plugin saves player preferences using the [Cookies](https://github.com/SwiftlyS2-Plugins/Cookies) plugin. All preferences persist between sessions and are loaded automatically on connect.

### What Gets Saved

| Category | Preferences |
| :--- | :--- |
| **Weapons** | AWP toggle, SSG08 toggle (full-buy), SSG08 half-buy toggle, AWP priority, pistol round primary, half-buy primary/secondary, full-buy primary/secondary |
| **Spawns** | Spawn menu toggle, T/CT preferred spawn per bombsite (A and B) |

### Configuration

| Field | Default | Description |
| :--- | :--- | :--- |
| `Preferences.UsePerTeamPreferences` | `true` | Separate T/CT weapon preferences in `!guns` |
| `Preferences.SpawnMenuEnabled` | `true` | Enable the CT spawn selection menu (`!spawns` and the toggle in `!retake`). When `false`, the feature is hidden everywhere and CTs are always auto-assigned spawns regardless of any saved preference |
| `Preferences.DatabaseConnectionName` | `"default"` | Cookies database connection name |

---

## Weapon Allocation

### Round Types

| Round Type | Armor | Primary | Secondary | CT Defuser |
| :--- | :--- | :--- | :--- | :--- |
| **Pistol** | Kevlar only (no helmet by default) | Pistol | ❌ | 1 random CT player |
| **Half-Buy** | Kevlar + Helmet | SMGs / budget rifles (or Scout) | ✅ Pistol | All CTs |
| **Full-Buy** | Kevlar + Helmet | Rifles (or AWP / Scout) | ✅ Pistol | All CTs |

### Round Type Selection

| Field | Default | Description |
| :--- | :--- | :--- |
| `Allocation.Enabled` | `true` | Enable/disable weapon allocation entirely |
| `Allocation.RoundType` | `"random"` | How each round's type is chosen (see modes below) |
| `Allocation.RoundTypePctPistol` | `20` | % chance of pistol round (random mode) |
| `Allocation.RoundTypePctHalf` | `30` | % chance of half-buy round (random mode) |
| `Allocation.RoundTypePctFull` | `50` | % chance of full-buy round (random mode) |
| `Allocation.RoundTypeSequence` | `[]` | Ordered sequence of round types (sequence mode) |

**`RoundType` modes:**

| Value | Description |
| :--- | :--- |
| `"random"` | Mix of all types based on the percentages above |
| `"pistol"` / `"p"` | Pistol rounds only |
| `"half"` / `"h"` | Half-buy rounds only |
| `"full"` / `"f"` | Full-buy rounds only |
| `"sequence"` | Plays through a defined sequence, looping on the last entry |

<details>
<summary><b>Sequence mode example</b></summary>

```json
"Allocation": {
  "RoundType": "sequence",
  "RoundTypeSequence": [
    { "Type": "Pistol",  "Count": 3  },
    { "Type": "HalfBuy", "Count": 2  },
    { "Type": "FullBuy", "Count": 25 }
  ]
}
```

Plays 3 pistol rounds → 2 half-buy rounds → full-buy for all remaining rounds.
</details>

### Weapon Selection

| Field | Default | Description |
| :--- | :--- | :--- |
| `Allocation.PistolHelmet` | `false` | Give helmet on pistol rounds (otherwise kevlar only) |
| `Allocation.InstantSwap` | `true` | Swap weapons in-hand immediately when a player changes preference mid-round |

Weapon selection priority (human players):
1. Player's saved preference (from `!guns`)
2. Configured default loadout (`Weapons.Defaults.*`)
3. Random weapon from the allowed list

Bots always receive a random weapon from the allowed list.

### Allowed Weapon Lists

Configured under `Weapons`:

| Field | Description |
| :--- | :--- |
| `Weapons.BuyMenuEnabled` | Show/hide the buy menu (disabling also zeroes player money) |
| `Weapons.Pistols` | List of weapons selectable on pistol rounds and as secondaries |
| `Weapons.HalfBuy.T` / `.Ct` / `.All` | Allowed primaries for half-buy rounds per team |
| `Weapons.FullBuy.T` / `.Ct` / `.All` | Allowed primaries for full-buy rounds per team |

### Default Loadouts

Used when a player has no saved preference yet. Player selections always override these.

```json
"Weapons": {
  "Defaults": {
    "Pistol": {
      "Primary": { "T": "weapon_glock", "Ct": "weapon_usp_silencer" },
      "Secondary": { "T": null, "Ct": null }
    },
    "HalfBuy": {
      "Primary": { "T": "weapon_galilar", "Ct": "weapon_famas" },
      "Secondary": { "T": "weapon_glock", "Ct": "weapon_usp_silencer" }
    },
    "FullBuy": {
      "Primary": { "T": "weapon_ak47", "Ct": "weapon_m4a1_silencer" },
      "Secondary": { "T": "weapon_glock", "Ct": "weapon_usp_silencer" }
    }
  }
}
```

### AWP Allocation

Only on full-buy rounds. Players must toggle `!awp` to be eligible.

| Field | Default | Description |
| :--- | :--- | :--- |
| `Allocation.AwpEnabled` | `true` | Enable AWP allocation |
| `Allocation.AwpPerTeam` | `1` | Maximum AWPs given per team |
| `Allocation.AwpAllowEveryone` | `false` | Ignore player preference — everyone is eligible |
| `Allocation.AwpLowPlayersThreshold` | `4` | Team size at or below which low-population mode activates |
| `Allocation.AwpLowPlayersChance` | `50` | % chance of AWP spawning in low-population mode |
| `Allocation.AwpLowPlayersVipChance` | `60` | % chance when a VIP-priority player is in the low-population team |
| `Allocation.AwpPriorityFlag` | `""` | Permission flag that grants AWP priority (empty = disabled) |
| `Allocation.AwpPriorityPct` | `0` | % chance each AWP slot picks a priority player first |

### Scout (SSG08) Allocation

Only on full-buy rounds. Players who receive an AWP are excluded.

| Field | Default | Description |
| :--- | :--- | :--- |
| `Allocation.Ssg08Enabled` | `true` | Enable SSG08 allocation |
| `Allocation.Ssg08PerTeam` | `0` | Maximum SSG08s given per team (0 = disabled) |
| `Allocation.Ssg08AllowEveryone` | `false` | Ignore player preference — everyone is eligible |

### Scout (SSG08) Half-Buy Allocation

Half-buy rounds only. Independent of the full-buy SSG08 preference — players toggle it separately in the `!retake` menu ("Play with SSG08 on half-buy"). Disabled by default; opt in by raising `Ssg08HalfPerTeam`.

| Field | Default | Description |
| :--- | :--- | :--- |
| `Allocation.Ssg08HalfEnabled` | `true` | Enable SSG08 allocation on half-buy rounds |
| `Allocation.Ssg08HalfPerTeam` | `0` | Maximum SSG08s given per team on half-buy (0 = disabled) |
| `Allocation.Ssg08HalfAllowEveryone` | `false` | Ignore player preference — everyone is eligible |

> `!scout` / `!ssg` / `!ssg08` toggle the SSG08 preference for **both** full-buy and half-buy at once (single switch, mirrors `!awp`). Use the `!retake` menu for per-round-type control.

### Weapon Stripping

| Field | Default | Description |
| :--- | :--- | :--- |
| `Allocation.StripWeapons` | `true` | Remove existing weapons before giving new loadout |
| `Allocation.StripRemove` | `true` | Remove weapons instead of dropping them (keeps the ground clean) |
| `Allocation.GivePistolOnRifleRounds` | `true` | Give secondary pistol on half/full-buy rounds |

---

## Grenade Allocation

Grenades are configured separately from primary weapons, with three distinct allocation modes.

### Allocation Modes

| Field | Default | Description |
| :--- | :--- | :--- |
| `Grenades.AllocationType` | `"random"` | `"random"`, `"fixed"`, or `"dynamic"` |

| Mode | Description |
| :--- | :--- |
| `"random"` | Each grenade in `RandomChances` is rolled independently per player with a configurable % chance |
| `"fixed"` | Every player on the team always receives exactly the grenades listed in `Fixed` |
| `"dynamic"` | A shared pool of grenades is distributed across the team, prioritising players who dealt the most damage last round |

### Per-Round Grenade Lists

Configured under `Grenades.Pistol`, `Grenades.HalfBuy`, and `Grenades.FullBuy`. Each has a `T` and `Ct` sub-object with three fields:

| Field | Used by mode | Description |
| :--- | :--- | :--- |
| `RandomChances` | `random` | Map of `"weapon_name": chance (0–100)`. Each entry rolled independently per player |
| `Fixed` | `fixed` | List of grenades every player on the team receives |
| `DynamicPool` | `dynamic` | Ordered shared pool distributed across eligible players. Duplicates = multiple of that type in the pool |

<details>
<summary><b>Example grenade config</b></summary>

```json
"Grenades": {
  "AllocationType": "dynamic",
  "FullBuy": {
    "T": {
      "RandomChances": { "weapon_smokegrenade": 100, "weapon_flashbang": 70, "weapon_molotov": 45 },
      "Fixed": [ "weapon_smokegrenade", "weapon_flashbang", "weapon_molotov" ],
      "DynamicPool": [ "weapon_smokegrenade", "weapon_flashbang", "weapon_flashbang", "weapon_hegrenade", "weapon_molotov" ]
    },
    "Ct": {
      "RandomChances": { "weapon_smokegrenade": 100, "weapon_flashbang": 70, "weapon_incgrenade": 45 },
      "Fixed": [ "weapon_smokegrenade", "weapon_flashbang", "weapon_incgrenade" ],
      "DynamicPool": [ "weapon_smokegrenade", "weapon_flashbang", "weapon_flashbang", "weapon_hegrenade", "weapon_incgrenade" ]
    }
  }
}
```
</details>

### Dynamic Mode Settings

| Field | Default | Description |
| :--- | :--- | :--- |
| `Grenades.DynamicMinDamage` | `1` | Minimum damage dealt last round for a player to be eligible |
| `Grenades.DynamicTopFraction` | `0.5` | Fraction (0.0–1.0) of eligible players who can receive grenades. Applied after sorting by damage, rounded up. `1.0` = all eligible |
| `Grenades.DynamicMaxPerPlayer` | `0` | Maximum total grenades one player can receive. `0` = no limit |
| `Grenades.DynamicUseCumulativeScore` | `false` | Use exponentially-weighted multi-round damage score for priority instead of raw last-round damage. The `DynamicMinDamage` threshold still uses last-round damage |

**How `DynamicTopFraction` works:**

| Eligible players | TopFraction | Players who receive grenades |
| :---: | :---: | :---: |
| 5 | 0.5 | 3 (top 3 by damage) |
| 4 | 0.5 | 2 |
| 1 | 0.5 | 1 (always at least 1) |

The `DynamicPool` is distributed round-robin starting from the highest-damage player. If a player reaches `DynamicMaxPerPlayer`, they are skipped and the grenade moves to the next eligible player.

### Per-Grenade Cap

Applies across **all** allocation modes.

| Field | Default | Description |
| :--- | :--- | :--- |
| `Grenades.MaxPerGrenade` | See below | Maximum of each grenade type one player can receive |

Default caps (reflecting CS2 inventory limits):

```json
"MaxPerGrenade": {
  "weapon_flashbang":   2,
  "weapon_smokegrenade": 1,
  "weapon_hegrenade":   1,
  "weapon_molotov":     1,
  "weapon_incgrenade":  1,
  "weapon_decoy":       1
}
```

Grenades not listed here are uncapped.

---

## Bomb

### Auto Plant

| Field | Default | Description |
| :--- | :--- | :--- |
| `Bomb.AutoPlant` | `true` | Automatically plant the bomb at freeze end |
| `Bomb.EnforceNoC4` | `true` | Prevent players from carrying C4 (bomb is always auto-planted) |

### Instant Plant & Defuse

| Field | Default | Description |
| :--- | :--- | :--- |
| `InstantBomb.InstaPlant` | `true` | Enable instant bomb plant |
| `InstantBomb.InstaDefuse` | `true` | Enable instant bomb defuse |
| `InstantBomb.BlockDefuseIfTAlive` | `true` | Prevent instant defuse while any T is alive |
| `InstantBomb.BlockDefuseIfMollyNear` | `true` | Prevent instant defuse while a molotov is burning near the bomb |
| `InstantBomb.MollyRadius` | `120` | Radius (units) in which a molotov blocks instant defuse |
| `InstantBomb.SuccessfulMessageTarget` | `"All"` | Who sees the defuse success message (`"All"`, `"Team"`, `"Player"`) |
| `InstantBomb.UnsuccessfulMessageTarget` | `"All"` | Who sees the defuse failure message (`"All"`, `"Team"`, `"Player"`) |

---

## Team Balance

| Field | Default | Description |
| :--- | :--- | :--- |
| `TeamBalance.Enabled` | `true` | Enable automatic team balancing |
| `TeamBalance.TerroristRatio` | `0.45` | Target fraction of players placed on T side |
| `TeamBalance.ForceEvenWhenPlayersMod10` | `true` | Force exactly 5v5 when player count is a multiple of 10 |
| `TeamBalance.IncludeBots` | `false` | Count bots when calculating team sizes |
| `TeamBalance.SkillBasedEnabled` | `true` | Balance teams by cumulative damage score instead of randomly |
| `TeamBalance.ScrambleEnabled` | `true` | Enable automatic team scramble after a losing streak |
| `TeamBalance.RoundsToScramble` | `5` | Number of consecutive T-side wins that triggers a scramble |

---

## Queue

| Field | Default | Description |
| :--- | :--- | :--- |
| `Queue.Enabled` | `true` | Enable the queue system |
| `Queue.MaxPlayers` | `9` | Maximum active players (others go to spectator queue) |
| `Queue.PreventTeamChangesMidRound` | `true` | Lock players to their team during a live round |
| `Queue.ForceEvenTeamsWhenPlayerCountIsMultipleOf10` | `true` | Force even teams when player count is a multiple of 10 |
| `Queue.QueuePriorityFlags` | `""` | Permission flag(s) that grant queue priority |
| `Queue.QueueImmunityFlags` | `""` | Permission flag(s) that exempt players from being queued |
| `Queue.ShouldRemoveSpectators` | `true` | Move spectators to queue when a slot opens |

---

## Smoke Scenarios

| Field | Default | Description |
| :--- | :--- | :--- |
| `SmokeScenarios.Enabled` | `false` | Enable smoke scenario spawning |
| `SmokeScenarios.RandomRoundsEnabled` | `true` | Only spawn smokes on randomly selected rounds |
| `SmokeScenarios.RandomRoundChance` | `0.25` | Probability (0.0–1.0) of smokes spawning when random rounds are enabled |

Smokes can also be forced via `!forcesmokes` / `!stopsmokes` (root permission).

---

## AFK Manager

| Field | Default | Description |
| :--- | :--- | :--- |
| `AfkManager.Enabled` | `false` | Enable AFK detection |
| `AfkManager.IdleSecondsBeforeSpectator` | `60` | Seconds of inactivity before moving player to spectator |
| `AfkManager.SpectatorSecondsBeforeKick` | `60` | Seconds in spectator (due to AFK) before kicking |
| `AfkManager.MovementDistanceThreshold` | `5.0` | Minimum distance moved per check interval to count as active |
| `AfkManager.CheckIntervalSeconds` | `2` | How often (seconds) AFK status is evaluated |
| `AfkManager.KickReason` | `"Kicked for being AFK"` | Message shown when kicking an AFK player |

---

## Anti Team-Flash

| Field | Default | Description |
| :--- | :--- | :--- |
| `AntiTeamFlash.Enabled` | `true` | Enable anti team-flash protection |
| `AntiTeamFlash.FlashOwner` | `false` | Whether the thrower can be blinded by their own flash |
| `AntiTeamFlash.AccessFlag` | `""` | Permission flag required to use the feature (empty = everyone) |

---

## Solo Bot

Spawns a bot to play against when only one human player is present.

| Field | Default | Description |
| :--- | :--- | :--- |
| `SoloBot.Enabled` | `false` | Enable the solo bot system |
| `SoloBot.Difficulty` | `2` | Bot difficulty level (0 = easiest, 3 = hardest) |

---

## World Breaker

Breaks destructible objects and opens doors at round start for better flow.

| Field | Default | Description |
| :--- | :--- | :--- |
| `Breaker.BreakBreakables` | `true` | Break glass and other destructible entities at round start |
| `Breaker.OpenDoors` | `false` | Open all doors at round start |

---

## Damage Report

| Field | Default | Description |
| :--- | :--- | :--- |
| `DamageReport.Enabled` | `true` | Show per-opponent damage summary in chat at round end |

Message format can be edited in `resources/translations/en.jsonc` (`damage.report.header`, `damage.report.line`).

---

## Announcements

| Field | Default | Description |
| :--- | :--- | :--- |
| `Announcement.bombsite-A-img` | *(URL)* | Image URL shown in the bombsite A announcement |
| `Announcement.bombsite-B-img` | *(URL)* | Image URL shown in the bombsite B announcement |

---

## Server

| Field | Default | Description |
| :--- | :--- | :--- |
| `Server.FreezeTimeSeconds` | `5` | Freeze time at the start of each round (seconds) |
| `Server.ChatPrefix` | `"Retakes \|"` | Prefix shown before all plugin chat messages |
| `Server.ChatPrefixColor` | `"green"` | Color of the chat prefix |
| `Server.DebugEnabled` | `false` | Enable verbose debug logging |

---

## Commands

### Admin

| Command | Permission | Description |
| :--- | :--- | :--- |
| `!forcesite <A/B>` | Root | Force all rounds to be played at a specific bombsite |
| `!forcestop` | Root | Clear the forced bombsite |
| `!forcesmokes` | Root | Force smoke scenarios to spawn every round |
| `!stopsmokes` | Root | Disable forced smokes |
| `!loadcfg <mapname>` | Root | Load a specific map configuration |
| `!listcfg` | Root | List all available map configurations |
| `!reloadcfg` | Root | Reload `config.json` |
| `!scramble` | Admin | Scramble teams on the next round |

### Spawn Editor (Root)

| Command | Description |
| :--- | :--- |
| `!editspawns [A/B]` | Enter spawn editing mode. Defaults to both sites |
| `!addspawn <T/CT> [planter] [A/B]` | Add a spawn at your current position |
| `!remove <id>` | Remove a spawn by ID |
| `!addsmoke <A/B> [name]` | Add a smoke scenario at your current position |
| `!removesmoke <id>` | Remove a smoke scenario by ID |
| `!namespawn <id> <name>` | Set a name for a spawn |
| `!gotospawn <id>` | Teleport to a spawn's position |
| `!replysmoke <id>` | Instantly replay a smoke scenario for testing |
| `!savespawns` | Save all changes to the map config file |
| `!stopediting` | Exit spawn editing mode and reload the map |

### Player

| Command | Description |
| :--- | :--- |
| `!guns` | Open the weapon preference menu |
| `!gun <weapon>` | Quickly set a preferred weapon (see below) |
| `!retake` | Open the main Retakes menu |
| `!spawns` | Toggle the CT spawn selection menu |
| `!awp` | Toggle AWP preference |
| `!voices` | Toggle voice announcements |

### Quick Weapon Select (`!gun`)

Type `!gun <weapon>` to change your weapon preference instantly — no menu needed. Swaps the weapon in-hand immediately.

**Accepted formats:**
- Entity names: `weapon_ak47`, `weapon_m4a1_silencer`
- Short names: `ak`, `ak47`, `deag`, `usp`, `m4a1s`, `scout`, `galil`, `p250`, `cz`, `r8`, etc.
- Display names: `AK-47`, `M4A1-S`, `USP-S`

**Behaviour:**
- Validates against the current round type
- Assigns to the correct slot (primary or secondary) automatically
- Reports if the weapon is not allowed this round or is already set

**Examples:**
```
!gun ak      → sets AK-47 as your full-buy/half-buy primary
!gun usp     → sets USP-S as your secondary
!gun m4a1s   → sets M4A1-S as your CT primary
```

### Debug

| Command | Description |
| :--- | :--- |
| `!debugqueues` | Print debug information about the queue state |

---

## Map Configs

Map configs live in:

```
plugins/Retakes/resources/maps/*.json
```

Each file contains the spawn positions used by the retakes allocator. A template is available at `resources/templates/template.jsonc`.

---

## Building

```bash
dotnet build
```

---

## Credits

- Readme template by [criskkky](https://github.com/criskkky)
- Release workflow based on [K4ryuu/K4-Guilds-SwiftlyS2](https://github.com/K4ryuu/K4-Guilds-SwiftlyS2/blob/main/.github/workflows/release.yml)
- All spawns based on [B3none/cs2-retakes](https://github.com/B3none/cs2-retakes)
- Inspired by [itsAudioo/CS2BombsiteAnnouncer](https://github.com/itsAudioo/CS2BombsiteAnnouncer)
- Inspired by [yonilerner/cs2-retakes-allocator](https://github.com/yonilerner/cs2-retakes-allocator)
