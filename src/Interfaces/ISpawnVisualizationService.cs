using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for visualizing spawns with beams and labels.
/// </summary>
public interface ISpawnVisualizationService
{
  /// <summary>
  /// Shows spawns for the specified bombsite.
  /// </summary>
  /// <param name="spawns">The spawns to show</param>
  /// <param name="bombsite">The bombsite to filter by</param>
  void ShowSpawns(IEnumerable<Spawn> spawns, Bombsite bombsite);

  void ShowSpawnsAndSmokes(IEnumerable<Spawn> spawns, IEnumerable<SmokeScenario> smokes, Bombsite bombsite);

  /// <summary>
  /// Hides all spawn visualizations.
  /// </summary>
  void HideSpawns();
}
