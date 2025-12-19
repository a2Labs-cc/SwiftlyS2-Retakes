namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for anti team flash protection.
/// </summary>
public interface IAntiTeamFlashService
{
  /// <summary>
  /// Registers event hooks.
  /// </summary>
  void Register();

  /// <summary>
  /// Unregisters event hooks.
  /// </summary>
  void Unregister();
}
