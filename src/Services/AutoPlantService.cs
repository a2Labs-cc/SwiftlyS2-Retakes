using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class AutoPlantService : IAutoPlantService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IMapConfigService _mapConfig;
  private readonly Random _random;

  private readonly IConVar<bool> _autoPlant;
  private readonly IConVar<bool> _autoPlantStripC4;
  private readonly IConVar<bool> _enforceNoC4;

  public AutoPlantService(ISwiftlyCore core, ILogger logger, IMapConfigService mapConfig, Random random)
  {
    _core = core;
    _logger = logger;
    _mapConfig = mapConfig;
    _random = random;

    _autoPlant = core.ConVar.CreateOrFind("retakes_auto_plant", "Auto plant bomb at freeze end", true);
    _autoPlantStripC4 = core.ConVar.CreateOrFind("retakes_auto_plant_strip_c4", "Auto-plant: remove C4 from planter after planting", false);
    _enforceNoC4 = core.ConVar.CreateOrFind("retakes_enforce_no_c4", "Enforce mp_give_player_c4 0 to avoid C4 when using auto-plant", true);
  }

  public void EnforceNoC4()
  {
    if (_autoPlant.Value && _enforceNoC4.Value)
    {
      _core.Engine.ExecuteCommand("mp_give_player_c4 0");
    }
  }

  public void TryAutoPlant(Bombsite bombsite, ulong? assignedPlanterSteamId = null, Spawn? assignedPlanterSpawn = null)
  {
    if (!_autoPlant.Value)
    {
      _logger.LogPluginInformation("Retakes: auto-plant skipped (retakes_auto_plant=0)");
      return;
    }

    var rules = _core.EntitySystem.GetGameRules();
    if (rules is not null && rules.WarmupPeriod)
    {
      _logger.LogPluginInformation("Retakes: auto-plant skipped (warmup)");
      return;
    }

    const int maxAttempts = 15;

    void Attempt(int attempt)
    {
      try
      {
        var existing = _core.EntitySystem.GetAllEntitiesByDesignerName<CPlantedC4>("planted_c4");
        if (existing.Any(b => b is not null && b.IsValid))
        {
          _logger.LogPluginInformation("Retakes: auto-plant skipped (bomb already planted)");
          return;
        }

        IPlayer? eventPlanter = null;
        if (assignedPlanterSteamId is not null)
        {
          eventPlanter = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p =>
            p.IsValid
            && p.SteamID == assignedPlanterSteamId.Value);

          if (eventPlanter is null || !eventPlanter.IsValid)
          {
            _logger.LogPluginWarning(
              "Retakes: auto-plant: assigned planter not found, falling back to any alive player. SteamId={SteamId}",
              assignedPlanterSteamId.Value);
            eventPlanter = null;
          }
          else if (!eventPlanter.Controller.PawnIsAlive || eventPlanter.Pawn is null)
          {
            if (attempt < maxAttempts)
            {
              _core.Scheduler.DelayBySeconds(0.1f, () => Attempt(attempt + 1));
              return;
            }

            _logger.LogPluginWarning(
              "Retakes: auto-plant: assigned planter pawn not ready after retries, planting without assigned planter. SteamId={SteamId} Slot={Slot}",
              assignedPlanterSteamId.Value,
              eventPlanter.Slot);
            eventPlanter = null;
          }
        }

        if (eventPlanter is null)
        {
          var players = _core.PlayerManager.GetAllPlayers();
          eventPlanter = players.FirstOrDefault(p =>
            p.IsValid
            && p.Controller.PawnIsAlive
            && (Team)p.Controller.TeamNum == Team.T)
            ?? players.FirstOrDefault(p =>
              p.IsValid
              && p.Controller.PawnIsAlive);

          if (eventPlanter is not null && eventPlanter.IsValid && eventPlanter.Pawn is null)
          {
            _logger.LogPluginWarning("Retakes: auto-plant: fallback planter pawn not ready. Slot={Slot}", eventPlanter.Slot);
            eventPlanter = null;
          }
        }

        var spawn = assignedPlanterSpawn;
        if (spawn is null)
        {
          var planterSpawns = _mapConfig.Spawns
            .Where(s => s.Team == Team.T && s.Bombsite == bombsite && s.CanBePlanter)
            .ToList();

          if (planterSpawns.Count == 0)
          {
            planterSpawns = _mapConfig.Spawns
              .Where(s => s.Bombsite == bombsite && s.CanBePlanter)
              .ToList();
          }

          if (planterSpawns.Count == 0)
          {
            _logger.LogPluginWarning(
              "Retakes: auto-plant failed (no CanBePlanter spawns). Map={Map} Bombsite={Bombsite}",
              _mapConfig.LoadedMapName,
              bombsite);
            return;
          }

          spawn = planterSpawns[_random.Next(planterSpawns.Count)];
        }

        // Note: SpawnManager already teleported the planter to their assigned spawn.
        // We only plant the bomb at that position; no redundant teleport needed.

        var planted = _core.EntitySystem.CreateEntityByDesignerName<CPlantedC4>("planted_c4");
        if (planted is null || !planted.IsValid)
        {
          _logger.LogPluginWarning("Retakes: auto-plant failed (CreateEntity planted_c4 returned null/invalid)");
          return;
        }

        var body = planted.CBodyComponent;
        var node = body?.SceneNode;
        if (node is not null)
        {
          node.AbsOrigin.X = spawn.Position.X;
          node.AbsOrigin.Y = spawn.Position.Y;
          node.AbsOrigin.Z = spawn.Position.Z;
        }

        planted.HasExploded = false;
        planted.BombSite = bombsite == Bombsite.A ? 0 : 1;
        planted.BombTicking = true;
        planted.CannotBeDefused = false;

        planted.DispatchSpawn();

        var rules = _core.EntitySystem.GetGameRules();
        if (rules is not null)
        {
          rules.BombPlanted = true;
          rules.BombDefused = false;
          rules.BombPlantedUpdated();
        }

        var site = (short)(bombsite == Bombsite.A ? 0 : 1);
        if (eventPlanter is not null && eventPlanter.IsValid)
        {
          _core.GameEvent.Fire<EventBombPlanted>(e =>
          {
            e.Site = site;
            e.UserId = eventPlanter.Slot;
          });
        }
        else
        {
          _logger.LogPluginInformation("Retakes: auto-planted bomb without a planter (no alive players)");
        }

        _logger.LogPluginDebug(
          "Retakes: auto-planted bomb. Bombsite={Bombsite} Slot={Slot} AssignedPlanter={Assigned}",
          bombsite,
          eventPlanter?.Slot ?? -1,
          assignedPlanterSteamId is not null);

        if (_autoPlantStripC4.Value)
        {
          _logger.LogPluginWarning("Retakes: retakes_auto_plant_strip_c4 is enabled, but stripping C4 is not supported (can crash server)");
        }
      }
      catch (Exception ex)
      {
        _logger.LogPluginError(ex, "Retakes: auto-plant exception");
      }
    }

    _core.Scheduler.DelayBySeconds(0.1f, () => Attempt(0));
  }

  private void RemoveAllC4Weapons()
  {
    try
    {
      var weapons = _core.EntitySystem.GetAllEntitiesByDesignerName<CC4>("weapon_c4");
      foreach (var w in weapons)
      {
        if (w is null || !w.IsValid) continue;
        w.Despawn();
      }
    }
    catch (Exception ex)
    {
      _logger.LogPluginError(ex, "Retakes: failed to remove C4 weapons");
    }
  }
}
