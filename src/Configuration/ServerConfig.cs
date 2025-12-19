namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for server settings.
/// </summary>
public sealed class ServerConfig
{
  public int FreezeTimeSeconds { get; set; } = 5;
  public string ChatPrefix { get; set; } = "Retakes |";
  public string ChatPrefixColor { get; set; } = "green";
}
