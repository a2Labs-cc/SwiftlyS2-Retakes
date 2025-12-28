namespace SwiftlyS2_Retakes.Configuration;

public sealed class SmokeScenarioConfig
{
  public bool Enabled { get; set; } = false;
  public bool RandomRoundsEnabled { get; set; } = true;
  public float RandomRoundChance { get; set; } = 0.25f;
}
