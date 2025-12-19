using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing pawn lifecycle and deferred actions.
/// </summary>
public interface IPawnLifecycleService
{
  /// <summary>
  /// Called when a round is about to start.
  /// </summary>
  void OnRoundPrestart();

  /// <summary>
  /// Resets all pending actions.
  /// </summary>
  void Reset();

  /// <summary>
  /// Gets a debug summary of the current state.
  /// </summary>
  /// <returns>Debug summary string</returns>
  string DebugSummary();

  /// <summary>
  /// Executes an action when the player's pawn is ready.
  /// </summary>
  /// <param name="player">The player</param>
  /// <param name="action">The action to execute</param>
  void WhenPawnReady(IPlayer player, Action<IPlayer> action);

  /// <summary>
  /// Called when a player spawns.
  /// </summary>
  /// <param name="player">The player that spawned</param>
  void OnPlayerSpawn(IPlayer player);
}
