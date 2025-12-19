namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for anti team flash protection.
/// </summary>
public sealed class AntiTeamFlashConfig
{
  public bool Enabled { get; set; } = true;
  public bool FlashOwner { get; set; } = true;
  public string AccessFlag { get; set; } = "";
}
