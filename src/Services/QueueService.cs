using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;

namespace SwiftlyS2_Retakes.Services;

public sealed class QueueService : IQueueService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IRetakesConfigService _config;
  private readonly IMessageService _messages;

  private readonly HashSet<ulong> _activePlayers = new();
  private readonly HashSet<ulong> _queuePlayers = new();
  private readonly HashSet<ulong> _roundTerrorists = new();
  private readonly HashSet<ulong> _roundCounterTerrorists = new();

  public IReadOnlySet<ulong> ActivePlayers => _activePlayers;
  public IReadOnlySet<ulong> QueuePlayers => _queuePlayers;

  public int ActiveCount => _activePlayers.Count;
  public int QueueCount => _queuePlayers.Count;

  public QueueService(ISwiftlyCore core, ILogger logger, IRetakesConfigService config, IMessageService messages)
  {
    _core = core;
    _logger = logger;
    _config = config;
    _messages = messages;
  }

  public int GetTargetNumTerrorists()
  {
    var cfg = _config.Config.Queue;
    var shouldForceEven = cfg.ForceEvenTeamsWhenPlayerCountIsMultipleOf10 && _activePlayers.Count % 10 == 0;
    var ratio = (shouldForceEven ? 0.5f : _config.Config.TeamBalance.TerroristRatio) * _activePlayers.Count;
    var numTerrorists = (int)MathF.Round(ratio);
    return numTerrorists > 0 ? numTerrorists : 1;
  }

  public int GetTargetNumCounterTerrorists()
  {
    return _activePlayers.Count - GetTargetNumTerrorists();
  }

  public bool IsActive(ulong steamId) => _activePlayers.Contains(steamId);
  public bool IsQueued(ulong steamId) => _queuePlayers.Contains(steamId);

  public HookResult OnPlayerJoinedTeam(IPlayer player, Team fromTeam, Team toTeam)
  {
    var steamId = player.SteamID;
    var cfg = _config.Config.Queue;

    _logger.LogDebug("QueueService: [{Name}] Team change: {From} -> {To}", player.Controller.PlayerName, fromTeam, toTeam);

    // Allow initial connection to spectator
    if (fromTeam == Team.None && toTeam == Team.Spectator)
    {
      return HookResult.Continue;
    }

    // Player is already active
    if (_activePlayers.Contains(steamId))
    {
      _logger.LogDebug("QueueService: [{Name}] Player is active", player.Controller.PlayerName);

      // Switching to spectator - remove from active
      if (toTeam == Team.Spectator)
      {
        _logger.LogInformation("QueueService: [{Name}] Switched to spectator", player.Controller.PlayerName);
        RemovePlayerFromQueues(steamId);
        return HookResult.Continue;
      }

      // Check for mid-round team change prevention
      var rules = _core.EntitySystem.GetGameRules();
      if (!cfg.PreventTeamChangesMidRound || (rules is not null && rules.WarmupPeriod))
      {
        return HookResult.Continue;
      }

      // Prevent switching to a team they weren't on at round start
      if (_roundTerrorists.Count > 0 && _roundCounterTerrorists.Count > 0)
      {
        var tryingToJoinCT = toTeam == Team.CT && !_roundCounterTerrorists.Contains(steamId);
        var tryingToJoinT = toTeam == Team.T && !_roundTerrorists.Contains(steamId);

        if (tryingToJoinCT || tryingToJoinT)
        {
          _logger.LogInformation("QueueService: [{Name}] Prevented mid-round team change", player.Controller.PlayerName);
          _activePlayers.Remove(steamId);
          _queuePlayers.Add(steamId);

          // Kill and move to spectator
          if (player.Controller.PawnIsAlive && player.Pawn is not null)
          {
            player.Pawn.CommitSuicide(false, true);
          }

          player.ChangeTeam(Team.Spectator);
          return HookResult.Handled;
        }
      }

      CheckRoundDone();
      return HookResult.Handled;
    }

    // Player is not active - check if we can add them
    if (!_queuePlayers.Contains(steamId))
    {
      var rules = _core.EntitySystem.GetGameRules();
      var isWarmup = rules is not null && rules.WarmupPeriod;

      // During warmup, add directly to active if there's room
      if (isWarmup && _activePlayers.Count < cfg.MaxPlayers)
      {
        _logger.LogInformation("QueueService: [{Name}] Added to active players (warmup)", player.Controller.PlayerName);
        _activePlayers.Add(steamId);
        return HookResult.Continue;
      }

      // Add to queue
      _logger.LogInformation("QueueService: [{Name}] Added to queue", player.Controller.PlayerName);
      var loc = _core.Translation.GetPlayerLocalizer(player);
      _messages.Chat(player, loc["queue.added"]);
      _queuePlayers.Add(steamId);
    }

    CheckRoundDone();
    return HookResult.Handled;
  }

  public void Update()
  {
    RemoveDisconnectedPlayers();

    var cfg = _config.Config.Queue;
    _logger.LogDebug("QueueService: Update: Max={Max}, Active={Active}, Queue={Queue}",
      cfg.MaxPlayers, _activePlayers.Count, _queuePlayers.Count);

    var playersToAdd = cfg.MaxPlayers - _activePlayers.Count;
    if (playersToAdd > 0 && _queuePlayers.Count > 0)
    {
      var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

      // Prioritize players with queue priority, then by slot (join order)
      var playersToAddList = _queuePlayers
        .Select(steamId => allPlayers.FirstOrDefault(p => p.SteamID == steamId))
        .Where(p => p is not null && p.IsValid)
        .OrderBy(p => HasQueuePriority(p!) ? 0 : 1)
        .ThenBy(p => p!.Slot)
        .Take(playersToAdd)
        .ToList();

      foreach (var player in playersToAddList)
      {
        if (player is null || !player.IsValid) continue;

        _queuePlayers.Remove(player.SteamID);
        _activePlayers.Add(player.SteamID);
        player.ChangeTeam(Team.CT);
        _logger.LogInformation("QueueService: Moved {Name} from queue to active", player.Controller.PlayerName);
      }
    }

    HandleQueuePriority();

    // Notify queued players
    if (_activePlayers.Count >= cfg.MaxPlayers && _queuePlayers.Count > 0)
    {
      var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
      foreach (var steamId in _queuePlayers)
      {
        var player = allPlayers.FirstOrDefault(p => p.SteamID == steamId);
        if (player is null || !player.IsValid) continue;

        var loc = _core.Translation.GetPlayerLocalizer(player);
        _messages.Chat(player, loc["queue.waiting", _activePlayers.Count, cfg.MaxPlayers]);
      }
    }
  }

  private void HandleQueuePriority()
  {
    var cfg = _config.Config.Queue;
    if (_activePlayers.Count != cfg.MaxPlayers) return;

    var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

    var vipQueuePlayers = _queuePlayers
      .Select(steamId => allPlayers.FirstOrDefault(p => p.SteamID == steamId))
      .Where(p => p is not null && p.IsValid && HasQueuePriority(p!))
      .ToList();

    if (vipQueuePlayers.Count == 0) return;

    foreach (var vipPlayer in vipQueuePlayers)
    {
      if (vipPlayer is null || !vipPlayer.IsValid) continue;

      // Find replaceable non-VIP players (newest first by slot)
      var replaceablePlayers = _activePlayers
        .Select(steamId => allPlayers.FirstOrDefault(p => p.SteamID == steamId))
        .Where(p => p is not null && p.IsValid && !HasQueuePriority(p!) && !HasQueueImmunity(p!))
        .OrderByDescending(p => p!.Slot)
        .ToList();

      if (replaceablePlayers.Count == 0)
      {
        _logger.LogDebug("QueueService: No replaceable players found");
        break;
      }

      var replaceablePlayer = replaceablePlayers.First()!;

      // Swap the players
      replaceablePlayer.ChangeTeam(Team.Spectator);
      _activePlayers.Remove(replaceablePlayer.SteamID);
      _queuePlayers.Add(replaceablePlayer.SteamID);
      var replaceableLoc = _core.Translation.GetPlayerLocalizer(replaceablePlayer);
      _messages.Chat(replaceablePlayer, replaceableLoc["queue.moved_out", vipPlayer.Controller.PlayerName]);

      _activePlayers.Add(vipPlayer.SteamID);
      _queuePlayers.Remove(vipPlayer.SteamID);
      vipPlayer.ChangeTeam(Team.CT);
      var vipLoc = _core.Translation.GetPlayerLocalizer(vipPlayer);
      _messages.Chat(vipPlayer, vipLoc["queue.moved_in", replaceablePlayer.Controller.PlayerName]);

      _logger.LogInformation("QueueService: VIP {Vip} replaced {Replaced}", vipPlayer.Controller.PlayerName, replaceablePlayer.Controller.PlayerName);
    }
  }

  private void RemoveDisconnectedPlayers()
  {
    var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
    var connectedSteamIds = allPlayers.Select(p => p.SteamID).ToHashSet();

    var disconnectedActive = _activePlayers.Where(id => !connectedSteamIds.Contains(id)).ToList();
    if (disconnectedActive.Count > 0)
    {
      _logger.LogDebug("QueueService: Removing {Count} disconnected active players", disconnectedActive.Count);
      foreach (var id in disconnectedActive)
      {
        _activePlayers.Remove(id);
        _roundTerrorists.Remove(id);
        _roundCounterTerrorists.Remove(id);
      }
    }

    var disconnectedQueue = _queuePlayers.Where(id => !connectedSteamIds.Contains(id)).ToList();
    if (disconnectedQueue.Count > 0)
    {
      _logger.LogDebug("QueueService: Removing {Count} disconnected queue players", disconnectedQueue.Count);
      foreach (var id in disconnectedQueue)
      {
        _queuePlayers.Remove(id);
      }
    }
  }

  public void RemovePlayerFromQueues(ulong steamId)
  {
    _activePlayers.Remove(steamId);
    _queuePlayers.Remove(steamId);
    _roundTerrorists.Remove(steamId);
    _roundCounterTerrorists.Remove(steamId);
    _logger.LogDebug("QueueService: Removed {SteamId} from all queues", steamId);
    CheckRoundDone();
  }

  public void CheckRoundDone()
  {
    var rules = _core.EntitySystem.GetGameRules();
    if (rules is null || rules.WarmupPeriod) return;

    var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
    
    var tCount = allPlayers.Count(p => (Team)p.Controller.TeamNum == Team.T && p.Controller.PawnIsAlive);
    var ctCount = allPlayers.Count(p => (Team)p.Controller.TeamNum == Team.CT && p.Controller.PawnIsAlive);

    if (tCount == 0 || ctCount == 0)
    {
      _logger.LogInformation("QueueService: CheckRoundDone - T:{T} CT:{CT}, terminating round", tCount, ctCount);
      
      // Determine winner based on who has players left
      var reason = ctCount == 0 ? RoundEndReason.TerroristsWin : RoundEndReason.CTsWin;
      
      try
      {
        rules.TerminateRound(reason, 0.1f);
      }
      catch (Exception ex)
      {
        _logger.LogWarning("QueueService: Failed to terminate round: {Error}", ex.Message);
        
        // Fallback: kill all remaining players to force round end
        foreach (var player in allPlayers.Where(p => p.Controller.PawnIsAlive && p.Pawn is not null))
        {
          player.Pawn!.CommitSuicide(false, true);
        }
      }
    }
  }

  public void SetRoundTeams()
  {
    var cfg = _config.Config.Queue;
    if (!cfg.PreventTeamChangesMidRound) return;

    _roundTerrorists.Clear();
    _roundCounterTerrorists.Clear();

    var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

    foreach (var steamId in _activePlayers)
    {
      var player = allPlayers.FirstOrDefault(p => p.SteamID == steamId);
      if (player is null || !player.IsValid) continue;

      var team = (Team)player.Controller.TeamNum;
      if (team == Team.T)
      {
        _roundTerrorists.Add(steamId);
      }
      else if (team == Team.CT)
      {
        _roundCounterTerrorists.Add(steamId);
      }
    }

    _logger.LogDebug("QueueService: Round teams set: {T} T, {CT} CT", _roundTerrorists.Count, _roundCounterTerrorists.Count);
  }

  public void ClearRoundTeams()
  {
    _roundTerrorists.Clear();
    _roundCounterTerrorists.Clear();
    _logger.LogDebug("QueueService: Round teams cleared");
  }

  public void Reset()
  {
    _activePlayers.Clear();
    _queuePlayers.Clear();
    _roundTerrorists.Clear();
    _roundCounterTerrorists.Clear();
    _logger.LogDebug("QueueService: Reset all queues");
  }

  private bool HasQueuePriority(IPlayer player)
  {
    var flags = _config.Config.Queue.QueuePriorityFlags;
    if (string.IsNullOrWhiteSpace(flags)) return false;

    var flagList = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var flag in flagList)
    {
      if (_core.Permission.PlayerHasPermission(player.SteamID, flag))
      {
        return true;
      }
    }

    return false;
  }

  private bool HasQueueImmunity(IPlayer player)
  {
    var flags = _config.Config.Queue.QueueImmunityFlags;
    if (string.IsNullOrWhiteSpace(flags))
    {
      // Fall back to priority flags if immunity not specified
      return HasQueuePriority(player);
    }

    var flagList = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var flag in flagList)
    {
      if (_core.Permission.PlayerHasPermission(player.SteamID, flag))
      {
        return true;
      }
    }

    return false;
  }

  public string DebugSummary()
  {
    var allPlayers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

    var activeNames = _activePlayers
      .Select(id => allPlayers.FirstOrDefault(p => p.SteamID == id)?.Controller?.PlayerName ?? id.ToString())
      .ToList();

    var queueNames = _queuePlayers
      .Select(id => allPlayers.FirstOrDefault(p => p.SteamID == id)?.Controller?.PlayerName ?? id.ToString())
      .ToList();

    return $"Active ({_activePlayers.Count}): [{string.Join(", ", activeNames)}] | Queue ({_queuePlayers.Count}): [{string.Join(", ", queueNames)}]";
  }
}
