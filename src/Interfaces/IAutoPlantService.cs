using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for automatic bomb planting.
/// </summary>
public interface IAutoPlantService
{
  /// <summary>
  /// Enforces the mp_give_player_c4 0 convar.
  /// </summary>
  void EnforceNoC4();

  /// <summary>
  /// Tries to auto-plant the bomb.
  /// </summary>
  /// <param name="bombsite">The bombsite to plant at</param>
  /// <param name="assignedPlanterSteamId">Optional assigned planter steam ID</param>
  /// <param name="assignedPlanterSpawn">Optional assigned planter spawn</param>
  void TryAutoPlant(Bombsite bombsite, ulong? assignedPlanterSteamId = null, Spawn? assignedPlanterSpawn = null);
}
