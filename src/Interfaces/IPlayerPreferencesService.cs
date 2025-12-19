using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for managing player preferences.
/// </summary>
public interface IPlayerPreferencesService
{
  void Initialize();
  void Clear(ulong steamId);

  bool WantsAwp(ulong steamId);
  bool ToggleAwp(ulong steamId);

  bool WantsSsg08(ulong steamId);
  bool ToggleSsg08(ulong steamId);

  bool WantsAwpPriority(ulong steamId);
  bool ToggleAwpPriority(ulong steamId);

  bool WantsSpawnMenu(ulong steamId);
  bool ToggleSpawnMenu(ulong steamId);

  int? GetPreferredSpawn(ulong steamId, bool isCt, Bombsite bombsite);
  void SetPreferredSpawn(ulong steamId, bool isCt, Bombsite bombsite, int? spawnId);

  string? GetPistolPrimary(ulong steamId, bool isCt);
  void SetPistolPrimary(ulong steamId, bool isCt, string? weapon);

  (string? Primary, string? Secondary) GetHalfBuyPack(ulong steamId, bool isCt);
  void SetHalfBuyPrimary(ulong steamId, bool isCt, string? weapon);
  void SetHalfBuySecondary(ulong steamId, bool isCt, string? weapon);

  (string? Primary, string? Secondary) GetFullBuyPack(ulong steamId, bool isCt);
  void SetFullBuyPrimary(ulong steamId, bool isCt, string? weapon);
  void SetFullBuySecondary(ulong steamId, bool isCt, string? weapon);
}
