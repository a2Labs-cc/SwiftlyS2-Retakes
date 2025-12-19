namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Configuration for instant plant/defuse mechanics.
/// </summary>
public sealed class InstantBombConfig
{
  public bool InstaPlant { get; set; } = true;
  public bool InstaDefuse { get; set; } = true;
  public bool BlockDefuseIfTAlive { get; set; } = true;
  public bool BlockDefuseIfMollyNear { get; set; } = true;
  public float MollyRadius { get; set; } = 120f;
}
