namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for bomb mechanics.
/// </summary>
public sealed class BombConfig
{
  public bool AutoPlant { get; set; } = true;
  public bool EnforceNoC4 { get; set; } = true;
}
