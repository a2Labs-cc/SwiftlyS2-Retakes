namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing the buy menu.
/// </summary>
public interface IBuyMenuService
{
  /// <summary>
  /// Initializes the buy menu service.
  /// </summary>
  void Initialize();

  /// <summary>
  /// Unregisters event hooks.
  /// </summary>
  void Unregister();

  /// <summary>
  /// Applies buy menu convars.
  /// </summary>
  void ApplyBuyMenuConvars();

  /// <summary>
  /// Called when a round starts.
  /// </summary>
  void OnRoundStart();

  /// <summary>
  /// Called when a round type is selected.
  /// </summary>
  void OnRoundTypeSelected();
}
