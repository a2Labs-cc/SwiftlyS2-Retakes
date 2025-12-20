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

  private string Tr(ICommandContext context, string key, params object[] args)
  {
    var core = _core;
    if (core is null) return key;

    IPlayer? player = null;
    if (context.IsSentByPlayer && context.Sender is not null)
    {
      player = context.Sender;
    }
    else
    {
      player = core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p is not null && p.IsValid);
    }

    if (player is null) return key;

    var loc = core.Translation.GetPlayerLocalizer(player);
    var result = args.Length == 0 ? loc[key] : loc[key, args];
    return result.Colored();
  }

  private void ForceSite(ICommandContext context)
  {
    if (context.Args.Length < 1)
    {
      context.Reply(Tr(context, "command.forcesite.usage"));
      return;
    }

    var arg = context.Args[0].Trim();
    if (arg.Equals("A", StringComparison.OrdinalIgnoreCase))
    {
      _state.ForceBombsite(Bombsite.A);
      context.Reply(Tr(context, "command.forcesite.forced_a"));
      return;
    }

    if (arg.Equals("B", StringComparison.OrdinalIgnoreCase))
    {
      _state.ForceBombsite(Bombsite.B);
      context.Reply(Tr(context, "command.forcesite.forced_b"));
      return;
    }

    context.Reply(Tr(context, "command.forcesite.usage"));
  }

  private void ForceStop(ICommandContext context)
  {
    _state.ClearForcedBombsite();
    context.Reply(Tr(context, "command.forcestop.cleared"));
  }

  private void EditSpawns(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply(Tr(context, "error.plugin_not_ready"));
      return;
    }

    Bombsite bombsite;
    if (context.Args.Length == 0)
    {
      bombsite = Bombsite.Both;
    }
    else
    {
      var arg = context.Args[0].Trim();
      if (arg.Equals("A", StringComparison.OrdinalIgnoreCase)) bombsite = Bombsite.A;
      else if (arg.Equals("B", StringComparison.OrdinalIgnoreCase)) bombsite = Bombsite.B;
      else if (arg.Equals("Both", StringComparison.OrdinalIgnoreCase)) bombsite = Bombsite.Both;
      else
      {
        context.Reply(Tr(context, "command.editspawns.usage"));
        return;
      }
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

    context.Reply(Tr(context, "command.editspawns.start", bombsite));
    context.Reply(Tr(context, "command.editspawns.commands"));
  }

  private void StopEditing(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply(Tr(context, "error.plugin_not_ready"));
      return;
    }

    _state.SetShowingSpawnsForBombsite(null);
    _spawnViz.HideSpawns();

    core.Engine.ExecuteCommand("mp_warmup_pausetimer 0");
    core.Engine.ExecuteCommand("mp_warmup_end");

    // Ensure config is re-applied after leaving edit mode (warmup end)
    core.Scheduler.DelayBySeconds(1.0f, () =>
    {
        _config.ApplyToConvars(false);
    });

    // Reload from disk so external edits (e.g. spawn names) apply immediately without server restart.
    if (_mapConfig.LoadedMapName is not null && _mapConfig.Load(_mapConfig.LoadedMapName))
    {
      _spawnManager.SetSpawns(_mapConfig.Spawns);
    }

    context.Reply(Tr(context, "command.stopediting.stopped"));
  }

  private void AddSpawn(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply(Tr(context, "error.plugin_not_ready"));
      return;
    }

    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply(Tr(context, "error.must_be_player"));
      return;
    }

    var bombsiteState = _state.ShowingSpawnsForBombsite;
    if (bombsiteState is null)
    {
      context.Reply(Tr(context, "error.must_be_in_spawn_edit_mode"));
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply(Tr(context, "command.addspawn.usage"));
      return;
    }

    var teamArg = context.Args[0].Trim();
    Team team;
    if (teamArg.Equals("T", StringComparison.OrdinalIgnoreCase)) team = Team.T;
    else if (teamArg.Equals("CT", StringComparison.OrdinalIgnoreCase)) team = Team.CT;
    else
    {
      context.Reply(Tr(context, "command.addspawn.usage"));
      return;
    }

    // Parse optional args
    var canBePlanter = false;
    Bombsite? specifiedSite = null;

    for (var i = 1; i < context.Args.Length; i++)
    {
      var arg = context.Args[i].Trim();
      if (arg.Equals("planter", StringComparison.OrdinalIgnoreCase))
      {
        canBePlanter = true;
      }
      else if (arg.Equals("A", StringComparison.OrdinalIgnoreCase))
      {
        specifiedSite = Bombsite.A;
      }
      else if (arg.Equals("B", StringComparison.OrdinalIgnoreCase))
      {
        specifiedSite = Bombsite.B;
      }
    }

    Bombsite finalBombsite;
    if (bombsiteState.Value == Bombsite.Both)
    {
      if (specifiedSite is null)
      {
        context.Reply(Tr(context, "command.addspawn.both_mode_needs_site"));
        return;
      }
      finalBombsite = specifiedSite.Value;
    }
    else
    {
      finalBombsite = bombsiteState.Value;
      // If user specified a site that contradicts the current view, maybe warn? 
      // For now, let's just ignore it or assume they know what they are doing if it matches. 
      // If they are in A and type B, we could error, but let's stick to the view mode restriction for safety/simplicity 
      // or just allow the view mode to dictate.
      // Actually, if they are in A and type B, it might be confusing if it adds to A.
      // Let's enforce that if they specify a site, it must match the view (unless view is Both).
      if (specifiedSite is not null && specifiedSite.Value != finalBombsite)
      {
        context.Reply(Tr(context, "command.addspawn.site_mismatch", finalBombsite));
        return;
      }
    }

    if (canBePlanter && team != Team.T)
    {
      context.Reply(Tr(context, "command.addspawn.only_t_planter"));
      return;
    }

    var pawn = context.Sender.PlayerPawn;
    if (pawn is null)
    {
      context.Reply(Tr(context, "command.addspawn.must_have_pawn"));
      return;
    }

    var position = pawn.CBodyComponent?.SceneNode?.AbsOrigin;
    var angles = pawn.EyeAngles;
    if (position is null)
    {
      context.Reply(Tr(context, "command.addspawn.read_pos_failed"));
      return;
    }

    var newId = _mapConfig.AddSpawn(position.Value, angles, team, finalBombsite, canBePlanter);

    // Refresh visualization
    _spawnViz.HideSpawns();
    core.Scheduler.DelayBySeconds(0.5f, () =>
    {
      if (_state.ShowingSpawnsForBombsite is null) return;
      _spawnViz.ShowSpawns(_mapConfig.Spawns, _state.ShowingSpawnsForBombsite.Value);
    });

    var planterText = canBePlanter ? " (planter)" : "";
    context.Reply(Tr(context, "command.addspawn.added", newId, team, finalBombsite, planterText));
    context.Reply(Tr(context, "spawns.save_hint"));
  }

  private void RemoveSpawn(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply(Tr(context, "error.plugin_not_ready"));
      return;
    }

    var bombsite = _state.ShowingSpawnsForBombsite;
    if (bombsite is null)
    {
      context.Reply(Tr(context, "error.must_be_in_spawn_edit_mode"));
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply(Tr(context, "command.remove.usage"));
      return;
    }

    if (!int.TryParse(context.Args[0], out var id))
    {
      context.Reply(Tr(context, "command.remove.usage"));
      return;
    }

    var spawn = _mapConfig.GetSpawnById(id);
    if (spawn is null)
    {
      context.Reply(Tr(context, "command.remove.spawn_not_found", id));
      return;
    }

    if (!_mapConfig.RemoveSpawn(id))
    {
      context.Reply(Tr(context, "command.remove.failed", id));
      return;
    }

    // Refresh visualization
    _spawnViz.HideSpawns();
    core.Scheduler.DelayBySeconds(0.5f, () =>
    {
      if (_state.ShowingSpawnsForBombsite is null) return;
      _spawnViz.ShowSpawns(_mapConfig.Spawns, _state.ShowingSpawnsForBombsite.Value);
    });

    context.Reply(Tr(context, "command.remove.removed", id));
    context.Reply(Tr(context, "spawns.save_hint"));
  }

  private void NameSpawn(ICommandContext context)
  {
    var bombsite = _state.ShowingSpawnsForBombsite;
    if (bombsite is null)
    {
      context.Reply(Tr(context, "error.must_be_in_spawn_edit_mode"));
      return;
    }

    if (context.Args.Length < 2)
    {
      context.Reply(Tr(context, "command.namespawn.usage"));
      return;
    }

    if (!int.TryParse(context.Args[0], out var id))
    {
      context.Reply(Tr(context, "command.namespawn.usage"));
      return;
    }

    var name = string.Join(" ", context.Args.Skip(1));
    if (string.IsNullOrWhiteSpace(name))
    {
      context.Reply(Tr(context, "command.namespawn.usage"));
      return;
    }

    var spawn = _mapConfig.GetSpawnById(id);
    if (spawn is null)
    {
      context.Reply(Tr(context, "command.remove.spawn_not_found", id));
      return;
    }

    if (!_mapConfig.SetSpawnName(id, name))
    {
      context.Reply(Tr(context, "command.namespawn.failed_set", id));
      return;
    }

    context.Reply(Tr(context, "command.namespawn.named", id, name));
    context.Reply(Tr(context, "spawns.save_hint"));
  }

  private void SaveSpawns(ICommandContext context)
  {
    if (_mapConfig.LoadedMapName is null)
    {
      context.Reply(Tr(context, "command.savespawns.no_map_config"));
      return;
    }

    if (_mapConfig.Save())
    {
      _spawnManager.SetSpawns(_mapConfig.Spawns);
      context.Reply(Tr(context, "command.savespawns.saved", _mapConfig.Spawns.Count, _mapConfig.LoadedMapName));
    }
    else
    {
      context.Reply(Tr(context, "command.savespawns.failed"));
    }
  }

  private void GoToSpawn(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply(Tr(context, "error.must_be_player"));
      return;
    }

    if (context.Args.Length < 1)
    {
      context.Reply(Tr(context, "command.gotospawn.usage"));
      return;
    }

    if (!int.TryParse(context.Args[0], out var id))
    {
      context.Reply(Tr(context, "command.gotospawn.usage"));
      return;
    }

    var spawn = _mapConfig.Spawns.FirstOrDefault(s => s.Id == id);
    if (spawn is null)
    {
      context.Reply(Tr(context, "command.gotospawn.not_found", id));
      return;
    }

    _pawnLifecycle.WhenPawnReady(context.Sender, p => p.Teleport(spawn.Position, spawn.Angle, Vector.Zero));
    context.Reply(Tr(context, "command.gotospawn.teleporting", id));
  }

  private void LoadCfg(ICommandContext context)
  {
    if (context.Args.Length < 1)
    {
      context.Reply(Tr(context, "command.loadcfg.usage"));
      return;
    }

    var mapName = context.Args[0].Trim();
    if (string.IsNullOrWhiteSpace(mapName))
    {
      context.Reply(Tr(context, "command.loadcfg.usage"));
      return;
    }

    var ok = _mapConfig.Load(mapName);
    if (!ok)
    {
      context.Reply(Tr(context, "command.loadcfg.failed", mapName));
      return;
    }

    _spawnManager.SetSpawns(_mapConfig.Spawns);
    context.Reply(Tr(context, "command.loadcfg.loaded", mapName, _mapConfig.Spawns.Count));
  }

  private void ListCfg(ICommandContext context)
  {
    try
    {
      var core = _core;
      if (core is null)
      {
        context.Reply(Tr(context, "error.plugin_not_ready"));
        return;
      }

      var mapsDir = Path.Combine(core.PluginPath, "resources", "maps");
      if (!Directory.Exists(mapsDir))
      {
        context.Reply(Tr(context, "command.listcfg.maps_dir_not_found"));
        return;
      }

      var files = Directory.GetFiles(mapsDir, "*.json", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileNameWithoutExtension)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .OrderBy(x => x)
        .ToList();

      if (files.Count == 0)
      {
        context.Reply(Tr(context, "command.listcfg.none_found"));
        return;
      }

      context.Reply(Tr(context, "command.listcfg.list", string.Join(", ", files)));
    }
    catch
    {
      context.Reply(Tr(context, "command.listcfg.failed"));
    }
  }

  private void Scramble(ICommandContext context)
  {
    _state.ScrambleNextRound = true;
    context.Reply(Tr(context, "command.scramble.next_round"));
  }

  private void Voices(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply(Tr(context, "error.must_be_player"));
      return;
    }

    var enabled = _state.ToggleVoices(context.Sender.SteamID);
    context.Reply(enabled ? Tr(context, "command.voices.enabled") : Tr(context, "command.voices.disabled"));
  }

  private void Guns(ICommandContext context)
  {
    var core = _core;
    if (core is null)
    {
      context.Reply(Tr(context, "error.plugin_not_ready"));
      return;
    }

    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply(Tr(context, "error.must_be_player"));
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
      context.Reply(Tr(context, "error.plugin_not_ready"));
      return;
    }

    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply(Tr(context, "error.must_be_player"));
      return;
    }

    OpenRetakeMenu(core, context.Sender);
  }

  private void Spawns(ICommandContext context)
  {
    if (!context.IsSentByPlayer || context.Sender is null)
    {
      context.Reply(Tr(context, "error.must_be_player"));
      return;
    }

    var enabled = _prefs.ToggleSpawnMenu(context.Sender.SteamID);
    context.Reply(enabled ? Tr(context, "command.spawns.enabled") : Tr(context, "command.spawns.disabled"));
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
      context.Reply(Tr(context, "error.must_be_player"));
      return;
    }

    var enabled = _prefs.ToggleAwp(context.Sender.SteamID);
    context.Reply(enabled ? Tr(context, "command.awp.enabled") : Tr(context, "command.awp.disabled"));
  }

  private void ReloadCfg(ICommandContext context)
  {
    try
    {
      _config.LoadOrCreate();
      _config.ApplyToConvars(restartGame: true);
      context.Reply(Tr(context, "command.reloadcfg.success"));
    }
    catch
    {
      context.Reply(Tr(context, "command.reloadcfg.failed"));
    }
  }

  private void DebugQueues(ICommandContext context)
  {
    context.Reply(_pawnLifecycle.DebugSummary());
  }
}
