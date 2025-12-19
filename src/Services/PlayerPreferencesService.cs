using Microsoft.Extensions.Logging;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class PlayerPreferencesService : IPlayerPreferencesService
{
  private readonly ILogger _logger;
  private readonly IRetakesConfigService _config;
  private readonly IDatabaseService _database;

  private readonly Dictionary<ulong, UserSettings> _cache = new();
  private readonly object _lock = new();

  private const string TableName = "retakes_user_settings";

  public PlayerPreferencesService(ILogger logger, IRetakesConfigService config, IDatabaseService database)
  {
    _logger = logger;
    _config = config;
    _database = database;
  }

  public void Initialize()
  {
    // Database schema is initialized by DatabaseService
    _database.InitializeSchema();
  }

  public void Clear(ulong steamId)
  {
    lock (_lock)
    {
      _cache.Remove(steamId);
    }
  }

  public bool WantsAwp(ulong steamId)
  {
    return GetOrCreate(steamId).WantsAwp;
  }

  public bool ToggleAwp(ulong steamId)
  {
    var row = GetOrCreate(steamId);
    row.WantsAwp = !row.WantsAwp;
    Save(row);
    return row.WantsAwp;
  }

  public bool WantsSsg08(ulong steamId)
  {
    return GetOrCreate(steamId).WantsSsg08;
  }

  public bool ToggleSsg08(ulong steamId)
  {
    var row = GetOrCreate(steamId);
    row.WantsSsg08 = !row.WantsSsg08;
    Save(row);
    return row.WantsSsg08;
  }

  public bool WantsAwpPriority(ulong steamId)
  {
    return GetOrCreate(steamId).WantsAwpPriority;
  }

  public bool ToggleAwpPriority(ulong steamId)
  {
    var row = GetOrCreate(steamId);
    row.WantsAwpPriority = !row.WantsAwpPriority;
    Save(row);
    return row.WantsAwpPriority;
  }

  public bool WantsSpawnMenu(ulong steamId)
  {
    return GetOrCreate(steamId).WantsCtSpawnMenu;
  }

  public bool ToggleSpawnMenu(ulong steamId)
  {
    var row = GetOrCreate(steamId);
    row.WantsCtSpawnMenu = !row.WantsCtSpawnMenu;
    Save(row);
    return row.WantsCtSpawnMenu;
  }

  public int? GetPreferredSpawn(ulong steamId, bool isCt, Bombsite bombsite)
  {
    var row = GetOrCreate(steamId);
    return (isCt, bombsite) switch
    {
      (true, Bombsite.A) => row.CtSpawnA,
      (true, Bombsite.B) => row.CtSpawnB,
      (false, Bombsite.A) => row.TSpawnA,
      (false, Bombsite.B) => row.TSpawnB,
      _ => null,
    };
  }

  public void SetPreferredSpawn(ulong steamId, bool isCt, Bombsite bombsite, int? spawnId)
  {
    var row = GetOrCreate(steamId);
    if (isCt)
    {
      if (bombsite == Bombsite.A) row.CtSpawnA = spawnId;
      else row.CtSpawnB = spawnId;
    }
    else
    {
      if (bombsite == Bombsite.A) row.TSpawnA = spawnId;
      else row.TSpawnB = spawnId;
    }
    Save(row);
  }

  public string? GetPistolPrimary(ulong steamId, bool isCt)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences) return row.TPistolPrimary;
    return isCt ? row.CtPistolPrimary : row.TPistolPrimary;
  }

  public void SetPistolPrimary(ulong steamId, bool isCt, string? weapon)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      row.TPistolPrimary = weapon;
      row.CtPistolPrimary = weapon;
    }
    else
    {
      if (isCt) row.CtPistolPrimary = weapon;
      else row.TPistolPrimary = weapon;
    }

    Save(row);
  }

  public (string? Primary, string? Secondary) GetHalfBuyPack(ulong steamId, bool isCt)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences) return (row.THalfPrimary, row.THalfSecondary);
    return isCt ? (row.CtHalfPrimary, row.CtHalfSecondary) : (row.THalfPrimary, row.THalfSecondary);
  }

  public void SetHalfBuyPrimary(ulong steamId, bool isCt, string? weapon)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      row.THalfPrimary = weapon;
      row.CtHalfPrimary = weapon;
    }
    else
    {
      if (isCt) row.CtHalfPrimary = weapon;
      else row.THalfPrimary = weapon;
    }
    Save(row);
  }

  public void SetHalfBuySecondary(ulong steamId, bool isCt, string? weapon)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      row.THalfSecondary = weapon;
      row.CtHalfSecondary = weapon;
    }
    else
    {
      if (isCt) row.CtHalfSecondary = weapon;
      else row.THalfSecondary = weapon;
    }
    Save(row);
  }

  public (string? Primary, string? Secondary) GetFullBuyPack(ulong steamId, bool isCt)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences) return (row.TFullPrimary, row.TFullSecondary);
    return isCt ? (row.CtFullPrimary, row.CtFullSecondary) : (row.TFullPrimary, row.TFullSecondary);
  }

  public void SetFullBuyPrimary(ulong steamId, bool isCt, string? weapon)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      row.TFullPrimary = weapon;
      row.CtFullPrimary = weapon;
    }
    else
    {
      if (isCt) row.CtFullPrimary = weapon;
      else row.TFullPrimary = weapon;
    }
    Save(row);
  }

  public void SetFullBuySecondary(ulong steamId, bool isCt, string? weapon)
  {
    var row = GetOrCreate(steamId);
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      row.TFullSecondary = weapon;
      row.CtFullSecondary = weapon;
    }
    else
    {
      if (isCt) row.CtFullSecondary = weapon;
      else row.TFullSecondary = weapon;
    }
    Save(row);
  }

  private UserSettings GetOrCreate(ulong steamId)
  {
    lock (_lock)
    {
      if (_cache.TryGetValue(steamId, out var cached)) return cached;
    }

    try
    {
      var row = _database.QuerySingleOrDefault<UserSettings>($"SELECT * FROM {TableName} WHERE steam_id=@steamId", new { steamId });

      if (row is null)
      {
        row = UserSettings.CreateDefault(steamId);
        _database.Execute($"INSERT INTO {TableName} (steam_id, updated_at, wants_awp, wants_ssg08, wants_awp_priority, wants_ct_spawn_menu) VALUES (@SteamId, @UpdatedAt, @WantsAwp, @WantsSsg08, @WantsAwpPriority, @WantsCtSpawnMenu)", row);
      }

      lock (_lock)
      {
        _cache[steamId] = row;
      }

      return row;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Retakes: failed to load preferences for steamId={SteamId}", steamId);
      var row = UserSettings.CreateDefault(steamId);
      lock (_lock)
      {
        _cache[steamId] = row;
      }
      return row;
    }
  }

  private void Save(UserSettings row)
  {
    row.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    lock (_lock)
    {
      _cache[row.SteamId] = row;
    }

    try
    {
      var affected = _database.Execute($@"
UPDATE {TableName}
SET
  updated_at=@UpdatedAt,
  wants_awp=@WantsAwp,
  wants_ssg08=@WantsSsg08,
  wants_awp_priority=@WantsAwpPriority,
  wants_ct_spawn_menu=@WantsCtSpawnMenu,
  t_spawn_a=@TSpawnA,
  t_spawn_b=@TSpawnB,
  ct_spawn_a=@CtSpawnA,
  ct_spawn_b=@CtSpawnB,
  t_pistol_primary=@TPistolPrimary,
  t_half_primary=@THalfPrimary,
  t_half_secondary=@THalfSecondary,
  t_full_primary=@TFullPrimary,
  t_full_secondary=@TFullSecondary,
  ct_pistol_primary=@CtPistolPrimary,
  ct_half_primary=@CtHalfPrimary,
  ct_half_secondary=@CtHalfSecondary,
  ct_full_primary=@CtFullPrimary,
  ct_full_secondary=@CtFullSecondary
WHERE steam_id=@SteamId", row);

      if (affected == 0)
      {
        _database.Execute($@"
INSERT INTO {TableName} (
  steam_id, updated_at, wants_awp, wants_ssg08, wants_awp_priority, wants_ct_spawn_menu,
  t_spawn_a, t_spawn_b, ct_spawn_a, ct_spawn_b,
  t_pistol_primary, t_half_primary, t_half_secondary, t_full_primary, t_full_secondary,
  ct_pistol_primary, ct_half_primary, ct_half_secondary, ct_full_primary, ct_full_secondary
) VALUES (
  @SteamId, @UpdatedAt, @WantsAwp, @WantsSsg08, @WantsAwpPriority, @WantsCtSpawnMenu,
  @TSpawnA, @TSpawnB, @CtSpawnA, @CtSpawnB,
  @TPistolPrimary, @THalfPrimary, @THalfSecondary, @TFullPrimary, @TFullSecondary,
  @CtPistolPrimary, @CtHalfPrimary, @CtHalfSecondary, @CtFullPrimary, @CtFullSecondary
)", row);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Retakes: failed to save preferences for steamId={SteamId}", row.SteamId);
    }
  }
}
