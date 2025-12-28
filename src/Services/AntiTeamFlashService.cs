using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;

namespace SwiftlyS2_Retakes.Services;

public sealed class AntiTeamFlashService : IAntiTeamFlashService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;

  private readonly IConVar<bool> _enabled;
  private readonly IConVar<bool> _flashOwner;
  private readonly IConVar<string> _accessFlag;

  private Guid _playerBlindHook;

  public AntiTeamFlashService(ISwiftlyCore core, ILogger logger)
  {
    _core = core;
    _logger = logger;

    _enabled = core.ConVar.CreateOrFind("retakes_antiteamflash_enabled", "Anti team flash enabled", true);
    _flashOwner = core.ConVar.CreateOrFind("retakes_antiteamflash_flash_owner", "Allow the flash owner to be flashed by their own flash (false = protect owner, true = flash owner)", false);
    _accessFlag = core.ConVar.CreateOrFind("retakes_antiteamflash_access_flag", "Permission flag required to receive anti team flash protection (empty = everyone)", "");
  }

  public void Register()
  {
    _playerBlindHook = _core.GameEvent.HookPost<EventPlayerBlind>(OnPlayerBlind);
  }

  public void Unregister()
  {
    if (_playerBlindHook != Guid.Empty) _core.GameEvent.Unhook(_playerBlindHook);
    _playerBlindHook = Guid.Empty;
  }

  private HookResult OnPlayerBlind(EventPlayerBlind @event)
  {
    if (!_enabled.Value) return HookResult.Continue;

    var victim = @event.UserIdPlayer;
    if (victim is null || !victim.IsValid) return HookResult.Continue;
    if (victim.Controller is null || !victim.Controller.PawnIsAlive) return HookResult.Continue;

    var attackerId = @event.Attacker;
    
    IPlayer? attacker = null;
    
    if (attackerId > 0)
    {
      // Note: for game events, attacker is typically a userid (not a slot). Some APIs also surface slot/playerid.
      // Matching both makes this reliable.
      attacker = _core.PlayerManager.GetAllPlayers()
        .FirstOrDefault(p => p is not null && p.IsValid && (p.PlayerID == attackerId || p.Slot == attackerId));
    }
    
    // If attacker is 0 or not found, it's likely a self-flash (CS2 bug where self-flash attacker is 0)
    if (attacker is null || !attacker.IsValid || attacker.Controller is null)
    {
      attacker = victim;
    }

    var requiredFlag = (_accessFlag.Value ?? string.Empty).Trim();
    if (!string.IsNullOrEmpty(requiredFlag))
    {
      try
      {
        if (!_core.Permission.PlayerHasPermission(victim.SteamID, requiredFlag))
        {
          return HookResult.Continue;
        }
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Retakes: anti team flash permission check failed");
        return HookResult.Continue;
      }
    }

    var victimTeam = (Team)victim.Controller.TeamNum;
    var attackerTeam = (Team)attacker.Controller.TeamNum;

    if (victimTeam != Team.T && victimTeam != Team.CT) return HookResult.Continue;
    if (attackerTeam != Team.T && attackerTeam != Team.CT) return HookResult.Continue;

    if (victimTeam != attackerTeam) return HookResult.Continue;

    var isSelfFlash = attacker.SteamID == victim.SteamID;

    // FlashOwner semantics: true = allow owner to be flashed, false = protect owner
    if (isSelfFlash && _flashOwner.Value)
    {
      return HookResult.Continue;
    }

    var pawn = victim.PlayerPawn;
    if (pawn is null || !pawn.IsValid) return HookResult.Continue;

    // Apply on next tick so we override the engine-applied blind values.
    _core.Scheduler.NextTick(() =>
    {
      if (victim is null || !victim.IsValid) return;
      if (victim.Controller is null || !victim.Controller.PawnIsAlive) return;

      var p = victim.PlayerPawn;
      if (p is null || !p.IsValid) return;

      var now = _core.Engine.GlobalVars.CurrentTime;

      // Clear flash effect
      p.BlindStartTime.Value = now;
      p.BlindUntilTime.Value = now;

      p.FlashDuration = 0.0f;
      p.FlashMaxAlpha = 0.0f;
      p.FlashDurationUpdated();
      p.FlashMaxAlphaUpdated();
    });

    return HookResult.Continue;
  }
}
