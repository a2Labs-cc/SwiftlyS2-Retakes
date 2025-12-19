using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing player queue.
/// </summary>
public interface IQueueService
{
  IReadOnlySet<ulong> ActivePlayers { get; }
  IReadOnlySet<ulong> QueuePlayers { get; }
  int ActiveCount { get; }
  int QueueCount { get; }

  int GetTargetNumTerrorists();
  int GetTargetNumCounterTerrorists();

  bool IsActive(ulong steamId);
  bool IsQueued(ulong steamId);

  HookResult OnPlayerJoinedTeam(IPlayer player, Team fromTeam, Team toTeam);

  void Update();
  void RemovePlayerFromQueues(ulong steamId);
  void CheckRoundDone();
  void SetRoundTeams();
  void ClearRoundTeams();
  void Reset();

  string DebugSummary();
}
