namespace SwiftlyS2_Retakes.Configuration;

public sealed class AfkManagerConfig
{
  public bool Enabled { get; set; } = false;

  public int IdleSecondsBeforeSpectator { get; set; } = 60;

  public int SpectatorSecondsBeforeKick { get; set; } = 60;

  public float MovementDistanceThreshold { get; set; } = 5.0f;

  public int CheckIntervalSeconds { get; set; } = 2;

  public string KickReason { get; set; } = "Kicked for being AFK";
}
