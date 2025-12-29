namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for team balancing.
/// </summary>
public sealed class TeamBalanceConfig
{
  public bool Enabled { get; set; } = true;
  public float TerroristRatio { get; set; } = 0.45f;
  public bool ForceEvenWhenPlayersMod10 { get; set; } = true;
  public bool IncludeBots { get; set; } = false;

  public bool SkillBasedEnabled { get; set; } = true;

  public bool ScrambleEnabled { get; set; } = true;
  public int RoundsToScramble { get; set; } = 5;
}
