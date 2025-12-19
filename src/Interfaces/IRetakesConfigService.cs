using SwiftlyS2_Retakes.Configuration;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing retakes plugin configuration.
/// </summary>
public interface IRetakesConfigService
{
  /// <summary>
  /// Gets the path to the configuration file.
  /// </summary>
  string ConfigPath { get; }

  /// <summary>
  /// Gets the current configuration.
  /// </summary>
  RetakesConfig Config { get; }

  /// <summary>
  /// Loads or creates the configuration file.
  /// </summary>
  void LoadOrCreate();

  /// <summary>
  /// Saves the current configuration to disk.
  /// </summary>
  void Save();

  /// <summary>
  /// Applies configuration values to convars.
  /// </summary>
  /// <param name="restartGame">Whether to restart the game after applying</param>
  void ApplyToConvars(bool restartGame = false);
}
