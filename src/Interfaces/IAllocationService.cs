using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for weapon allocation.
/// </summary>
public interface IAllocationService
{
  /// <summary>
  /// Gets the current round type.
  /// </summary>
  RoundType? CurrentRoundType { get; }

  /// <summary>
  /// Selects the round type for the current round.
  /// </summary>
  /// <returns>The selected round type</returns>
  RoundType SelectRoundType();

  /// <summary>
  /// Allocates weapons for all current players.
  /// </summary>
  /// <param name="pawnLifecycle">The pawn lifecycle service</param>
  void AllocateForCurrentPlayers(IPawnLifecycleService pawnLifecycle);
}
