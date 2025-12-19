using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
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

  private Guid _playerSpawnPreHook;
  private Guid _playerSpawnPostHook;
  private Guid _playerTeamPreHook;
  private Guid _playerDeathHook;
  private Guid _playerDisconnectHook;
  private Guid _clientCommandHook;
  private Guid _playerHurtHook;

  public PlayerEventHandlers(IPawnLifecycleService pawnLifecycle, IClutchAnnounceService clutch, IPlayerPreferencesService prefs, IRetakesStateService state, IRetakesConfigService config, IQueueService queue, IDamageReportService damageReport)
  {
    _pawnLifecycle = pawnLifecycle;
    _clutch = clutch;
    _prefs = prefs;
    _state = state;
    _config = config;
    _queue = queue;
    _damageReport = damageReport;
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
    if (_clientCommandHook != Guid.Empty) core.Command.UnhookClientCommand(_clientCommandHook);
    _playerSpawnPreHook = Guid.Empty;
    _playerSpawnPostHook = Guid.Empty;
    _playerTeamPreHook = Guid.Empty;
    _playerDeathHook = Guid.Empty;
    _playerDisconnectHook = Guid.Empty;
    _playerHurtHook = Guid.Empty;
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

    // Allow initial team selection (player not yet on T/CT)
    if (currentTeam != Team.T && currentTeam != Team.CT)
    {
      return HookResult.Continue;
    }

    // Block team switching for players already on T/CT outside warmup
    return HookResult.Stop;
  }

  private HookResult OnPlayerTeamPre(EventPlayerTeam @event)
  {
    // Team switching is blocked via OnClientCommand hook which uses HookResult.Stop
    // This handler is kept for any edge cases where team changes happen programmatically
    // without going through the command system
    
    // Allow programmatic team changes (e.g., team balance)
    if (_state.TeamChangeBypassEnabled) return HookResult.Continue;

    return HookResult.Continue;
  }

  private HookResult OnPlayerSpawnPre(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player is null || !player.IsValid) return HookResult.Continue;

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
            player.ChangeTeam(targetTeam);
            if (_state.TryQueueRestartThisRound())
            {
              core.Engine.ExecuteCommand("mp_restartgame 1");
            }

            return HookResult.Continue;
          }
        }

        _state.EnqueueJoiner(player.SteamID);
        player.ChangeTeam(Team.Spectator);
      }
    }

    // Prevent manual team switching mid-round: keep participants on their locked team.
    if (_state.RoundLive && _state.TryGetLockedTeam(player.SteamID, out var lockedTeam))
    {
      var currentTeam = (Team)player.Controller.TeamNum;
      if (lockedTeam == Team.T || lockedTeam == Team.CT)
      {
        if (currentTeam != lockedTeam)
        {
          core?.Scheduler.NextTick(() =>
          {
            if (player is null || !player.IsValid) return;
            if (!_state.RoundLive) return;
            if (!_state.TryGetLockedTeam(player.SteamID, out var stillLocked)) return;
            if (stillLocked != Team.T && stillLocked != Team.CT) return;
            if ((Team)player.Controller.TeamNum == stillLocked) return;
            player.ChangeTeam(stillLocked);
          });
        }
      }
    }

    return HookResult.Continue;
  }

  private HookResult OnPlayerSpawnPost(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    _pawnLifecycle.OnPlayerSpawn(player);
    return HookResult.Continue;
  }

  private HookResult OnPlayerDeath(EventPlayerDeath @event)
  {
    _clutch.OnPlayerCountMayHaveChanged();
    return HookResult.Continue;
  }

  private HookResult OnPlayerHurt(EventPlayerHurt @event)
  {
    var core = _core;
    if (core is null) return HookResult.Continue;

    var victim = @event.UserIdPlayer;
    if (victim is null) return HookResult.Continue;

    var attackerId = @event.Attacker;
    if (attackerId <= 0) return HookResult.Continue;

    var attacker = core.PlayerManager.GetAllPlayers()
      .FirstOrDefault(p => p.IsValid && (p.PlayerID == attackerId || p.Slot == attackerId));

    if (attacker is null) return HookResult.Continue;

    _damageReport.OnPlayerHurt(attacker, victim, @event.DmgHealth);
    return HookResult.Continue;
  }

  private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event)
  {
    _clutch.OnPlayerCountMayHaveChanged();

    var player = @event.UserIdPlayer;
    if (player is not null && player.IsValid)
    {
      _state.OnPlayerLeft(player.SteamID);
      _prefs.Clear(player.SteamID);
      _queue.RemovePlayerFromQueues(player.SteamID);
    }

    return HookResult.Continue;
  }
}
