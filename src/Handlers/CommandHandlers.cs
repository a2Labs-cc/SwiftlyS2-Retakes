using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2_Retakes.Configuration;
using SwiftlyS2_Retakes.Constants;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SwiftlyS2_Retakes.Handlers;

public sealed class CommandHandlers
{
  private ISwiftlyCore? _core;

  private readonly IMapConfigService _mapConfig;
  private readonly ISpawnManager _spawnManager;
  private readonly IPawnLifecycleService _pawnLifecycle;
  private readonly ISpawnVisualizationService _spawnViz;
  private readonly IRetakesStateService _state;
  private readonly IPlayerPreferencesService _prefs;
  private readonly IRetakesConfigService _config;

  private readonly List<Guid> _commandGuids = new();

  public CommandHandlers(
    IMapConfigService mapConfig,
    ISpawnManager spawnManager,
    IPawnLifecycleService pawnLifecycle,
    ISpawnVisualizationService spawnViz,
    IRetakesStateService state,
    IPlayerPreferencesService prefs,
    IRetakesConfigService config
  )
  {
    _mapConfig = mapConfig;
    _spawnManager = spawnManager;
    _pawnLifecycle = pawnLifecycle;
    _spawnViz = spawnViz;
    _state = state;
    _prefs = prefs;
    _config = config;
  }

  public void Register(ISwiftlyCore core)
  {
    _core = core;

    // Team switching is controlled via EventPlayerTeam hook in PlayerEventHandlers.cs
    // Do NOT register jointeam/spectate commands as it blocks the native command execution

    _commandGuids.Add(core.Command.RegisterCommand("forcesite", ForceSite, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("forcestop", ForceStop, registerRaw: true, permission: RetakesPermissions.Root));

    _commandGuids.Add(core.Command.RegisterCommand("editspawns", EditSpawns, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("addspawn", AddSpawn, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("remove", RemoveSpawn, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("gotospawn", GoToSpawn, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("namespawn", NameSpawn, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("savespawns", SaveSpawns, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("stopediting", StopEditing, registerRaw: true, permission: RetakesPermissions.Root));

    _commandGuids.Add(core.Command.RegisterCommand("loadcfg", LoadCfg, registerRaw: true, permission: RetakesPermissions.Root));
    _commandGuids.Add(core.Command.RegisterCommand("listcfg", ListCfg, registerRaw: true, permission: RetakesPermissions.Root));

    _commandGuids.Add(core.Command.RegisterCommand("scramble", Scramble, registerRaw: true, permission: RetakesPermissions.Admin));
    _commandGuids.Add(core.Command.RegisterCommand("voices", Voices, registerRaw: true));

    _commandGuids.Add(core.Command.RegisterCommand("guns", Guns, registerRaw: true));
    _commandGuids.Add(core.Command.RegisterCommand("gun", Guns, registerRaw: true));
    _commandGuids.Add(core.Command.RegisterCommand("retake", Retake, registerRaw: true));
    _commandGuids.Add(core.Command.RegisterCommand("spawns", Spawns, registerRaw: true));
    _commandGuids.Add(core.Command.RegisterCommand("awp", Awp, registerRaw: true));
    _commandGuids.Add(core.Command.RegisterCommand("reloadcfg", ReloadCfg, registerRaw: true, permission: RetakesPermissions.Root));

    _commandGuids.Add(core.Command.RegisterCommand("debugqueues", DebugQueues, registerRaw: true));
  }

  public void Unregister(ISwiftlyCore core)
  {
    _spawnViz.HideSpawns();

    foreach (var id in _commandGuids)
    {
      core.Command.UnregisterCommand(id);
    }

    _commandGuids.Clear();
    _core = null;
  }

  private void ForceSite(ICommandContext context)
  {
    if (context.Args.Length < 1)
    {
      context.Reply("Usage: !forcesite <A/B>");
      return;
    }

    var arg = context.Args[0].Trim();
    if (arg.Equals("A", StringComparison.OrdinalIgnoreCase))
    {
      _state.ForceBombsite(Bombsite.A);
      context.Reply("Retakes: forced bombsite A");
      return;
    }

    if (arg.Equals("B", StringComparison.OrdinalIgnoreCase))
    {
      _state.ForceBombsite(Bombsite.B);
      context.Reply("Retakes: forced bombsite B");
      return;
    }

    context.Reply("Usage: !forcesite <A/B>");
  }

  private void ForceStop(ICommandContext context)
  {
    _state.ClearForcedBombsite();
    context.Reply("Retakes: forced bombsite cleared");
  }

  private void EditSpawns(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply("Retakes: plugin not ready");
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply("Usage: !editspawns <A/B>");
      return;
    }

    var arg = context.Args[0].Trim();
    Bombsite bombsite;
    if (arg.Equals("A", StringComparison.OrdinalIgnoreCase)) bombsite = Bombsite.A;
    else if (arg.Equals("B", StringComparison.OrdinalIgnoreCase)) bombsite = Bombsite.B;
    else
    {
      context.Reply("Usage: !editspawns <A/B>");
      return;
    }

    _state.SetShowingSpawnsForBombsite(bombsite);

    core.Engine.ExecuteCommand("mp_warmup_pausetimer 1");
    core.Engine.ExecuteCommand("mp_warmuptime 999999");
    core.Engine.ExecuteCommand("mp_warmup_start");

    core.Scheduler.DelayBySeconds(1.0f, () =>
    {
      if (_state.ShowingSpawnsForBombsite is null) return;
      _spawnViz.ShowSpawns(_mapConfig.Spawns, bombsite);
    });

    context.Reply($"Retakes: editing spawns for {bombsite}");
    context.Reply("Commands: !addspawn <T/CT> [planter], !remove <id>, !namespawn <id> <name>, !gotospawn <id>, !savespawns, !stopediting");
  }

  private void StopEditing(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply("Retakes: plugin not ready");
      return;
    }

    _state.SetShowingSpawnsForBombsite(null);
    _spawnViz.HideSpawns();

    core.Engine.ExecuteCommand("mp_warmup_pausetimer 0");
    core.Engine.ExecuteCommand("mp_warmup_end");

    // Reload from disk so external edits (e.g. spawn names) apply immediately without server restart.
    if (_mapConfig.LoadedMapName is not null && _mapConfig.Load(_mapConfig.LoadedMapName))
    {
      _spawnManager.SetSpawns(_mapConfig.Spawns);
    }

    context.Reply("Retakes: spawn editing stopped");
  }

  private void AddSpawn(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply("Retakes: plugin not ready");
      return;
    }

    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    var bombsite = _state.ShowingSpawnsForBombsite;
    if (bombsite is null)
    {
      context.Reply("Retakes: you must be in spawn editing mode (use !editspawns A/B)");
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply("Usage: !addspawn <T/CT> [planter]");
      return;
    }

    var teamArg = context.Args[0].Trim();
    Team team;
    if (teamArg.Equals("T", StringComparison.OrdinalIgnoreCase)) team = Team.T;
    else if (teamArg.Equals("CT", StringComparison.OrdinalIgnoreCase)) team = Team.CT;
    else
    {
      context.Reply("Usage: !addspawn <T/CT> [planter]");
      return;
    }

    var canBePlanter = context.Args.Length > 1 && 
      context.Args[1].Trim().Equals("planter", StringComparison.OrdinalIgnoreCase);

    if (canBePlanter && team != Team.T)
    {
      context.Reply("Retakes: only T spawns can be planter spawns");
      return;
    }

    var pawn = context.Sender.PlayerPawn;
    if (pawn is null)
    {
      context.Reply("Retakes: you must have a player pawn");
      return;
    }

    var position = pawn.CBodyComponent?.SceneNode?.AbsOrigin;
    var angles = pawn.EyeAngles;
    if (position is null)
    {
      context.Reply("Retakes: failed to read player position");
      return;
    }

    var newId = _mapConfig.AddSpawn(position.Value, angles, team, bombsite.Value, canBePlanter);

    // Refresh visualization
    _spawnViz.HideSpawns();
    core.Scheduler.DelayBySeconds(0.5f, () =>
    {
      if (_state.ShowingSpawnsForBombsite is null) return;
      _spawnViz.ShowSpawns(_mapConfig.Spawns, _state.ShowingSpawnsForBombsite.Value);
    });

    var planterText = canBePlanter ? " (planter)" : "";
    context.Reply($"Retakes: added spawn #{newId} for {team} at {bombsite}{planterText}");
    context.Reply("Use !savespawns to save changes to file");
  }

  private void RemoveSpawn(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply("Retakes: plugin not ready");
      return;
    }

    var bombsite = _state.ShowingSpawnsForBombsite;
    if (bombsite is null)
    {
      context.Reply("Retakes: you must be in spawn editing mode (use !editspawns A/B)");
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply("Usage: !remove <id>");
      return;
    }

    if (!int.TryParse(context.Args[0], out var id))
    {
      context.Reply("Usage: !remove <id>");
      return;
    }

    var spawn = _mapConfig.GetSpawnById(id);
    if (spawn is null)
    {
      context.Reply($"Retakes: spawn #{id} not found");
      return;
    }

    if (!_mapConfig.RemoveSpawn(id))
    {
      context.Reply($"Retakes: failed to remove spawn #{id}");
      return;
    }

    // Refresh visualization
    _spawnViz.HideSpawns();
    core.Scheduler.DelayBySeconds(0.5f, () =>
    {
      if (_state.ShowingSpawnsForBombsite is null) return;
      _spawnViz.ShowSpawns(_mapConfig.Spawns, _state.ShowingSpawnsForBombsite.Value);
    });

    context.Reply($"Retakes: removed spawn #{id}");
    context.Reply("Use !savespawns to save changes to file");
  }

  private void NameSpawn(ICommandContext context)
  {
    var bombsite = _state.ShowingSpawnsForBombsite;
    if (bombsite is null)
    {
      context.Reply("Retakes: you must be in spawn editing mode (use !editspawns A/B)");
      return;
    }

    if (context.Args.Length < 2)
    {
      context.Reply("Usage: !namespawn <id> <name>");
      return;
    }

    if (!int.TryParse(context.Args[0], out var id))
    {
      context.Reply("Usage: !namespawn <id> <name>");
      return;
    }

    var name = string.Join(" ", context.Args.Skip(1));
    if (string.IsNullOrWhiteSpace(name))
    {
      context.Reply("Usage: !namespawn <id> <name>");
      return;
    }

    var spawn = _mapConfig.GetSpawnById(id);
    if (spawn is null)
    {
      context.Reply($"Retakes: spawn #{id} not found");
      return;
    }

    if (!_mapConfig.SetSpawnName(id, name))
    {
      context.Reply($"Retakes: failed to set name for spawn #{id}");
      return;
    }

    context.Reply($"Retakes: spawn #{id} named '{name}'");
    context.Reply("Use !savespawns to save changes to file");
  }

  private void SaveSpawns(ICommandContext context)
  {
    if (_mapConfig.LoadedMapName is null)
    {
      context.Reply("Retakes: no map config loaded");
      return;
    }

    if (_mapConfig.Save())
    {
      _spawnManager.SetSpawns(_mapConfig.Spawns);
      context.Reply($"Retakes: saved {_mapConfig.Spawns.Count} spawns to {_mapConfig.LoadedMapName}.json");
    }
    else
    {
      context.Reply("Retakes: failed to save spawns");
    }
  }

  private void GoToSpawn(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply("Usage: !gotospawn <id>");
      return;
    }

    if (!int.TryParse(context.Args[0], out var id))
    {
      context.Reply("Usage: !gotospawn <id>");
      return;
    }

    var spawn = _mapConfig.Spawns.FirstOrDefault(s => s.Id == id);
    if (spawn is null)
    {
      context.Reply($"Retakes: spawn id {id} not found");
      return;
    }

    _pawnLifecycle.WhenPawnReady(context.Sender, p => p.Teleport(spawn.Position, spawn.Angle, Vector.Zero));
    context.Reply($"Retakes: teleporting to spawn {id}");
  }

  private void LoadCfg(ICommandContext context)
  {
    if (context.Args.Length < 1)
    {
      context.Reply("Usage: !loadcfg <mapname>");
      return;
    }

    var mapName = context.Args[0].Trim();
    if (string.IsNullOrWhiteSpace(mapName))
    {
      context.Reply("Usage: !loadcfg <mapname>");
      return;
    }

    var ok = _mapConfig.Load(mapName);
    if (!ok)
    {
      context.Reply($"Retakes: failed to load map config {mapName}");
      return;
    }

    _spawnManager.SetSpawns(_mapConfig.Spawns);
    context.Reply($"Retakes: loaded {mapName} ({_mapConfig.Spawns.Count} spawns)");
  }

  private void ListCfg(ICommandContext context)
  {
    try
    {
      var core = _core;
      if (core is null)
      {
        context.Reply("Retakes: plugin not ready");
        return;
      }

      var mapsDir = Path.Combine(core.PluginPath, "resources", "maps");
      if (!Directory.Exists(mapsDir))
      {
        context.Reply("Retakes: resources/maps directory not found");
        return;
      }

      var files = Directory.GetFiles(mapsDir, "*.json", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileNameWithoutExtension)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .OrderBy(x => x)
        .ToList();

      if (files.Count == 0)
      {
        context.Reply("Retakes: no map configs found");
        return;
      }

      context.Reply("Retakes cfg: " + string.Join(", ", files));
    }
    catch
    {
      context.Reply("Retakes: failed to list configs");
    }
  }

  private void Scramble(ICommandContext context)
  {
    _state.ScrambleNextRound = true;
    context.Reply("Retakes: teams will scramble next round");
  }

  private void Voices(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    var enabled = _state.ToggleVoices(context.Sender.SteamID);
    context.Reply(enabled ? "Retakes: voice announcements enabled" : "Retakes: voice announcements disabled");
  }

  private void Guns(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply("Retakes: plugin not ready");
      return;
    }

    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    OpenGunsMenu(core, context.Sender);
  }

  private void OpenGunsMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player)
  {
    var weapons = _config.Config.Weapons;

    var hasPistols = weapons.Pistols.Count > 0;
    var hasHalfBuy = weapons.HalfBuy.All.Count > 0 || weapons.HalfBuy.T.Count > 0 || weapons.HalfBuy.Ct.Count > 0;
    var hasFullBuy = weapons.FullBuy.All.Count > 0 || weapons.FullBuy.T.Count > 0 || weapons.FullBuy.Ct.Count > 0;

    var builder = core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle("Weapon Preferences")
      .EnableSound();

    if (hasFullBuy)
    {
      builder.AddOption(new SubmenuMenuOption("FullBuy", () => BuildRoundPackMenu(core, player, RoundType.FullBuy)));
    }

    if (hasHalfBuy)
    {
      builder.AddOption(new SubmenuMenuOption("HalfBuy", () => BuildRoundPackMenu(core, player, RoundType.HalfBuy)));
    }

    if (hasPistols)
    {
      builder.AddOption(new SubmenuMenuOption("Pistols", () => BuildPistolMenu(core, player)));
    }

    core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
  }

  private void Retake(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply("Retakes: plugin not ready");
      return;
    }

    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    OpenRetakeMenu(core, context.Sender);
  }

  private void Spawns(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    var enabled = _prefs.ToggleSpawnMenu(context.Sender.SteamID);
    context.Reply(enabled ? "Retakes: Spawn Menu enabled" : "Retakes: Spawn Menu disabled");
  }

  private void OpenRetakeMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player)
  {
    var builder = core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle("Retake")
      .EnableSound();

    var spawnMenuEnabled = _prefs.WantsSpawnMenu(player.SteamID);
    var spawnMenuText = spawnMenuEnabled ? "Spawn Menu: ON" : "Spawn Menu: OFF";
    var spawnMenuToggle = new ButtonMenuOption(spawnMenuText);
    spawnMenuToggle.Click += async (_, args) =>
    {
      _prefs.ToggleSpawnMenu(args.Player.SteamID);
      OpenRetakeMenu(core, args.Player);
      await ValueTask.CompletedTask;
    };
    builder.AddOption(spawnMenuToggle);

    var awpEnabled = _prefs.WantsAwp(player.SteamID);
    var awpToggleText = awpEnabled ? "Play with AWP: ON" : "Play with AWP: OFF";
    var awpToggle = new ButtonMenuOption(awpToggleText);
    awpToggle.Click += async (_, args) =>
    {
      _prefs.ToggleAwp(args.Player.SteamID);
      OpenRetakeMenu(core, args.Player);
      await ValueTask.CompletedTask;
    };
    builder.AddOption(awpToggle);

    var ssgEnabled = _prefs.WantsSsg08(player.SteamID);
    var ssgToggleText = ssgEnabled ? "Play with SSG08: ON" : "Play with SSG08: OFF";
    var ssgToggle = new ButtonMenuOption(ssgToggleText);
    ssgToggle.Click += async (_, args) =>
    {
      _prefs.ToggleSsg08(args.Player.SteamID);
      OpenRetakeMenu(core, args.Player);
      await ValueTask.CompletedTask;
    };
    builder.AddOption(ssgToggle);

    var requiredFlag = (_config.Config.Allocation.AwpPriorityFlag ?? string.Empty).Trim();
    var pct = Math.Clamp(_config.Config.Allocation.AwpPriorityPct, 0, 100);
    if (!string.IsNullOrWhiteSpace(requiredFlag) && pct > 0)
    {
      var hasPerm = false;
      try
      {
        hasPerm = core.Permission.PlayerHasPermission(player.SteamID, requiredFlag);
      }
      catch
      {
        hasPerm = false;
      }

      if (hasPerm)
      {
        var prioEnabled = _prefs.WantsAwpPriority(player.SteamID);
        var prioText = prioEnabled ? $"AWP priority ({pct}%): ON" : $"AWP priority ({pct}%): OFF";
        var prioToggle = new ButtonMenuOption(prioText);
        prioToggle.Click += async (_, args) =>
        {
          _prefs.ToggleAwpPriority(args.Player.SteamID);
          OpenRetakeMenu(core, args.Player);
          await ValueTask.CompletedTask;
        };
        builder.AddOption(prioToggle);
      }
    }

    core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
  }

  private IMenuAPI BuildSpawnSelectionMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player)
  {
    var tMenu = new SubmenuMenuOption("T spawns", () => BuildSpawnTeamMenu(core, player, isCt: false));
    var ctMenu = new SubmenuMenuOption("CT spawns", () => BuildSpawnTeamMenu(core, player, isCt: true));

    return core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle("Spawn selection")
      .EnableSound()
      .AddOption(tMenu)
      .AddOption(ctMenu)
      .Build();
  }

  private IMenuAPI BuildSpawnTeamMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player, bool isCt)
  {
    var teamName = isCt ? "CT" : "T";
    var a = new SubmenuMenuOption("Bombsite A", () => BuildSpawnListMenu(core, player, isCt, Bombsite.A));
    var b = new SubmenuMenuOption("Bombsite B", () => BuildSpawnListMenu(core, player, isCt, Bombsite.B));

    return core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle($"{teamName} spawn selection")
      .EnableSound()
      .AddOption(a)
      .AddOption(b)
      .Build();
  }

  private IMenuAPI BuildSpawnListMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player, bool isCt, Bombsite bombsite)
  {
    var team = isCt ? Team.CT : Team.T;
    var spawns = _mapConfig.Spawns
      .Where(s => s.Team == team && s.Bombsite == bombsite)
      .OrderBy(s => s.Id)
      .ToList();

    var selected = _prefs.GetPreferredSpawn(player.SteamID, isCt, bombsite);
    var selectedText = selected.HasValue ? selected.Value.ToString() : "Random";

    var builder = core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle($"{team} {bombsite} (Selected: {selectedText})")
      .EnableSound();

    foreach (var s in spawns)
    {
      var label = string.IsNullOrWhiteSpace(s.Name) ? $"#{s.Id}" : $"#{s.Id} - {s.Name}";
      var opt = new ButtonMenuOption(label);
      opt.Click += async (_, args) =>
      {
        _prefs.SetPreferredSpawn(args.Player.SteamID, isCt, bombsite, s.Id);
        core.MenusAPI.OpenMenuForPlayer(args.Player, BuildSpawnListMenu(core, args.Player, isCt, bombsite));
        await ValueTask.CompletedTask;
      };
      builder.AddOption(opt);
    }

    var clear = new ButtonMenuOption("Clear (random)");
    clear.Click += async (_, args) =>
    {
      _prefs.SetPreferredSpawn(args.Player.SteamID, isCt, bombsite, null);
      core.MenusAPI.OpenMenuForPlayer(args.Player, BuildSpawnListMenu(core, args.Player, isCt, bombsite));
      await ValueTask.CompletedTask;
    };
    builder.AddOption(clear);

    return builder.Build();
  }

  private IMenuAPI BuildPistolMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player)
  {
    var isCt = (Team)player.Controller.TeamNum == Team.CT;
    var selected = _prefs.GetPistolPrimary(player.SteamID, isCt);
    var selectedText = WeaponOrRandom(selected);

    var builder = core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle($"Pistols: Primary {selectedText}")
      .EnableSound();

    foreach (var w in _config.Config.Weapons.Pistols)
    {
      var opt = new ButtonMenuOption(WeaponDisplayName(w));
      opt.Click += async (_, args) =>
      {
        var ct = (Team)args.Player.Controller.TeamNum == Team.CT;
        _prefs.SetPistolPrimary(args.Player.SteamID, ct, w);
        core.MenusAPI.OpenMenuForPlayer(args.Player, BuildPistolMenu(core, args.Player));
        await ValueTask.CompletedTask;
      };
      builder.AddOption(opt);
    }

    var clear = new ButtonMenuOption("Clear (random)");
    clear.Click += async (_, args) =>
    {
      var ct = (Team)args.Player.Controller.TeamNum == Team.CT;
      _prefs.SetPistolPrimary(args.Player.SteamID, ct, null);
      core.MenusAPI.OpenMenuForPlayer(args.Player, BuildPistolMenu(core, args.Player));
      await ValueTask.CompletedTask;
    };
    builder.AddOption(clear);

    return builder.Build();
  }

  private IMenuAPI BuildRoundPackMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player, RoundType roundType)
  {
    var isCt = (Team)player.Controller.TeamNum == Team.CT;
    var pack = roundType == RoundType.FullBuy
      ? _prefs.GetFullBuyPack(player.SteamID, isCt)
      : _prefs.GetHalfBuyPack(player.SteamID, isCt);

    var primaryText = WeaponOrRandom(pack.Primary);
    var secondaryText = WeaponOrRandom(pack.Secondary);

    var primary = new SubmenuMenuOption($"Primary: {primaryText}", () => BuildPackSlotMenu(core, player, roundType, isPrimary: true));
    var secondary = new SubmenuMenuOption($"Secondary: {secondaryText}", () => BuildPackSlotMenu(core, player, roundType, isPrimary: false));

    return core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle(PackTitle(roundType, pack.Primary, pack.Secondary))
      .EnableSound()
      .AddOption(primary)
      .AddOption(secondary)
      .Build();
  }

  private IMenuAPI BuildPackSlotMenu(ISwiftlyCore core, SwiftlyS2.Shared.Players.IPlayer player, RoundType roundType, bool isPrimary)
  {
    var isCt = (Team)player.Controller.TeamNum == Team.CT;
    var title = isPrimary ? "Primary" : "Secondary";

    var list = GetAllowedWeaponsForMenu(roundType, isCt, isPrimary);

    var builder = core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle($"{roundType} {title}")
      .EnableSound();

    foreach (var w in list)
    {
      var opt = new ButtonMenuOption(WeaponDisplayName(w));
      opt.Click += async (_, args) =>
      {
        var ct = (Team)args.Player.Controller.TeamNum == Team.CT;
        if (roundType == RoundType.FullBuy)
        {
          if (isPrimary) _prefs.SetFullBuyPrimary(args.Player.SteamID, ct, w);
          else _prefs.SetFullBuySecondary(args.Player.SteamID, ct, w);
        }
        else
        {
          if (isPrimary) _prefs.SetHalfBuyPrimary(args.Player.SteamID, ct, w);
          else _prefs.SetHalfBuySecondary(args.Player.SteamID, ct, w);
        }

        core.MenusAPI.OpenMenuForPlayer(args.Player, BuildRoundPackMenu(core, args.Player, roundType));
        await ValueTask.CompletedTask;
      };
      builder.AddOption(opt);
    }

    var clear = new ButtonMenuOption("Clear (random)");
    clear.Click += async (_, args) =>
    {
      var ct = (Team)args.Player.Controller.TeamNum == Team.CT;
      if (roundType == RoundType.FullBuy)
      {
        if (isPrimary) _prefs.SetFullBuyPrimary(args.Player.SteamID, ct, null);
        else _prefs.SetFullBuySecondary(args.Player.SteamID, ct, null);
      }
      else
      {
        if (isPrimary) _prefs.SetHalfBuyPrimary(args.Player.SteamID, ct, null);
        else _prefs.SetHalfBuySecondary(args.Player.SteamID, ct, null);
      }

      core.MenusAPI.OpenMenuForPlayer(args.Player, BuildRoundPackMenu(core, args.Player, roundType));
      await ValueTask.CompletedTask;
    };
    builder.AddOption(clear);

    return builder.Build();
  }

  private List<string> GetAllowedWeaponsForMenu(RoundType roundType, bool isCt, bool isPrimary)
  {
    // Secondary always uses shared pistols list
    if (!isPrimary)
    {
      return _config.Config.Weapons.Pistols
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    if (roundType == RoundType.Pistol)
    {
      return _config.Config.Weapons.Pistols
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    // Primary weapons per round type
    RoundWeaponsConfig? roundCfg = roundType switch
    {
      RoundType.FullBuy => _config.Config.Weapons.FullBuy,
      RoundType.HalfBuy => _config.Config.Weapons.HalfBuy,
      _ => null,
    };

    if (roundCfg is null) return new List<string>();

    var result = new HashSet<string>(roundCfg.All, StringComparer.OrdinalIgnoreCase);
    var teamList = isCt ? roundCfg.Ct : roundCfg.T;
    foreach (var w in teamList)
    {
      result.Add(w);
    }

    return result.OrderBy(w => w, StringComparer.OrdinalIgnoreCase).ToList();
  }

  private static readonly Dictionary<string, string> WeaponNameOverrides = new(StringComparer.OrdinalIgnoreCase)
  {
    ["weapon_ak47"] = "AK-47",
    ["weapon_m4a1"] = "M4A4",
    ["weapon_m4a1_silencer"] = "M4A1-S",
    ["weapon_awp"] = "AWP",
    ["weapon_ssg08"] = "SSG 08",
    ["weapon_aug"] = "AUG",
    ["weapon_sg556"] = "SG 553",
    ["weapon_famas"] = "FAMAS",
    ["weapon_galilar"] = "Galil AR",
    ["weapon_mac10"] = "MAC-10",
    ["weapon_mp9"] = "MP9",
    ["weapon_mp7"] = "MP7",
    ["weapon_mp5sd"] = "MP5-SD",
    ["weapon_ump45"] = "UMP-45",
    ["weapon_p90"] = "P90",
    ["weapon_bizon"] = "PP-Bizon",
    ["weapon_xm1014"] = "XM1014",
    ["weapon_nova"] = "Nova",
    ["weapon_sawedoff"] = "Sawed-Off",
    ["weapon_mag7"] = "MAG-7",
    ["weapon_negev"] = "Negev",
    ["weapon_m249"] = "M249",
    ["weapon_glock"] = "Glock-18",
    ["weapon_hkp2000"] = "P2000",
    ["weapon_usp_silencer"] = "USP-S",
    ["weapon_p250"] = "P250",
    ["weapon_fiveseven"] = "Five-SeveN",
    ["weapon_tec9"] = "Tec-9",
    ["weapon_cz75a"] = "CZ75-Auto",
    ["weapon_deagle"] = "Desert Eagle",
    ["weapon_revolver"] = "R8 Revolver",
    ["weapon_elite"] = "Dual Berettas",
  };

  private static string WeaponOrRandom(string? weapon)
  {
    if (string.IsNullOrWhiteSpace(weapon)) return "(random)";
    return WeaponDisplayName(weapon);
  }

  private static string PackSummary(string label, string? primary, string? secondary)
  {
    if (string.IsNullOrWhiteSpace(primary) && string.IsNullOrWhiteSpace(secondary))
      return $"{label}: (random)";

    return $"{label}: {WeaponOrRandom(primary)} + {WeaponOrRandom(secondary)}";
  }

  private static string PackTitle(RoundType roundType, string? primary, string? secondary)
  {
    if (string.IsNullOrWhiteSpace(primary) && string.IsNullOrWhiteSpace(secondary))
      return $"{roundType}: (random)";

    return $"{roundType}: {WeaponOrRandom(primary)} + {WeaponOrRandom(secondary)}";
  }

  private static string WeaponDisplayName(string weapon)
  {
    if (WeaponNameOverrides.TryGetValue(weapon, out var known)) return known;

    var s = weapon;
    if (s.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)) s = s[7..];
    s = s.Replace('_', ' ');

    // Best-effort title casing while keeping digits. e.g. "scar20" -> "Scar20".
    // Known weird cases should be added to WeaponNameOverrides.
    return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..]));
  }

  private void Awp(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply("Retakes: this command must be run by a player");
      return;
    }

    var enabled = _prefs.ToggleAwp(context.Sender.SteamID);
    context.Reply(enabled ? "Retakes: AWP preference enabled" : "Retakes: AWP preference disabled");
  }

  private void ReloadCfg(ICommandContext context)
  {
    try
    {
      _config.LoadOrCreate();
      _config.ApplyToConvars(restartGame: true);
      context.Reply("Retakes: reloaded config.json");
    }
    catch
    {
      context.Reply("Retakes: failed to reload config.json");
    }
  }

  private void DebugQueues(ICommandContext context)
  {
    context.Reply(_pawnLifecycle.DebugSummary());
  }
}
