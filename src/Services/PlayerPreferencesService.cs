using Cookies.Contract;
using Microsoft.Extensions.Logging;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class PlayerPreferencesService : IPlayerPreferencesService
{
  private readonly ILogger _logger;
  private readonly IRetakesConfigService _config;

  private IPlayerCookiesAPIv1? _cookies;

  private const string KeyWantsAwp = "retakes_wants_awp";
  private const string KeyWantsSsg08 = "retakes_wants_ssg08";
  private const string KeyWantsAwpPriority = "retakes_wants_awp_priority";
  private const string KeyWantsCtSpawnMenu = "retakes_wants_ct_spawn_menu";
  private const string KeyTSpawnA = "retakes_t_spawn_a";
  private const string KeyTSpawnB = "retakes_t_spawn_b";
  private const string KeyCtSpawnA = "retakes_ct_spawn_a";
  private const string KeyCtSpawnB = "retakes_ct_spawn_b";
  private const string KeyTPistolPrimary = "retakes_t_pistol_primary";
  private const string KeyTHalfPrimary = "retakes_t_half_primary";
  private const string KeyTHalfSecondary = "retakes_t_half_secondary";
  private const string KeyTFullPrimary = "retakes_t_full_primary";
  private const string KeyTFullSecondary = "retakes_t_full_secondary";
  private const string KeyCtPistolPrimary = "retakes_ct_pistol_primary";
  private const string KeyCtHalfPrimary = "retakes_ct_half_primary";
  private const string KeyCtHalfSecondary = "retakes_ct_half_secondary";
  private const string KeyCtFullPrimary = "retakes_ct_full_primary";
  private const string KeyCtFullSecondary = "retakes_ct_full_secondary";

  public PlayerPreferencesService(ILogger logger, IRetakesConfigService config)
  {
    _logger = logger;
    _config = config;
  }

  public void SetCookiesApi(IPlayerCookiesAPIv1 cookies)
  {
    _cookies = cookies;
  }

  public void Initialize()
  {
  }

  public void Clear(ulong steamId)
  {
  }

  public bool WantsAwp(ulong steamId) =>
    GetBool(steamId, KeyWantsAwp, false);

  public bool ToggleAwp(ulong steamId)
  {
    var val = !WantsAwp(steamId);
    SetBool(steamId, KeyWantsAwp, val);
    return val;
  }

  public bool WantsSsg08(ulong steamId) =>
    GetBool(steamId, KeyWantsSsg08, false);

  public bool ToggleSsg08(ulong steamId)
  {
    var val = !WantsSsg08(steamId);
    SetBool(steamId, KeyWantsSsg08, val);
    return val;
  }

  public bool WantsAwpPriority(ulong steamId) =>
    GetBool(steamId, KeyWantsAwpPriority, false);

  public bool ToggleAwpPriority(ulong steamId)
  {
    var val = !WantsAwpPriority(steamId);
    SetBool(steamId, KeyWantsAwpPriority, val);
    return val;
  }

  public bool WantsSpawnMenu(ulong steamId) =>
    GetBool(steamId, KeyWantsCtSpawnMenu, false);

  public bool ToggleSpawnMenu(ulong steamId)
  {
    var val = !WantsSpawnMenu(steamId);
    SetBool(steamId, KeyWantsCtSpawnMenu, val);
    return val;
  }

  public int? GetPreferredSpawn(ulong steamId, bool isCt, Bombsite bombsite)
  {
    var key = (isCt, bombsite) switch
    {
      (true, Bombsite.A) => KeyCtSpawnA,
      (true, Bombsite.B) => KeyCtSpawnB,
      (false, Bombsite.A) => KeyTSpawnA,
      _ => KeyTSpawnB,
    };
    return GetInt(steamId, key);
  }

  public void SetPreferredSpawn(ulong steamId, bool isCt, Bombsite bombsite, int? spawnId)
  {
    var key = (isCt, bombsite) switch
    {
      (true, Bombsite.A) => KeyCtSpawnA,
      (true, Bombsite.B) => KeyCtSpawnB,
      (false, Bombsite.A) => KeyTSpawnA,
      _ => KeyTSpawnB,
    };
    SetInt(steamId, key, spawnId);
  }

  public string? GetPistolPrimary(ulong steamId, bool isCt)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
      return GetString(steamId, KeyTPistolPrimary);
    return GetString(steamId, isCt ? KeyCtPistolPrimary : KeyTPistolPrimary);
  }

  public void SetPistolPrimary(ulong steamId, bool isCt, string? weapon)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      SetString(steamId, KeyTPistolPrimary, weapon);
      SetString(steamId, KeyCtPistolPrimary, weapon);
    }
    else
    {
      SetString(steamId, isCt ? KeyCtPistolPrimary : KeyTPistolPrimary, weapon);
    }
  }

  public (string? Primary, string? Secondary) GetHalfBuyPack(ulong steamId, bool isCt)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
      return (GetString(steamId, KeyTHalfPrimary), GetString(steamId, KeyTHalfSecondary));
    return isCt
      ? (GetString(steamId, KeyCtHalfPrimary), GetString(steamId, KeyCtHalfSecondary))
      : (GetString(steamId, KeyTHalfPrimary), GetString(steamId, KeyTHalfSecondary));
  }

  public void SetHalfBuyPrimary(ulong steamId, bool isCt, string? weapon)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      SetString(steamId, KeyTHalfPrimary, weapon);
      SetString(steamId, KeyCtHalfPrimary, weapon);
    }
    else
    {
      SetString(steamId, isCt ? KeyCtHalfPrimary : KeyTHalfPrimary, weapon);
    }
  }

  public void SetHalfBuySecondary(ulong steamId, bool isCt, string? weapon)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      SetString(steamId, KeyTHalfSecondary, weapon);
      SetString(steamId, KeyCtHalfSecondary, weapon);
    }
    else
    {
      SetString(steamId, isCt ? KeyCtHalfSecondary : KeyTHalfSecondary, weapon);
    }
  }

  public (string? Primary, string? Secondary) GetFullBuyPack(ulong steamId, bool isCt)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
      return (GetString(steamId, KeyTFullPrimary), GetString(steamId, KeyTFullSecondary));
    return isCt
      ? (GetString(steamId, KeyCtFullPrimary), GetString(steamId, KeyCtFullSecondary))
      : (GetString(steamId, KeyTFullPrimary), GetString(steamId, KeyTFullSecondary));
  }

  public void SetFullBuyPrimary(ulong steamId, bool isCt, string? weapon)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      SetString(steamId, KeyTFullPrimary, weapon);
      SetString(steamId, KeyCtFullPrimary, weapon);
    }
    else
    {
      SetString(steamId, isCt ? KeyCtFullPrimary : KeyTFullPrimary, weapon);
    }
  }

  public void SetFullBuySecondary(ulong steamId, bool isCt, string? weapon)
  {
    if (!_config.Config.Preferences.UsePerTeamPreferences)
    {
      SetString(steamId, KeyTFullSecondary, weapon);
      SetString(steamId, KeyCtFullSecondary, weapon);
    }
    else
    {
      SetString(steamId, isCt ? KeyCtFullSecondary : KeyTFullSecondary, weapon);
    }
  }

  private bool GetBool(ulong steamId, string key, bool defaultValue)
  {
    if (_cookies is null) return defaultValue;
    try { return _cookies.GetOrDefault<bool>((long)steamId, key, defaultValue); }
    catch (Exception ex) { _logger.LogError(ex, "Retakes: cookie read failed key={Key}", key); return defaultValue; }
  }

  private void SetBool(ulong steamId, string key, bool value)
  {
    if (_cookies is null) return;
    try { _cookies.Set((long)steamId, key, value); }
    catch (Exception ex) { _logger.LogError(ex, "Retakes: cookie write failed key={Key}", key); }
  }

  private int? GetInt(ulong steamId, string key)
  {
    if (_cookies is null) return null;
    try
    {
      if (!_cookies.Has((long)steamId, key)) return null;
      return _cookies.Get<int?>((long)steamId, key);
    }
    catch (Exception ex) { _logger.LogError(ex, "Retakes: cookie read failed key={Key}", key); return null; }
  }

  private void SetInt(ulong steamId, string key, int? value)
  {
    if (_cookies is null) return;
    try
    {
      if (value is null) _cookies.Unset((long)steamId, key);
      else _cookies.Set((long)steamId, key, value.Value);
    }
    catch (Exception ex) { _logger.LogError(ex, "Retakes: cookie write failed key={Key}", key); }
  }

  private string? GetString(ulong steamId, string key)
  {
    if (_cookies is null) return null;
    try
    {
      if (!_cookies.Has((long)steamId, key)) return null;
      return _cookies.Get<string>((long)steamId, key);
    }
    catch (Exception ex) { _logger.LogError(ex, "Retakes: cookie read failed key={Key}", key); return null; }
  }

  private void SetString(ulong steamId, string key, string? value)
  {
    if (_cookies is null) return;
    try
    {
      if (value is null) _cookies.Unset((long)steamId, key);
      else _cookies.Set((long)steamId, key, value);
    }
    catch (Exception ex) { _logger.LogError(ex, "Retakes: cookie write failed key={Key}", key); }
  }
}
