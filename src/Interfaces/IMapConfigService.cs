using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing map spawn configurations.
/// </summary>
public interface IMapConfigService
{
  /// <summary>
  /// Gets the currently loaded map name.
  /// </summary>
  string? LoadedMapName { get; }

  /// <summary>
  /// Gets the list of spawns for the current map.
  /// </summary>
  IReadOnlyList<Spawn> Spawns { get; }

  /// <summary>
  /// Resets the map configuration.
  /// </summary>
  void Reset();

  /// <summary>
  /// Loads the map configuration for the specified map.
  /// </summary>
  /// <param name="mapName">The map name to load</param>
  /// <returns>True if loaded successfully</returns>
  bool Load(string mapName);

  /// <summary>
  /// Saves the current map configuration.
  /// </summary>
  /// <returns>True if saved successfully</returns>
  bool Save();

  /// <summary>
  /// Gets a spawn by its ID.
  /// </summary>
  /// <param name="id">The spawn ID</param>
  /// <returns>The spawn or null if not found</returns>
  Spawn? GetSpawnById(int id);

  /// <summary>
  /// Adds a new spawn to the configuration.
  /// </summary>
  /// <param name="position">The spawn position</param>
  /// <param name="angle">The spawn angle</param>
  /// <param name="team">The team for this spawn</param>
  /// <param name="bombsite">The bombsite for this spawn</param>
  /// <param name="canBePlanter">Whether this spawn can be used by the planter</param>
  /// <returns>The ID of the new spawn</returns>
  int AddSpawn(Vector position, QAngle angle, Team team, Bombsite bombsite, bool canBePlanter);

  /// <summary>
  /// Removes a spawn by its ID.
  /// </summary>
  /// <param name="id">The spawn ID to remove</param>
  /// <returns>True if removed successfully</returns>
  bool RemoveSpawn(int id);

  /// <summary>
  /// Sets the name of a spawn.
  /// </summary>
  /// <param name="id">The spawn ID</param>
  /// <param name="name">The new name (null to clear)</param>
  /// <returns>True if set successfully</returns>
  bool SetSpawnName(int id, string? name);
}
