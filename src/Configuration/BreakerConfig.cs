namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for map breaker (breakables and doors).
/// </summary>
public sealed class BreakerConfig
{
  public bool BreakBreakables { get; set; } = true;
  public bool OpenDoors { get; set; } = false;
}
