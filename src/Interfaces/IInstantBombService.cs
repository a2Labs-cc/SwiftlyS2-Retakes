namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for instant bomb plant and defuse.
/// </summary>
public interface IInstantBombService
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
