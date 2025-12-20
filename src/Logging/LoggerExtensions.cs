using Microsoft.Extensions.Logging;

namespace SwiftlyS2_Retakes.Logging;

/// <summary>
/// Extension helpers to gate plugin logs behind a toggle.
/// </summary>
public static class LoggerExtensions
{
  public static void LogPluginDebug(this ILogger logger, string message, params object?[] args)
  {
    if (LoggingToggle.DebugEnabled) logger.LogDebug(message, args);
  }

  public static void LogPluginDebug(this ILogger logger, Exception exception, string message, params object?[] args)
  {
    if (LoggingToggle.DebugEnabled) logger.LogDebug(exception, message, args);
  }

  public static void LogPluginInformation(this ILogger logger, string message, params object?[] args)
  {
    logger.LogInformation(message, args);
  }

  public static void LogPluginWarning(this ILogger logger, string message, params object?[] args)
  {
    logger.LogWarning(message, args);
  }

  public static void LogPluginWarning(this ILogger logger, Exception exception, string message, params object?[] args)
  {
    logger.LogWarning(exception, message, args);
  }

  public static void LogPluginError(this ILogger logger, string message, params object?[] args)
  {
    logger.LogError(message, args);
  }

  public static void LogPluginError(this ILogger logger, Exception exception, string message, params object?[] args)
  {
    logger.LogError(exception, message, args);
  }
}
