using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for announcing clutch situations.
/// </summary>
public interface IClutchAnnounceService
{
  /// <summary>
  /// Called when a round starts.
  /// </summary>
  void OnRoundStart();

  /// <summary>
  /// Called when player count may have changed (death, disconnect).
  /// </summary>
  void OnPlayerCountMayHaveChanged();

  /// <summary>
  /// Called when a round ends.
  /// </summary>
  /// <param name="winner">The winning team</param>
  void OnRoundEnd(Team winner);
}
