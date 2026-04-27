using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;
using SwiftlyS2_Retakes.Utils;
using System.Linq;

namespace SwiftlyS2_Retakes.Handlers;

public sealed class PlayerEventHandlers
{
  private ISwiftlyCore? _core;
  private readonly IPawnLifecycleService _pawnLifecycle;
  private readonly IClutchAnnounceService _clutch;
  private readonly IPlayerPreferencesService _prefs;
  private readonly IRetakesStateService _state;
  private readonly IRetakesConfigService _config;
  private readonly IQueueService _queue;
  private readonly IDamageReportService _damageReport;
  private readonly ISoloBotService _soloBot;
  private readonly IAllocationService _allocation;

  private Guid _playerSpawnPreHook;
  private Guid _playerSpawnPostHook;
  private Guid _playerTeamPreHook;
  private Guid _playerDeathHook;
  private Guid _playerDisconnectHook;
  private Guid _clientCommandHook;
  private Guid _playerHurtHook;
  private Guid _bombDefusedHook;

  public PlayerEventHandlers(IPawnLifecycleService pawnLifecycle, IClutchAnnounceService clutch, IPlayerPreferencesService prefs, IRetakesStateService state, IRetakesConfigService config, IQueueService queue, IDamageReportService damageReport, ISoloBotService soloBot, IAllocationService allocation)
  {
    _pawnLifecycle = pawnLifecycle;
    _clutch = clutch;
    _prefs = prefs;
    _state = state;
    _config = config;
    _queue = queue;
    _damageReport = damageReport;
    _soloBot = soloBot;
    _allocation = allocation;
  }

  public void Register(ISwiftlyCore core)
  {
    _core = core;
    _playerSpawnPreHook = core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawnPre);
    _playerSpawnPostHook = core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawnPost);
    _playerTeamPreHook = core.GameEvent.HookPre<EventPlayerTeam>(OnPlayerTeamPre);
    _playerDeathHook = core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
    _playerDisconnectHook = core.GameEvent.HookPost<EventPlayerDisconnect>(OnPlayerDisconnect);
    _playerHurtHook = core.GameEvent.HookPost<EventPlayerHurt>(OnPlayerHurt);
    _bombDefusedHook = core.GameEvent.HookPost<EventBombDefused>(OnBombDefused);
    _clientCommandHook = core.Command.HookClientCommand(OnClientCommand);
  }

  public void Unregister(ISwiftlyCore core)
  {
    if (_playerSpawnPreHook != Guid.Empty) core.GameEvent.Unhook(_playerSpawnPreHook);
    if (_playerSpawnPostHook != Guid.Empty) core.GameEvent.Unhook(_playerSpawnPostHook);
    if (_playerTeamPreHook != Guid.Empty) core.GameEvent.Unhook(_playerTeamPreHook);
    if (_playerDeathHook != Guid.Empty) core.GameEvent.Unhook(_playerDeathHook);
    if (_playerDisconnectHook != Guid.Empty) core.GameEvent.Unhook(_playerDisconnectHook);
    if (_playerHurtHook != Guid.Empty) core.GameEvent.Unhook(_playerHurtHook);
    if (_bombDefusedHook != Guid.Empty) core.GameEvent.Unhook(_bombDefusedHook);
    if (_clientCommandHook != Guid.Empty) core.Command.UnhookClientCommand(_clientCommandHook);
    _playerSpawnPreHook = Guid.Empty;
    _playerSpawnPostHook = Guid.Empty;
    _playerTeamPreHook = Guid.Empty;
    _playerDeathHook = Guid.Empty;
    _playerDisconnectHook = Guid.Empty;
    _playerHurtHook = Guid.Empty;
    _bombDefusedHook = Guid.Empty;
    _clientCommandHook = Guid.Empty;
    _core = null;
  }

  private HookResult OnClientCommand(int playerId, string commandLine)
  {
    // Check if this is a team-related command
    var cmd = commandLine.Trim().ToLowerInvariant();
    if (!cmd.StartsWith("jointeam") && !cmd.StartsWith("spectate"))
    {
      return HookResult.Continue;
    }

    var core = _core;
    if (core is null) return HookResult.Continue;

    // Allow during warmup
    var rules = core.EntitySystem.GetGameRules();
    if (rules is not null && rules.WarmupPeriod)
    {
      return HookResult.Continue;
    }

    // Get the player
    var player = core.PlayerManager.GetPlayer(playerId);
    if (player is null || !player.IsValid) return HookResult.Continue;

    var currentTeam = (Team)player.Controller.TeamNum;

    // Allow switching to spectator unconditionally
    if (cmd.StartsWith("spectate"))
    {
      return HookResult.Continue;
    }

    if (cmd.StartsWith("jointeam"))
    {
      var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
      // Only enforce queue when joining T (2) or CT (3). Allow team 0 (none/auto)
      // and team 1 (spectator) through unconditionally.
      if (parts.Length >= 2 && parts[1] != "2" && parts[1] != "3")
      {
        return HookResult.Continue;
      }

      // Player on T/CT trying to switch teams mid-round — block
      if (currentTeam == Team.T || currentTeam == Team.CT)
      {
        return HookResult.Stop;
      }

      // Player in spectator/none trying to join T/CT — enforce queue
      if (_config.Config.Queue.Enabled)
      {
        if (_queue.IsActive(player.SteamID))
        {
          // Already tracked as active, allow
          return HookResult.Continue;
        }

        if (_queue.ActiveCount >= _config.Config.Queue.MaxPlayers)
        {
          // Queue is full — send to queue, block the join
          _queue.OnPlayerJoinedTeam(player, currentTeam, Team.CT);
          return HookResult.Stop;
        }
      }
    }

    return HookResult.Continue;
  }

  private HookResult OnPlayerTeamPre(EventPlayerTeam @event)
  {
    // Allow programmatic team changes (e.g., team balance, queue moves)
    if (_state.TeamChangeBypassEnabled) return HookResult.Continue;

    var player = @event.UserIdPlayer;
    if (player is null || !player.IsValid) return HookResult.Continue;

    if (!_config.Config.Queue.Enabled) return HookResult.Continue;

    var fromTeam = (Team)player.Controller.TeamNum;
    var toTeam = (Team)@event.Team;

    // Only queue when the player is trying to join T or CT. Joining
    // None (0) or Spectator (1) should bypass queue logic entirely.
    if (toTeam != Team.T && toTeam != Team.CT) return HookResult.Continue;

    return _queue.OnPlayerJoinedTeam(player, fromTeam, toTeam);
  }

  private HookResult OnPlayerSpawnPre(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player is null || !player.IsValid) return HookResult.Continue;
    var isHuman = PlayerUtil.IsHuman(player);

    var core = _core;
    if (core is not null)
    {
      var rules = core.EntitySystem.GetGameRules();
      if (rules is not null && rules.WarmupPeriod)
      {
        return HookResult.Continue;
      }
    }

    // If the round is already live and this player wasn't in the round participants list,
    // keep them as spectator until next round to avoid default map spawns.
    if (_state.RoundLive && !_state.IsRoundParticipant(player.SteamID))
    {
      if (!isHuman)
      {
        return HookResult.Continue;
      }

      var team = (Team)player.Controller.TeamNum;
      if (team == Team.T || team == Team.CT)
      {
        if (core is not null)
        {
          var teamPlayers = core.PlayerManager.GetAllPlayers()
            .Where(p => p.IsValid)
            .ToList();
          var tCount = teamPlayers.Count(p => (Team)p.Controller.TeamNum == Team.T);
          var ctCount = teamPlayers.Count(p => (Team)p.Controller.TeamNum == Team.CT);

          // Special case: if the server is effectively 1v0, put the joiner on the empty team
          // and restart the round so everyone gets proper retake spawns.
          if ((tCount == 1 && ctCount == 0) || (tCount == 0 && ctCount == 1))
          {
            var targetTeam = ctCount == 0 ? Team.CT : Team.T;
            player.SwitchTeam(targetTeam);
            if (_state.TryQueueRestartThisRound())
            {
              core.Engine.ExecuteCommand("mp_restartgame 1");
            }

            return HookResult.Continue;
          }
        }

        _state.EnqueueJoiner(player.SteamID);
        if (player.Controller.PawnIsAlive && player.Pawn is not null)
        {
          player.Pawn.CommitSuicide(false, true);
        }
        player.ChangeTeam(Team.Spectator);
        return HookResult.Handled;
      }
    }

    // Prevent manual team switching mid-round: keep participants on their locked team.
    // Allow switching to spectator (voluntary spec).
    if (isHuman && _state.RoundLive && _state.TryGetLockedTeam(player.SteamID, out var lockedTeam))
    {
      var currentTeam = (Team)player.Controller.TeamNum;
      if (lockedTeam == Team.T || lockedTeam == Team.CT)
      {
        if (currentTeam != lockedTeam && currentTeam != Team.Spectator && currentTeam != Team.None)
        {
          core?.Scheduler.NextTick(() =>
          {
            if (player is null || !player.IsValid) return;
            if (!_state.RoundLive) return;
            if (!_state.TryGetLockedTeam(player.SteamID, out var stillLocked)) return;
            if (stillLocked != Team.T && stillLocked != Team.CT) return;
            var teamNow = (Team)player.Controller.TeamNum;
            if (teamNow == stillLocked || teamNow == Team.Spectator || teamNow == Team.None) return;
            player.SwitchTeam(stillLocked);
          });
        }
      }
    }

    return HookResult.Continue;
  }

  private HookResult OnPlayerSpawnPost(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player is null)
    {
      return HookResult.Continue;
    }
    _pawnLifecycle.OnPlayerSpawn(player);

    if (!_config.Config.Weapons.BuyMenuEnabled)
    {
      _core?.Scheduler.NextTick(() => {
        if (player is null || !player.IsValid) return;
        var money = player.Controller.InGameMoneyServices;
        if (money is not null)
        {
          money.Account = 0;
          money.AccountUpdated();
          money.StartAccount = 0;
          money.StartAccountUpdated();
        }
      });
    }

    if (player is not null && player.IsValid && PlayerUtil.IsHuman(player))
    {
      _soloBot.UpdateSoloBot();
    }

    // Strip helmet carried over from warmup or re-applied by the engine on pistol rounds
    if (_allocation.CurrentRoundType == RoundType.Pistol && !_config.Config.Allocation.PistolHelmet)
    {
      var p = player;
      _core?.Scheduler.NextTick(() =>
      {
        if (p is null || !p.IsValid) return;
        var pawn = p.Pawn;
        if (pawn is null || !pawn.IsValid) return;
        if (pawn.ItemServices is CCSPlayer_ItemServices svc && svc.HasHelmet)
        {
          svc.HasHelmet = false;
          svc.HasHelmetUpdated();
        }
      });
    }

    return HookResult.Continue;
  }

  private HookResult OnPlayerDeath(EventPlayerDeath @event)
  {
    var core = _core;
    if (core is not null)
    {
      var attackerId = @event.Attacker;
      if (attackerId > 0)
      {
        var attacker = core.PlayerManager.GetAllPlayers()
          .FirstOrDefault(p => p.IsValid && (p.PlayerID == attackerId || p.Slot == attackerId));
        if (attacker is not null)
        {
          _damageReport.OnPlayerKill(attacker);
        }
      }
    }

    _clutch.OnPlayerCountMayHaveChanged();
    return HookResult.Continue;
  }

  private HookResult OnBombDefused(EventBombDefused @event)
  {
    var defuser = @event.UserIdPlayer;
    if (defuser is not null && defuser.IsValid && defuser.SteamID != 0)
    {
      _damageReport.SetLastDefuser(defuser.SteamID);
    }

    return HookResult.Continue;
  }

  private HookResult OnPlayerHurt(EventPlayerHurt @event)
  {
    var victim = @event.UserIdPlayer;
    if (victim is null) return HookResult.Continue;

    var attacker = @event.AttackerPlayer;
    if (attacker is null || !attacker.IsValid) return HookResult.Continue;

    _damageReport.OnPlayerHurt(attacker, victim, @event.DmgHealth);
    return HookResult.Continue;
  }

  private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
  {
    _clutch.OnPlayerCountMayHaveChanged();

    var player = @event.UserIdPlayer;
    if (player is not null && player.IsValid)
    {
      if (!PlayerUtil.IsHuman(player))
      {
        _soloBot.UpdateSoloBot();
        return HookResult.Continue;
      }

      _state.OnPlayerLeft(player.SteamID);
      _prefs.Clear(player.SteamID);
      _queue.RemovePlayerFromQueues(player.SteamID);
      _soloBot.UpdateSoloBot();
    }

    return HookResult.Continue;
  }
}
