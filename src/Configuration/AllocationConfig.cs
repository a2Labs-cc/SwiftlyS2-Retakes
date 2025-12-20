namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for weapon allocation.
/// </summary>
public sealed class AllocationConfig
{
  public bool Enabled { get; set; } = true;
  public string RoundType { get; set; } = "random";
  public int RoundTypePctPistol { get; set; } = 20;
  public int RoundTypePctHalf { get; set; } = 30;
  public int RoundTypePctFull { get; set; } = 50;

  public bool AwpEnabled { get; set; } = true;
  public int AwpPerTeam { get; set; } = 1;
  public bool AwpAllowEveryone { get; set; } = false;
  public int AwpLowPlayersThreshold { get; set; } = 4;
  public int AwpLowPlayersChance { get; set; } = 50;
  public int AwpLowPlayersVipChance { get; set; } = 60;

  public bool Ssg08Enabled { get; set; } = true;
  public int Ssg08PerTeam { get; set; } = 0;
  public bool Ssg08AllowEveryone { get; set; } = false;

  public string AwpPriorityFlag { get; set; } = "";
  public int AwpPriorityPct { get; set; } = 0;
}
