namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for anti team flash protection.
/// </summary>
public sealed class AntiTeamFlashConfig
{
  public bool Enabled { get; set; } = true;
  /// <summary>
  /// Allow the flash owner to be flashed by their own flash.
  /// false = protect owner from self-flash, true = allow owner to be flashed by their own flash
  /// </summary>
  public bool FlashOwner { get; set; } = false;
  public string AccessFlag { get; set; } = "";
}
