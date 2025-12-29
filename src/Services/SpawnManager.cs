using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2_Retakes.Models;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Utils;

namespace SwiftlyS2_Retakes.Services;

public sealed class SpawnManager : ISpawnManager
{
  private readonly ISwiftlyCore _core;
  private readonly IPawnLifecycleService _pawnLifecycle;
  private readonly Random _random;
  private readonly IPlayerPreferencesService _prefs;

  private readonly Dictionary<Bombsite, Dictionary<Team, List<Spawn>>> _spawns = new();

  private readonly Dictionary<ulong, Spawn> _lastAssignments = new();
  private ulong? _assignedPlanterSteamId;
  private Spawn? _assignedPlanterSpawn;

  private readonly Dictionary<ulong, IMenuAPI> _openSpawnMenuBySteamId = new();

  public SpawnManager(ISwiftlyCore core, IPawnLifecycleService pawnLifecycle, Random random, IPlayerPreferencesService prefs)
  {
    _core = core;
    _pawnLifecycle = pawnLifecycle;
    _random = random;
    _prefs = prefs;

    _spawns[Bombsite.A] = new Dictionary<Team, List<Spawn>>
    {
      [Team.T] = new List<Spawn>(),
      [Team.CT] = new List<Spawn>()
    };

    _spawns[Bombsite.B] = new Dictionary<Team, List<Spawn>>
    {
      [Team.T] = new List<Spawn>(),
      [Team.CT] = new List<Spawn>()
    };
  }

  public void SetSpawns(IEnumerable<Spawn> spawns)
  {
    _spawns[Bombsite.A][Team.T].Clear();
    _spawns[Bombsite.A][Team.CT].Clear();
    _spawns[Bombsite.B][Team.T].Clear();
    _spawns[Bombsite.B][Team.CT].Clear();

    var seen = new HashSet<string>(StringComparer.Ordinal);
    var removedDuplicates = 0;
    var removedInvalid = 0;

    foreach (var spawn in spawns)
    {
      if (spawn.Team != Team.T && spawn.Team != Team.CT)
      {
        continue;
      }

      if (!_spawns.TryGetValue(spawn.Bombsite, out var teamMap))
      {
        continue;
      }

      Vector pos;
      QAngle ang;
      try
      {
        pos = ParseUtil.ParseVector(spawn.Vector);
        ang = ParseUtil.ParseQAngle(spawn.QAngle);
      }
      catch
      {
        removedInvalid++;
        continue;
      }

      static float R(float v) => MathF.Round(v, 2);
      var key = $"{spawn.Bombsite}|{spawn.Team}|{R(pos.X)}|{R(pos.Y)}|{R(pos.Z)}";
      if (!seen.Add(key))
      {
        removedDuplicates++;
        continue;
      }

      teamMap[spawn.Team].Add(spawn);
    }

    if (removedDuplicates > 0)
    {
      _core.Logger.LogWarning("Retakes: removed {Count} duplicate spawns from map config (same team/bombsite/position)", removedDuplicates);
    }

    if (removedInvalid > 0)
    {
      _core.Logger.LogWarning("Retakes: removed {Count} invalid spawns from map config (bad Vector/QAngle format)", removedInvalid);
    }
  }

  public bool HandleRoundSpawns(Bombsite bombsite)
  {
    _lastAssignments.Clear();
    _assignedPlanterSteamId = null;
    _assignedPlanterSpawn = null;

    var teamPlayers = _core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid)
      .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
      .ToList();

    var tPlayers = teamPlayers.Where(p => (Team)p.Controller.TeamNum == Team.T).ToList();
    var ctPlayers = teamPlayers.Where(p => (Team)p.Controller.TeamNum == Team.CT).ToList();

    var tSpawns = _spawns[bombsite][Team.T];
    var ctSpawns = _spawns[bombsite][Team.CT];

    if (tPlayers.Count == 0 && ctPlayers.Count == 0)
    {
      return true;
    }

    var autoPlantEnabled = _core.ConVar.Find<bool>("retakes_auto_plant")?.Value ?? false;

    var availableTSpawns = autoPlantEnabled
      ? tSpawns.Where(s => !s.CanBePlanter).ToList()
      : tSpawns;

    if (autoPlantEnabled && availableTSpawns.Count < tPlayers.Count)
    {
      var planterSpawns = tSpawns.Where(s => s.CanBePlanter).ToList();
      if (planterSpawns.Count > 0)
      {
        availableTSpawns.AddRange(planterSpawns);
      }
    }

    if (availableTSpawns.Count < tPlayers.Count || ctSpawns.Count < ctPlayers.Count)
    {
      _core.Logger.LogWarning(
        "Retakes: Not enough spawns for {Bombsite}. T: {TPlayers}/{TSpawns}, CT: {CTPlayers}/{CTSpawns}",
        bombsite,
        tPlayers.Count,
        availableTSpawns.Count,
        ctPlayers.Count,
        ctSpawns.Count
      );
      return false;
    }

    var tSpawnPool = availableTSpawns.ToList();
    var ctSpawnPool = ctSpawns.ToList();

    Shuffle(tPlayers);
    Shuffle(ctPlayers);
    Shuffle(tSpawnPool);
    Shuffle(ctSpawnPool);

    ApplyPreferredSpawns(bombsite, isCt: false, tPlayers, tSpawnPool);

    if (autoPlantEnabled && tPlayers.Count > 0)
    {
      var planterSpawns = tSpawns.Where(s => s.CanBePlanter).ToList();
      if (planterSpawns.Count > 0)
      {
        _assignedPlanterSteamId = tPlayers[0].SteamID;
        _assignedPlanterSpawn = planterSpawns[_random.Next(planterSpawns.Count)];
      }
    }
    else if (!autoPlantEnabled && tPlayers.Count > 0 && tSpawnPool.Count > 0)
    {
      var planterIndex = tSpawnPool.FindIndex(s => s.CanBePlanter);
      if (planterIndex >= 0)
      {
        (tSpawnPool[0], tSpawnPool[planterIndex]) = (tSpawnPool[planterIndex], tSpawnPool[0]);
        _assignedPlanterSteamId = tPlayers[0].SteamID;
        _assignedPlanterSpawn = tSpawnPool[0];
      }
    }

    for (var i = 0; i < tPlayers.Count; i++)
    {
      var player = tPlayers[i];
      var spawn = tSpawnPool[i];

      _lastAssignments[player.SteamID] = spawn;
      if (!autoPlantEnabled && _assignedPlanterSteamId is null && spawn.CanBePlanter)
      {
        _assignedPlanterSteamId = player.SteamID;
        _assignedPlanterSpawn = spawn;
      }

      _pawnLifecycle.WhenPawnReady(player, p => p.Teleport(spawn.Position, spawn.Angle, Vector.Zero));
    }

    for (var i = 0; i < ctPlayers.Count; i++)
    {
      var player = ctPlayers[i];

      if (!_prefs.WantsSpawnMenu(player.SteamID))
      {
        // If the player disabled the spawn menu, auto-assign a spawn.
        if (ctSpawnPool.Count > 0)
        {
          var spawn = ctSpawnPool[0];
          ctSpawnPool.RemoveAt(0);
          _lastAssignments[player.SteamID] = spawn;
          _pawnLifecycle.WhenPawnReady(player, pawn => pawn.Teleport(spawn.Position, spawn.Angle, Vector.Zero));
        }
      }
      else
      {
        // Spawns are selected interactively each round (menu opened at round start).
        // Do not auto-teleport here.
        _lastAssignments.Remove(player.SteamID);
      }
    }

    return true;
  }

  public void CloseSpawnMenus()
  {
    if (_openSpawnMenuBySteamId.Count == 0) return;

    var entries = _openSpawnMenuBySteamId.ToList();
    foreach (var (steamId, menu) in entries)
    {
      var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId);
      if (player is null || !player.IsValid) continue;

      try
      {
        _core.MenusAPI.CloseMenuForPlayer(player, menu);
      }
      catch
      {
      }
    }

    _openSpawnMenuBySteamId.Clear();
  }

  public void OpenCtSpawnSelectionMenu(Bombsite bombsite)
  {
    CloseSpawnMenus();

    var ctPlayers = _core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid)
      .Where(p => (Team)p.Controller.TeamNum == Team.CT)
      .Where(PlayerUtil.IsHuman)
      .Where(p => _prefs.WantsSpawnMenu(p.SteamID))
      .ToList();

    if (ctPlayers.Count == 0)
    {
      return;
    }

    var spawns = _spawns[bombsite][Team.CT]
      .OrderBy(s => s.Id)
      .ToList();

    if (spawns.Count == 0)
    {
      return;
    }

    foreach (var player in ctPlayers)
    {
      _pawnLifecycle.WhenPawnReady(player, _ =>
      {
        var menu = BuildCtSpawnMenu(bombsite, spawns);

        _openSpawnMenuBySteamId[player.SteamID] = menu;
        _core.MenusAPI.OpenMenuForPlayer(player, menu, (p, closedMenu) =>
        {
          if (p is null || !p.IsValid) return;
          if (_openSpawnMenuBySteamId.TryGetValue(p.SteamID, out var tracked) && ReferenceEquals(tracked, closedMenu))
          {
            _openSpawnMenuBySteamId.Remove(p.SteamID);
          }
        });
      });
    }
  }

  private IMenuAPI BuildCtSpawnMenu(Bombsite bombsite, List<Spawn> spawns)
  {
    var builder = _core.MenusAPI.CreateBuilder()
      .Design.SetMenuTitle("Spawn Menu")
      .EnableSound();

    foreach (var s in spawns)
    {
      var label = string.IsNullOrWhiteSpace(s.Name) ? $"#{s.Id}" : $"#{s.Id} - {s.Name}";
      var opt = new ButtonMenuOption(label);
      opt.Click += async (_, args) =>
      {
        var pawn = args.Player.Pawn;
        if (pawn is not null && pawn.IsValid)
        {
          pawn.Teleport(s.Position, s.Angle, Vector.Zero);
        }
        else
        {
          _pawnLifecycle.WhenPawnReady(args.Player, p => p.Teleport(s.Position, s.Angle, Vector.Zero));
        }
        await ValueTask.CompletedTask;
      };
      builder.AddOption(opt);
    }

    return builder.Build();
  }

  private void ApplyPreferredSpawns(Bombsite bombsite, bool isCt, List<IPlayer> players, List<Spawn> spawnPool)
  {
    if (players.Count == 0 || spawnPool.Count == 0) return;

    var autoPlantEnabled = _core.ConVar.Find<bool>("retakes_auto_plant")?.Value ?? false;

    for (var i = players.Count - 1; i >= 0; i--)
    {
      var player = players[i];
      var preferredId = _prefs.GetPreferredSpawn(player.SteamID, isCt, bombsite);
      if (!preferredId.HasValue) continue;

      var idx = spawnPool.FindIndex(s => s.Id == preferredId.Value);
      if (idx < 0) continue;

      var spawn = spawnPool[idx];
      spawnPool.RemoveAt(idx);
      players.RemoveAt(i);

      _lastAssignments[player.SteamID] = spawn;
      if (!autoPlantEnabled && _assignedPlanterSteamId is null && !isCt && spawn.CanBePlanter)
      {
        _assignedPlanterSteamId = player.SteamID;
        _assignedPlanterSpawn = spawn;
      }

      _pawnLifecycle.WhenPawnReady(player, p => p.Teleport(spawn.Position, spawn.Angle, Vector.Zero));
    }
  }

  public bool TryGetAssignedPlanter(out ulong steamId, out Spawn spawn)
  {
    if (_assignedPlanterSteamId is not null && _assignedPlanterSpawn is not null)
    {
      steamId = _assignedPlanterSteamId.Value;
      spawn = _assignedPlanterSpawn;
      return true;
    }

    steamId = 0;
    spawn = null!;
    return false;
  }

  private void Shuffle<T>(IList<T> list)
  {
    for (var i = list.Count - 1; i > 0; i--)
    {
      var j = _random.Next(i + 1);
      (list[i], list[j]) = (list[j], list[i]);
    }
  }
}
