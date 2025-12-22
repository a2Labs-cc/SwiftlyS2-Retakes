using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
using SwiftlyS2_Retakes.Utils;

namespace SwiftlyS2_Retakes.Services;

public sealed class AfkManagerService : IAfkManagerService
{
  private const float MovementInputThreshold = 0.001f;
  private const long MillisecondsPerSecond = 1000L;
  private const int MinimumConfigValue = 1;

  private sealed class AfkState
  {
    public long LastActivityTickMs;

    public long? MovedToSpectatorTickMs;

    public bool WasMovedToSpectatorByPlugin;
  }

  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IRetakesConfigService _config;

  private readonly Dictionary<ulong, AfkState> _states = new();

  private bool _tickHandlerRegistered;
  private long _lastScanTickMs;

  public AfkManagerService(ISwiftlyCore core, ILogger logger, IRetakesConfigService config)
  {
    _core = core;
    _logger = logger;
    _config = config;
  }

  public void Register()
  {
    if (_tickHandlerRegistered) return;
    _core.Event.OnTick += OnTick;
    _core.Event.OnClientDisconnected += OnClientDisconnected;
    _core.Event.OnClientProcessUsercmds += OnClientProcessUsercmds;
    _tickHandlerRegistered = true;
  }

  public void Unregister()
  {
    if (!_tickHandlerRegistered) return;
    _core.Event.OnTick -= OnTick;
    _core.Event.OnClientDisconnected -= OnClientDisconnected;
    _core.Event.OnClientProcessUsercmds -= OnClientProcessUsercmds;
    _tickHandlerRegistered = false;
    _states.Clear();
  }

  private void OnClientProcessUsercmds(IOnClientProcessUsercmdsEvent @event)
  {
    try
    {
      var cfg = _config.Config.AfkManager;
      if (!cfg.Enabled) return;
      if (@event.Paused) return;

      var rules = _core.EntitySystem.GetGameRules();
      if (rules is not null && (rules.WarmupPeriod || rules.FreezePeriod)) return;

      var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
      if (player is null || !player.IsValid) return;
      if (!PlayerUtil.IsHuman(player)) return;

      var nowMs = Environment.TickCount64;
      var state = GetOrCreateAfkState(player.SteamID, nowMs);

      if (!HasPlayerInput(@event.Usercmds)) return;

      ResetAfkState(state, nowMs);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: AFK manager failed to process user commands");
    }
  }

  private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
  {
    try
    {
      var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
      if (player is null) return;
      _states.Remove(player.SteamID);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: AFK manager failed to handle client disconnect");
    }
  }

  private void OnTick()
  {
    try
    {
      var cfg = _config.Config.AfkManager;
      if (!cfg.Enabled)
      {
        _states.Clear();
        return;
      }

      var nowMs = Environment.TickCount64;
      var checkIntervalSec = Math.Max(MinimumConfigValue, cfg.CheckIntervalSeconds);
      
      if (nowMs - _lastScanTickMs < checkIntervalSec * MillisecondsPerSecond)
      {
        return;
      }

      _lastScanTickMs = nowMs;

      var idleBeforeSpecMs = Math.Max(MinimumConfigValue, cfg.IdleSecondsBeforeSpectator) * MillisecondsPerSecond;
      var specBeforeKickMs = Math.Max(MinimumConfigValue, cfg.SpectatorSecondsBeforeKick) * MillisecondsPerSecond;
      var kickReason = string.IsNullOrWhiteSpace(cfg.KickReason) ? "Kicked for being AFK" : cfg.KickReason.Trim();

      var rules = _core.EntitySystem.GetGameRules();
      var isWarmup = rules is not null && rules.WarmupPeriod;
      var isFreeze = rules is not null && rules.FreezePeriod;

      var players = _core.PlayerManager.GetAllPlayers()
        .Where(p => p is not null)
        .Where(PlayerUtil.IsHuman)
        .ToList();

      foreach (var player in players)
      {
        ProcessPlayerAfkStatus(player, nowMs, isWarmup, isFreeze, idleBeforeSpecMs, specBeforeKickMs, kickReason);
      }
    }
    catch (Exception ex)
    {
      _logger.LogPluginError(ex, "Retakes: AFK manager exception");
    }
  }

  private void ProcessPlayerAfkStatus(IPlayer player, long nowMs, bool isWarmup, bool isFreeze, 
    long idleBeforeSpecMs, long specBeforeKickMs, string kickReason)
  {
    var team = (Team)player.Controller.TeamNum;
    var state = GetOrCreateAfkState(player.SteamID, nowMs);

    if (IsSpectatorTeam(team))
    {
      HandleSpectatorPlayer(player, state, nowMs, specBeforeKickMs, kickReason);
      return;
    }

    if (isWarmup)
    {
      ResetAfkState(state, nowMs);
      return;
    }

    if (isFreeze)
    {
      return;
    }

    if (state.WasMovedToSpectatorByPlugin && state.MovedToSpectatorTickMs is not null)
    {
      if (nowMs - state.MovedToSpectatorTickMs.Value >= specBeforeKickMs)
      {
        TryKick(player, kickReason, force: true);
        return;
      }
    }

    if (nowMs - state.LastActivityTickMs >= idleBeforeSpecMs)
    {
      MoveToSpectator(player, nowMs);
    }
  }

  private void HandleSpectatorPlayer(IPlayer player, AfkState state, long nowMs, long specBeforeKickMs, string kickReason)
  {
    if (!state.WasMovedToSpectatorByPlugin) return;

    if (state.MovedToSpectatorTickMs is null)
    {
      state.MovedToSpectatorTickMs = nowMs;
    }

    if (nowMs - state.MovedToSpectatorTickMs.Value >= specBeforeKickMs)
    {
      TryKick(player, kickReason, force: true);
    }
  }

  private bool HasPlayerInput(IEnumerable<CSGOUserCmdPB> usercmds)
  {
    foreach (var cmd in usercmds)
    {
      if (cmd is null) continue;

      var buttons = cmd.Base.ButtonsPb;
      if (buttons.Buttonstate1 != 0 || buttons.Buttonstate2 != 0 || buttons.Buttonstate3 != 0)
      {
        return true;
      }

      if (MathF.Abs(cmd.Base.Forwardmove) > MovementInputThreshold || 
          MathF.Abs(cmd.Base.Leftmove) > MovementInputThreshold || 
          MathF.Abs(cmd.Base.Upmove) > MovementInputThreshold)
      {
        return true;
      }

      if (cmd.Base.Mousedx != 0 || cmd.Base.Mousedy != 0)
      {
        return true;
      }

      if (cmd.Base.Impulse != 0)
      {
        return true;
      }
    }

    return false;
  }

  private AfkState GetOrCreateAfkState(ulong steamId, long nowMs)
  {
    if (!_states.TryGetValue(steamId, out var state))
    {
      state = new AfkState
      {
        LastActivityTickMs = nowMs,
      };
      _states[steamId] = state;
    }

    return state;
  }

  private void ResetAfkState(AfkState state, long nowMs)
  {
    state.LastActivityTickMs = nowMs;
    state.MovedToSpectatorTickMs = null;
    state.WasMovedToSpectatorByPlugin = false;
  }

  private static bool IsSpectatorTeam(Team team)
  {
    return team == Team.Spectator || team == Team.None;
  }

  private void MoveToSpectator(IPlayer player, long nowMs)
  {
    try
    {
      if (player is null || !player.IsValid) return;

      var team = (Team)player.Controller.TeamNum;
      if (IsSpectatorTeam(team)) return;

      if (player.Controller.PawnIsAlive && player.Pawn is not null)
      {
        player.Pawn.CommitSuicide(false, true);
      }

      player.ChangeTeam(Team.Spectator);

      if (_states.TryGetValue(player.SteamID, out var state))
      {
        state.MovedToSpectatorTickMs = nowMs;
        state.WasMovedToSpectatorByPlugin = true;
      }

      _logger.LogPluginDebug("Retakes: AFK manager moved {Name} to spectator", player.Controller.PlayerName);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: AFK manager failed to move player to spectator");
    }
  }

  private void TryKick(IPlayer player, string reason, bool force)
  {
    try
    {
      if (player is null || !player.IsValid) return;

      if (!force)
      {
        var team = (Team)player.Controller.TeamNum;
        if (team != Team.Spectator && team != Team.None)
        {
          return;
        }
      }

      player.Kick(reason, ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED_IDLE);
      _logger.LogPluginInformation("Retakes: AFK manager kicked {Name} ({SteamId})", player.Controller.PlayerName, player.SteamID);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: AFK manager failed to kick player");
    }
  }
}
