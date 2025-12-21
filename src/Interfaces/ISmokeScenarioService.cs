using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for spawning smoke grenades at predefined positions.
/// </summary>
public interface ISmokeScenarioService
{
  IReadOnlyList<SmokeScenario> GetSmokeScenariosForBombsite(Bombsite bombsite);

  /// <summary>
  /// Spawns smoke grenades for the specified bombsite.
  /// </summary>
  /// <param name="bombsite">The bombsite to spawn smokes for</param>
  SmokeScenario? SpawnSmokesForBombsite(Bombsite bombsite);

  SmokeScenario? SpawnSmokeById(int smokeId);
}
