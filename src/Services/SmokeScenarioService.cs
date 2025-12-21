using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class SmokeScenarioService : ISmokeScenarioService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IMapConfigService _mapConfig;
  private readonly IConVar<string> _smokeFallbackParticle;

  private static bool TryEmitSmokeGrenade(Vector pos, QAngle angle, Vector velocity, Team team, CBasePlayerPawn? owner,
    out CSmokeGrenadeProjectile? projectile)
  {
    projectile = null;
    try
    {
      var type = AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("SwiftlyS2.Core.SchemaDefinitions.CSmokeGrenadeProjectileImpl", throwOnError: false))
        .FirstOrDefault(t => t is not null);

      if (type is null)
      {
        return false;
      }

      var method = type.GetMethod(
        "EmitGrenade",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
        binder: null,
        types: new[] { typeof(Vector), typeof(QAngle), typeof(Vector), typeof(Team), typeof(CBasePlayerPawn) },
        modifiers: null);

      if (method is null)
      {
        return false;
      }

      var obj = method.Invoke(null, new object?[] { pos, angle, velocity, team, owner });
      projectile = obj as CSmokeGrenadeProjectile;
      return projectile is not null && projectile.IsValid;
    }
    catch
    {
      projectile = null;
      return false;
    }
  }

  public SmokeScenarioService(ISwiftlyCore core, ILogger logger, IMapConfigService mapConfig)
  {
    _core = core;
    _logger = logger;
    _mapConfig = mapConfig;

    _smokeFallbackParticle = core.ConVar.CreateOrFind(
      "retakes_smoke_fallback_particle",
      "Particle name used when smoke scenarios fail to detonate (Engine.DispatchParticleEffect fallback)",
      "");
  }

  public IReadOnlyList<SmokeScenario> GetSmokeScenariosForBombsite(Bombsite bombsite)
  {
    return _mapConfig.SmokeScenarios
      .Where(s => s.Bombsite == bombsite)
      .ToList();
  }

  public SmokeScenario? SpawnSmokeById(int smokeId)
  {
    var scenario = _mapConfig.SmokeScenarios.FirstOrDefault(s => s.Id == smokeId);
    if (scenario is null)
    {
      return null;
    }

    _core.Scheduler.DelayBySeconds(0.1f, () =>
    {
      try
      {
        _logger.LogPluginInformation(
          "Retakes: Spawning smoke scenario by ID {Id} at {Position} for bombsite {Bombsite}",
          scenario.Id,
          scenario.Vector,
          scenario.Bombsite);

        SpawnSmoke(scenario);
      }
      catch (Exception ex)
      {
        _logger.LogPluginError(ex, "Retakes: Failed to spawn smoke scenario by ID {Id} at {Position}", scenario.Id, scenario.Vector);
      }
    });

    return scenario;
  }

  public SmokeScenario? SpawnSmokesForBombsite(Bombsite bombsite)
  {
    _logger.LogPluginInformation("Retakes: SpawnSmokesForBombsite called for bombsite {Bombsite}", bombsite);
    _logger.LogPluginInformation("Retakes: Total smoke scenarios in config: {Total}", _mapConfig.SmokeScenarios.Count);

    var scenarios = GetSmokeScenariosForBombsite(bombsite);

    _logger.LogPluginInformation("Retakes: Found {Count} smoke scenarios for bombsite {Bombsite}", scenarios.Count, bombsite);

    if (scenarios.Count == 0)
    {
      return null;
    }

    var chosen = scenarios[Random.Shared.Next(0, scenarios.Count)];

    _core.Scheduler.DelayBySeconds(0.5f, () =>
    {
      try
      {
        _logger.LogPluginInformation(
          "Retakes: Spawning smoke scenario ID {Id} at {Position} for bombsite {Bombsite}",
          chosen.Id,
          chosen.Vector,
          chosen.Bombsite);

        SpawnSmoke(chosen);

        _logger.LogPluginInformation("Retakes: Finished spawning smoke scenario ID {Id} for bombsite {Bombsite}", chosen.Id, bombsite);
      }
      catch (Exception ex)
      {
        _logger.LogPluginError(ex, "Retakes: Failed to spawn smoke scenario ID {Id} at {Position}", chosen.Id, chosen.Vector);
      }
    });

    return chosen;
  }

  private void SpawnSmoke(SmokeScenario scenario)
  {
    try
    {
      var detonationPos = scenario.Position;

      var thrower = _core.PlayerManager.GetAllPlayers()
        .FirstOrDefault(p => p.IsValid && p.Controller.PawnIsAlive && p.PlayerPawn is not null && p.PlayerPawn.IsValid &&
                             ((Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT));

      var ownerPawn = thrower?.PlayerPawn;
      var team = thrower is null ? Team.CT : (Team)thrower.Controller.TeamNum;

      var spawnPos = new Vector(detonationPos.X, detonationPos.Y, detonationPos.Z + 8);
      var didEmit = TryEmitSmokeGrenade(
        spawnPos,
        new QAngle(0, 0, 0),
        new Vector(0, 0, -50),
        team,
        ownerPawn,
        out var smokeProjectile);

      _logger.LogPluginInformation(
        "Retakes: Smoke projectile spawn path: {SpawnPath}",
        didEmit ? "core_emitgrenade" : "create_entity_fallback");

      smokeProjectile ??= _core.EntitySystem.CreateEntityByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
      if (smokeProjectile is null || !smokeProjectile.IsValid)
      {
        _logger.LogPluginWarning("Retakes: Failed to create smoke projectile entity");
        return;
      }

      if (!didEmit)
      {
        smokeProjectile.Teleport(spawnPos, new QAngle(0, 0, 0), new Vector(0, 0, -50));
        smokeProjectile.DispatchSpawn();
      }

      smokeProjectile.ItemIndex = (ushort)45;

      if (ownerPawn is not null && ownerPawn.IsValid)
      {
        smokeProjectile.Thrower.Value = ownerPawn;
        smokeProjectile.OriginalThrower.Value = ownerPawn;
        smokeProjectile.ThrowerUpdated();
      }

      smokeProjectile.SmokeDetonationPos = detonationPos;
      smokeProjectile.SmokeDetonationPosUpdated();

      smokeProjectile.IsSmokeGrenade = true;
      smokeProjectile.IsLive = true;
      smokeProjectile.IsLiveUpdated();

      smokeProjectile.DetonateTime.Value = _core.Engine.GlobalVars.CurrentTime + 0.10f;
      smokeProjectile.DetonateTimeUpdated();

      _core.Scheduler.DelayBySeconds(2.0f, () =>
      {
        if (!smokeProjectile.IsValid) return;

        _logger.LogPluginInformation(
          "Retakes: Smoke post-check at {Position}: DidSmokeEffect={DidSmokeEffect}, IsLive={IsLive}",
          scenario.Vector,
          smokeProjectile.DidSmokeEffect,
          smokeProjectile.IsLive);

        if (!smokeProjectile.DidSmokeEffect)
        {
          var filter = new CRecipientFilter(NetChannelBufType_t.BUF_RELIABLE);
          filter.AddAllPlayers();

          var particleName = (_smokeFallbackParticle.Value ?? string.Empty).Trim();
          if (string.IsNullOrEmpty(particleName))
          {
            return;
          }

          _core.Engine.DispatchParticleEffect(
            particleName,
            ParticleAttachment_t.PATTACH_ABSORIGIN,
            0,
            string.Empty,
            filter,
            resetAllParticlesOnEntity: false,
            splitScreenSlot: 0,
            entity: smokeProjectile);

          _logger.LogPluginInformation(
            "Retakes: Smoke particle fallback dispatched at {Position} (particle: {Particle})",
            scenario.Vector,
            particleName);
        }
      });

      _logger.LogPluginInformation("Retakes: Smoke grenade spawned at {Position} (forced detonation)", scenario.Vector);
    }
    catch (Exception ex)
    {
      _logger.LogPluginError(ex, "Retakes: Failed to spawn smoke at {Position}", scenario.Vector);
    }
  }
}
