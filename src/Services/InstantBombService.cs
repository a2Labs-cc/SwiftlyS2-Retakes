using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class InstantBombService : IInstantBombService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IMessageService _messages;

  private readonly IConVar<bool> _instaPlant;
  private readonly IConVar<bool> _instaDefuse;
  private readonly IConVar<bool> _defuseBlockIfTAlive;
  private readonly IConVar<bool> _defuseBlockIfMollyNear;
  private readonly IConVar<float> _defuseMollyRadius;

  private float? _originalPlantTime;

  private Vector? _bombSitePosition;
  private float _bombPlantedTime = float.NaN;
  private CPlantedC4? _plantedBomb;

  private readonly List<Vector> _activeInfernos = new();

  private Guid _bombBeginPlantHook;
  private Guid _bombPlantedHook;
  private Guid _infernoStartHook;
  private Guid _infernoExpireHook;
  private Guid _bombBeginDefuseHook;
  private Guid _roundStartHook;
  private Guid _bombDefusedHook;
  private Guid _bombExplodedHook;

  public InstantBombService(ISwiftlyCore core, ILogger logger, IMessageService messages)
  {
    _core = core;
    _logger = logger;
    _messages = messages;

    _instaPlant = core.ConVar.CreateOrFind("retakes_insta_plant", "Instant plant (sets mp_planttime 0 while plugin loaded)", true);
    _instaDefuse = core.ConVar.CreateOrFind("retakes_insta_defuse", "Instant defuse", true);
    _defuseBlockIfTAlive = core.ConVar.CreateOrFind("retakes_insta_defuse_block_t_alive", "Block instant defuse when a terrorist is alive", true);
    _defuseBlockIfMollyNear = core.ConVar.CreateOrFind("retakes_insta_defuse_block_molly", "Block instant defuse when molly is near bomb", true);
    _defuseMollyRadius = core.ConVar.CreateOrFind("retakes_insta_defuse_molly_radius", "Molly radius to block instant defuse", 120f, 0f, 1000f);
  }

  private void BroadcastTranslation(string key, params object[] args)
  {
    foreach (var player in _core.PlayerManager.GetAllPlayers())
    {
      if (player is null || !player.IsValid) continue;
      var localizer = _core.Translation.GetPlayerLocalizer(player);
      player.SendMessage(MessageType.Chat, _messages.FormatChat(localizer[key, args].Colored()));
    }
  }

  public void Register()
  {
    ApplyPlantTime();

    _bombBeginPlantHook = _core.GameEvent.HookPre<EventBombBeginplant>(OnBombBeginPlant);
    _bombPlantedHook = _core.GameEvent.HookPre<EventBombPlanted>(OnBombPlanted);
    _infernoStartHook = _core.GameEvent.HookPre<EventInfernoStartburn>(OnInfernoStartBurn);
    _infernoExpireHook = _core.GameEvent.HookPre<EventInfernoExpire>(OnInfernoExpire);
    _bombBeginDefuseHook = _core.GameEvent.HookPre<EventBombBegindefuse>(OnBombBeginDefuse);

    _roundStartHook = _core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
    _bombDefusedHook = _core.GameEvent.HookPre<EventBombDefused>(OnBombDefused);
    _bombExplodedHook = _core.GameEvent.HookPre<EventBombExploded>(OnBombExploded);
  }

  public void Unregister()
  {
    if (_bombBeginPlantHook != Guid.Empty) _core.GameEvent.Unhook(_bombBeginPlantHook);
    if (_bombPlantedHook != Guid.Empty) _core.GameEvent.Unhook(_bombPlantedHook);
    if (_infernoStartHook != Guid.Empty) _core.GameEvent.Unhook(_infernoStartHook);
    if (_infernoExpireHook != Guid.Empty) _core.GameEvent.Unhook(_infernoExpireHook);
    if (_bombBeginDefuseHook != Guid.Empty) _core.GameEvent.Unhook(_bombBeginDefuseHook);

    if (_roundStartHook != Guid.Empty) _core.GameEvent.Unhook(_roundStartHook);
    if (_bombDefusedHook != Guid.Empty) _core.GameEvent.Unhook(_bombDefusedHook);
    if (_bombExplodedHook != Guid.Empty) _core.GameEvent.Unhook(_bombExplodedHook);

    _bombBeginPlantHook = Guid.Empty;
    _bombPlantedHook = Guid.Empty;
    _infernoStartHook = Guid.Empty;
    _infernoExpireHook = Guid.Empty;
    _bombBeginDefuseHook = Guid.Empty;

    _roundStartHook = Guid.Empty;
    _bombDefusedHook = Guid.Empty;
    _bombExplodedHook = Guid.Empty;

    RestorePlantTime();
    ResetAll();
  }

  private void ApplyPlantTime()
  {
    var plantTime = _core.ConVar.Find<float>("mp_planttime");
    if (plantTime is null) return;

    if (_originalPlantTime is null)
    {
      _originalPlantTime = plantTime.Value;
    }

    if (_instaPlant.Value)
    {
      plantTime.Value = 0f;
    }
  }

  private void RestorePlantTime()
  {
    if (_originalPlantTime is null) return;

    var plantTime = _core.ConVar.Find<float>("mp_planttime");
    if (plantTime is null) return;

    plantTime.Value = _originalPlantTime.Value;
  }

  private HookResult OnBombBeginPlant(EventBombBeginplant @event)
  {
    if (!_instaPlant.Value) return HookResult.Continue;

    var planter = @event.UserIdPlayer;
    if (planter is null || !planter.IsValid) return HookResult.Continue;

    var pawn = planter.PlayerPawn;
    if (pawn is null || !pawn.IsValid) return HookResult.Continue;

    _core.Scheduler.NextTick(() =>
    {
      try
      {
        CC4? c4 = null;

        var weaponServices = pawn.WeaponServices;
        var activeWeapon = weaponServices?.ActiveWeapon.Value;
        if (activeWeapon is not null && activeWeapon.IsValid)
        {
          c4 = activeWeapon.As<CC4>();
        }

        if (c4 is null || !c4.IsValid)
        {
          // Fallback: find any weapon_c4 owned by this pawn
          var allC4 = _core.EntitySystem.GetAllEntitiesByDesignerName<CC4>("weapon_c4");
          foreach (var w in allC4)
          {
            if (!w.IsValid) continue;
            var owner = w.OwnerEntity.Value;
            if (owner is not null && owner.IsValid && owner.Address == pawn.Address)
            {
              c4 = w;
              break;
            }
          }
        }

        if (c4 is null || !c4.IsValid) return;

        // Make plant finish immediately by setting armed time to now.
        c4.StartedArming = true;
        c4.IsPlantingViaUse = true;
        c4.ArmedTime.Value = _core.Engine.GlobalVars.CurrentTime;
        c4.BombPlacedAnimation = true;

        c4.StartedArmingUpdated();
        c4.IsPlantingViaUseUpdated();
        c4.ArmedTimeUpdated();
        c4.BombPlacedAnimationUpdated();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Retakes: Failed to force instant plant");
      }
    });

    return HookResult.Continue;
  }

  private HookResult OnBombPlanted(EventBombPlanted @event)
  {
    var bomb = FindPlantedBomb();
    if (bomb is null || !bomb.IsValid) return HookResult.Continue;

    _bombSitePosition = bomb.AbsOrigin;
    _bombPlantedTime = _core.Engine.GlobalVars.CurrentTime;
    _plantedBomb = bomb;

    return HookResult.Continue;
  }

  private HookResult OnInfernoStartBurn(EventInfernoStartburn @event)
  {
    _activeInfernos.Add(new Vector(@event.X, @event.Y, @event.Z));
    return HookResult.Continue;
  }

  private HookResult OnInfernoExpire(EventInfernoExpire @event)
  {
    _activeInfernos.Clear();
    return HookResult.Continue;
  }

  private HookResult OnBombBeginDefuse(EventBombBegindefuse @event)
  {
    if (!_instaDefuse.Value) return HookResult.Continue;

    var defuser = @event.UserIdController;
    if (defuser is null || !defuser.IsValid || !defuser.PawnIsAlive) return HookResult.Continue;

    if (_plantedBomb is null || !_plantedBomb.IsValid)
    {
      _plantedBomb = FindPlantedBomb();
      if (_plantedBomb is not null && _plantedBomb.IsValid)
      {
        _bombSitePosition ??= _plantedBomb.AbsOrigin;
        if (float.IsNaN(_bombPlantedTime) && _plantedBomb.TimerLength > 0.0f)
        {
          var blowTime = _plantedBomb.C4Blow.Value;
          if (blowTime > 0.0f)
          {
            _bombPlantedTime = blowTime - _plantedBomb.TimerLength;
          }
        }
      }
    }

    if (_plantedBomb is null || !_plantedBomb.IsValid || _plantedBomb.CannotBeDefused)
    {
      _logger.LogPluginInformation("Retakes: instant defuse skipped (no valid planted bomb tracked)");
      return HookResult.Continue;
    }

    if (_bombSitePosition is null)
    {
      _logger.LogWarning("Retakes: instant defuse attempted without planted bomb tracking");
      return HookResult.Continue;
    }

    var terroristsAlive = _defuseBlockIfTAlive.Value
      && _core.PlayerManager.GetAllPlayers().Any(p =>
        p.IsValid
        && p.Controller.PawnIsAlive
        && (Team)p.Controller.TeamNum == Team.T);

    var mollyNear = false;
    if (_defuseBlockIfMollyNear.Value)
    {
      var radius = _defuseMollyRadius.Value;
      foreach (var fire in _activeInfernos)
      {
        var dx = fire.X - _bombSitePosition.Value.X;
        var dy = fire.Y - _bombSitePosition.Value.Y;
        var dist2D = MathF.Sqrt(dx * dx + dy * dy);

        if (dist2D < radius)
        {
          mollyNear = true;
          break;
        }
      }
    }

    if (terroristsAlive || mollyNear)
    {
      _logger.LogPluginInformation("Retakes: instant defuse blocked. TerroristsAlive={TAlive} MollyNear={MollyNear}", terroristsAlive, mollyNear);

      var key = (terroristsAlive, mollyNear) switch
      {
        (true, true) => "instadefuse.not_possible_enemy_molly",
        (true, false) => "instadefuse.not_possible_enemy",
        (false, true) => "instadefuse.not_possible_molly",
        _ => "instadefuse.not_possible",
      };

      BroadcastTranslation(key);
      return HookResult.Continue;
    }

    var requiredDefuseTime = defuser.PawnHasDefuser ? 5.0f : 10.0f;

    var now = _core.Engine.GlobalVars.CurrentTime;
    var blowTimeNow = _plantedBomb.C4Blow.Value;
    var bombTimeUntilDetonation = blowTimeNow > 0.0f
      ? (blowTimeNow - now)
      : (!float.IsNaN(_bombPlantedTime)
        ? _plantedBomb.TimerLength - (now - _bombPlantedTime)
        : float.NaN);

    if (float.IsNaN(bombTimeUntilDetonation))
    {
      _logger.LogWarning("Retakes: instant defuse attempted without being able to determine bomb time remaining");
      return HookResult.Continue;
    }

    var timeRemainingAtStart = bombTimeUntilDetonation;
    if (timeRemainingAtStart < 0.0f) timeRemainingAtStart = 0.0f;

    _core.Scheduler.NextTick(() =>
    {
      if (_plantedBomb is null || !_plantedBomb.IsValid) return;
      if (_bombSitePosition is null) return;

      var currentTime = _core.Engine.GlobalVars.CurrentTime;
      var blowTime = _plantedBomb.C4Blow.Value;
      var timeRemaining = blowTime > 0.0f
        ? (blowTime - currentTime)
        : (!float.IsNaN(_bombPlantedTime)
          ? _plantedBomb.TimerLength - (currentTime - _bombPlantedTime)
          : float.NaN);

      if (float.IsNaN(timeRemaining)) return;

      var defuserName = defuser.PlayerName;

      if (timeRemaining < requiredDefuseTime)
      {
        _plantedBomb.C4Blow.Value = currentTime;
        _plantedBomb.C4BlowUpdated();

        BroadcastTranslation(
          "instadefuse.unsuccessful",
          defuserName,
          timeRemainingAtStart.ToString("0.0"),
          requiredDefuseTime.ToString("0.0"));
      }
      else
      {
        _plantedBomb.DefuseCountDown.Value = currentTime;
        _plantedBomb.DefuseCountDownUpdated();

        BroadcastTranslation("instadefuse.successful", defuserName, timeRemainingAtStart.ToString("0.0"));
      }
    });

    return HookResult.Continue;
  }

  private HookResult OnRoundStart(EventRoundStart @event)
  {
    ResetAll();
    ApplyPlantTime();
    return HookResult.Continue;
  }

  private HookResult OnBombDefused(EventBombDefused @event)
  {
    ResetAll();
    return HookResult.Continue;
  }

  private HookResult OnBombExploded(EventBombExploded @event)
  {
    ResetAll();
    return HookResult.Continue;
  }

  private CPlantedC4? FindPlantedBomb()
  {
    var bombs = _core.EntitySystem.GetAllEntitiesByDesignerName<CPlantedC4>("planted_c4");
    return bombs.FirstOrDefault(b => b.IsValid);
  }

  private void ResetAll()
  {
    _bombSitePosition = null;
    _bombPlantedTime = float.NaN;
    _plantedBomb = null;
    _activeInfernos.Clear();
  }
}
