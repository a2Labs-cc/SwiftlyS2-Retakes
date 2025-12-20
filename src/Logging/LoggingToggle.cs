namespace SwiftlyS2_Retakes.Logging;

/// <summary>
/// Global toggle used to gate all plugin log output.
/// </summary>
public static class LoggingToggle
{
  /// <summary>
  /// When false, suppresses debug-level Retakes plugin logs.
  /// </summary>
  public static bool DebugEnabled { get; set; } = false;
}
