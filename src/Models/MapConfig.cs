namespace SwiftlyS2_Retakes.Models;

public sealed class MapConfig
{
  public List<Spawn> Spawns { get; set; } = new();
  public List<SmokeScenario> SmokeScenarios { get; set; } = new();
}
