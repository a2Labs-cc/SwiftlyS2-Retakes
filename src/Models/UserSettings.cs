namespace SwiftlyS2_Retakes.Models;

/// <summary>
/// Database model for user settings/preferences.
/// </summary>
public sealed class UserSettings
{
  public ulong SteamId { get; set; }
  public long UpdatedAt { get; set; }
  public bool WantsAwp { get; set; }
  public bool WantsSsg08 { get; set; }
  public bool WantsAwpPriority { get; set; }
  public bool WantsCtSpawnMenu { get; set; }

  public int? TSpawnA { get; set; }
  public int? TSpawnB { get; set; }
  public int? CtSpawnA { get; set; }
  public int? CtSpawnB { get; set; }

  public string? TPistolPrimary { get; set; }
  public string? THalfPrimary { get; set; }
  public string? THalfSecondary { get; set; }
  public string? TFullPrimary { get; set; }
  public string? TFullSecondary { get; set; }

  public string? CtPistolPrimary { get; set; }
  public string? CtHalfPrimary { get; set; }
  public string? CtHalfSecondary { get; set; }
  public string? CtFullPrimary { get; set; }
  public string? CtFullSecondary { get; set; }

  /// <summary>
  /// Creates a new default UserSettings for the given steam ID.
  /// </summary>
  public static UserSettings CreateDefault(ulong steamId) => new()
  {
    SteamId = steamId,
    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    WantsAwp = false,
    WantsSsg08 = false,
    WantsAwpPriority = false,
    WantsCtSpawnMenu = false,
  };
}
