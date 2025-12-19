namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for player preferences.
/// </summary>
public sealed class PreferencesConfig
{
  public bool UsePerTeamPreferences { get; set; } = true;
  public string DatabaseConnectionName { get; set; } = "default";
}
