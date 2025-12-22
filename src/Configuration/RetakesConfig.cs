namespace SwiftlyS2_Retakes.Configuration;

/// <summary>
/// Root configuration for the Retakes plugin.
/// </summary>
public sealed class RetakesConfig
{
  public AllocationConfig Allocation { get; set; } = new();
  public PreferencesConfig Preferences { get; set; } = new();
  public WeaponsConfig Weapons { get; set; } = new();
  public BombConfig Bomb { get; set; } = new();
  public SmokeScenarioConfig SmokeScenarios { get; set; } = new();
  public TeamBalanceConfig TeamBalance { get; set; } = new();
  public InstantBombConfig InstantBomb { get; set; } = new();
  public AntiTeamFlashConfig AntiTeamFlash { get; set; } = new();
  public AnnouncementConfig Announcement { get; set; } = new();
  public SoloBotConfig SoloBot { get; set; } = new();
  public AfkManagerConfig AfkManager { get; set; } = new();
  public ServerConfig Server { get; set; } = new();
  public BreakerConfig Breaker { get; set; } = new();
  public QueueConfig Queue { get; set; } = new();
}
