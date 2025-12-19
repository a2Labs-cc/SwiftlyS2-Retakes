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
    _flashOwner = core.ConVar.CreateOrFind("retakes_antiteamflash_flash_owner", "Also block the flash owner from their own team flashes", true);
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

    var attackerUserId = @event.Attacker;
    if (attackerUserId <= 0) return HookResult.Continue;

    var attacker = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p is not null && p.IsValid && p.Slot == attackerUserId);
    if (attacker is null || !attacker.IsValid || attacker.Controller is null) return HookResult.Continue;

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

    if (!_flashOwner.Value && attacker.SteamID == victim.SteamID)
    {
      return HookResult.Continue;
    }

    var pawn = victim.PlayerPawn;
    if (pawn is null || !pawn.IsValid) return HookResult.Continue;

    var now = _core.Engine.GlobalVars.CurrentTime;
    pawn.BlindUntilTime.Value = now;

    return HookResult.Continue;
  }
}
