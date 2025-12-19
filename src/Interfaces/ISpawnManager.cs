using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing spawn assignments and teleportation.
/// </summary>
public interface ISpawnManager
{
  /// <summary>
  /// Sets the available spawns.
  /// </summary>
  /// <param name="spawns">The spawns to use</param>
  void SetSpawns(IEnumerable<Spawn> spawns);

  /// <summary>
  /// Handles spawn assignments for the current round.
  /// </summary>
  /// <param name="bombsite">The bombsite for this round</param>
  /// <returns>True if spawns were assigned successfully</returns>
  bool HandleRoundSpawns(Bombsite bombsite);

  /// <summary>
  /// Opens the CT spawn selection menu for eligible players.
  /// </summary>
  /// <param name="bombsite">The bombsite for this round</param>
  void OpenCtSpawnSelectionMenu(Bombsite bombsite);

  void CloseSpawnMenus();

  /// <summary>
  /// Tries to get the assigned planter for this round.
  /// </summary>
  /// <param name="steamId">The planter's steam ID</param>
  /// <param name="spawn">The planter's spawn</param>
  /// <returns>True if a planter was assigned</returns>
  bool TryGetAssignedPlanter(out ulong steamId, out Spawn spawn);
}
