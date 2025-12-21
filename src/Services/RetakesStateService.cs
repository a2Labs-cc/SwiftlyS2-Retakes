using SwiftlyS2_Retakes.Models;
using SwiftlyS2.Shared.Players;
using System.Collections.Generic;
using System.Linq;
using SwiftlyS2_Retakes.Interfaces;

namespace SwiftlyS2_Retakes.Services;

public sealed class RetakesStateService : IRetakesStateService
{
  private readonly HashSet<ulong> _voicesDisabled = new();
  private readonly HashSet<ulong> _roundParticipants = new();
  private readonly HashSet<ulong> _pendingJoiners = new();
  private readonly Dictionary<ulong, Team> _lockedTeamByParticipant = new();
  private int _teamChangeBypassDepth;
  private bool _smokesForced;

  public bool RoundLive { get; private set; }
  public bool RestartQueuedThisRound { get; private set; }

  public Bombsite? ForcedBombsite { get; private set; }
  public Bombsite? ShowingSpawnsForBombsite { get; private set; }
  public bool SmokesForced => _smokesForced;
  public bool ScrambleNextRound { get; set; }

  public bool TeamChangeBypassEnabled => _teamChangeBypassDepth > 0;

  public int RoundNumber { get; private set; }
  public Team LastWinner { get; private set; } = Team.None;
  public byte LastWinReason { get; private set; }
  public string LastWinMessage { get; private set; } = string.Empty;
  public int CtWins { get; private set; }
  public int TWins { get; private set; }
  public int ConsecutiveWins { get; private set; }

  public void ResetMatchState()
  {
    RoundNumber = 0;
    LastWinner = Team.None;
    LastWinReason = 0;
    LastWinMessage = string.Empty;
    CtWins = 0;
    TWins = 0;
    ConsecutiveWins = 0;
    ScrambleNextRound = false;

    _smokesForced = false;

    RoundLive = false;
    RestartQueuedThisRound = false;
    _roundParticipants.Clear();
    _pendingJoiners.Clear();
    _lockedTeamByParticipant.Clear();
    _teamChangeBypassDepth = 0;
  }

  public void OnRoundStart(bool isWarmup)
  {
    if (isWarmup)
    {
      RoundLive = false;
      RestartQueuedThisRound = false;
      _roundParticipants.Clear();
      _lockedTeamByParticipant.Clear();
      return;
    }

    RoundNumber++;
    RoundLive = true;
    RestartQueuedThisRound = false;
  }

  public void SetRoundParticipants(IEnumerable<ulong> steamIds)
  {
    _roundParticipants.Clear();
    _lockedTeamByParticipant.Clear();
    foreach (var id in steamIds)
    {
      _roundParticipants.Add(id);
    }
  }

  public void SetRoundParticipants(IEnumerable<(ulong SteamId, Team Team)> participants)
  {
    _roundParticipants.Clear();
    _lockedTeamByParticipant.Clear();

    foreach (var (steamId, team) in participants)
    {
      _roundParticipants.Add(steamId);
      if (team == Team.T || team == Team.CT)
      {
        _lockedTeamByParticipant[steamId] = team;
      }
    }
  }

  public bool TryGetLockedTeam(ulong steamId, out Team team)
  {
    return _lockedTeamByParticipant.TryGetValue(steamId, out team);
  }

  public bool IsRoundParticipant(ulong steamId)
  {
    return _roundParticipants.Contains(steamId);
  }

  public void EnqueueJoiner(ulong steamId)
  {
    _pendingJoiners.Add(steamId);
  }

  public List<ulong> DrainPendingJoiners()
  {
    if (_pendingJoiners.Count == 0) return new List<ulong>();
    var list = _pendingJoiners.ToList();
    _pendingJoiners.Clear();
    return list;
  }

  public bool TryQueueRestartThisRound()
  {
    if (RestartQueuedThisRound) return false;
    RestartQueuedThisRound = true;
    return true;
  }

  public void OnPlayerLeft(ulong steamId)
  {
    _roundParticipants.Remove(steamId);
    _pendingJoiners.Remove(steamId);
    _voicesDisabled.Remove(steamId);
    _lockedTeamByParticipant.Remove(steamId);
  }

  public void OnRoundEnd(Team winner, byte reason, string message)
  {
    var prevWinner = LastWinner;

    if (winner == Team.None)
    {
      ConsecutiveWins = 0;
    }
    else if (prevWinner == winner)
    {
      ConsecutiveWins = Math.Max(1, ConsecutiveWins + 1);
    }
    else
    {
      ConsecutiveWins = 1;
    }

    LastWinner = winner;
    LastWinReason = reason;
    LastWinMessage = message;

    RoundLive = false;
    RestartQueuedThisRound = false;
    _roundParticipants.Clear();
    _lockedTeamByParticipant.Clear();

    _teamChangeBypassDepth = 0;

    if (winner == Team.CT) CtWins++;
    if (winner == Team.T) TWins++;
  }

  public void BeginTeamChangeBypass()
  {
    _teamChangeBypassDepth++;
  }

  public void EndTeamChangeBypass()
  {
    if (_teamChangeBypassDepth > 0) _teamChangeBypassDepth--;
  }

  public void ForceBombsite(Bombsite bombsite)
  {
    ForcedBombsite = bombsite;
  }

  public void ClearForcedBombsite()
  {
    ForcedBombsite = null;
  }

  public void ForceSmokes()
  {
    _smokesForced = true;
  }

  public void ClearForcedSmokes()
  {
    _smokesForced = false;
  }

  public void SetShowingSpawnsForBombsite(Bombsite? bombsite)
  {
    ShowingSpawnsForBombsite = bombsite;
  }

  public bool ToggleVoices(ulong steamId)
  {
    if (_voicesDisabled.Contains(steamId))
    {
      _voicesDisabled.Remove(steamId);
      return true;
    }

    _voicesDisabled.Add(steamId);
    return false;
  }

  public bool VoicesEnabled(ulong steamId)
  {
    return !_voicesDisabled.Contains(steamId);
  }
}
