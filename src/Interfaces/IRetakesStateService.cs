using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing retakes game state.
/// </summary>
public interface IRetakesStateService
{
  bool RoundLive { get; }
  bool RestartQueuedThisRound { get; }
  Bombsite? ForcedBombsite { get; }
  Bombsite? ShowingSpawnsForBombsite { get; }
  bool SmokesForced { get; }
  bool ScrambleNextRound { get; set; }
  bool TeamChangeBypassEnabled { get; }
  int RoundNumber { get; }
  Team LastWinner { get; }
  byte LastWinReason { get; }
  string LastWinMessage { get; }
  int CtWins { get; }
  int TWins { get; }
  int ConsecutiveWins { get; }

  void ResetMatchState();
  void OnRoundStart(bool isWarmup);
  void SetRoundParticipants(IEnumerable<ulong> steamIds);
  void SetRoundParticipants(IEnumerable<(ulong SteamId, Team Team)> participants);
  bool TryGetLockedTeam(ulong steamId, out Team team);
  bool IsRoundParticipant(ulong steamId);
  void EnqueueJoiner(ulong steamId);
  List<ulong> DrainPendingJoiners();
  bool TryQueueRestartThisRound();
  void OnPlayerLeft(ulong steamId);
  void OnRoundEnd(Team winner, byte reason, string message);
  void BeginTeamChangeBypass();
  void EndTeamChangeBypass();
  void ForceBombsite(Bombsite bombsite);
  void ClearForcedBombsite();
  void ForceSmokes();
  void ClearForcedSmokes();
  void SetShowingSpawnsForBombsite(Bombsite? bombsite);
  bool ToggleVoices(ulong steamId);
  bool VoicesEnabled(ulong steamId);
}
