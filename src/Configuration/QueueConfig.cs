namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for player queue.
/// </summary>
public sealed class QueueConfig
{
  public bool Enabled { get; set; } = true;
  public int MaxPlayers { get; set; } = 9;
  public bool PreventTeamChangesMidRound { get; set; } = true;
  public bool ForceEvenTeamsWhenPlayerCountIsMultipleOf10 { get; set; } = true;
  public string QueuePriorityFlags { get; set; } = "permission:vip";
  public string QueueImmunityFlags { get; set; } = "";
  public bool ShouldRemoveSpectators { get; set; } = true;
}
