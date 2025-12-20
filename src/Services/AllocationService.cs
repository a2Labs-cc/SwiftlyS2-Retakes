using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Configuration;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class AllocationService : IAllocationService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly Random _random;
  private readonly IPlayerPreferencesService _prefs;
  private readonly IRetakesConfigService _config;

  public RoundType? CurrentRoundType { get; private set; }

  private bool _stripWeaponsDisabled;

  private readonly IConVar<bool> _enabled;
  private readonly IConVar<string> _roundType;
  private readonly IConVar<int> _pistolPct;
  private readonly IConVar<int> _halfBuyPct;
  private readonly IConVar<int> _fullBuyPct;

  private readonly IConVar<bool> _awpEnabled;
  private readonly IConVar<int> _awpPerTeam;
  private readonly IConVar<bool> _awpAllowEveryone;
  private readonly IConVar<int> _awpLowPlayersThreshold;
  private readonly IConVar<int> _awpLowPlayersChance;
  private readonly IConVar<int> _awpLowPlayersVipChance;

  private readonly IConVar<bool> _ssg08Enabled;
  private readonly IConVar<int> _ssg08PerTeam;
  private readonly IConVar<bool> _ssg08AllowEveryone;

  private readonly IConVar<string> _awpPriorityFlag;
  private readonly IConVar<int> _awpPriorityPct;

  private readonly IConVar<bool> _stripWeapons;
  private readonly IConVar<bool> _givePistolOnRifleRounds;
  private readonly IConVar<bool> _stripRemove;

  public AllocationService(ISwiftlyCore core, ILogger logger, Random random, IPlayerPreferencesService prefs, IRetakesConfigService config)
  {
    _core = core;
    _logger = logger;
    _random = random;
    _prefs = prefs;
    _config = config;

    _enabled = core.ConVar.CreateOrFind("retakes_allocation_enabled", "Enable weapon allocation", true);
    _roundType = core.ConVar.CreateOrFind("retakes_round_type", "Round type: random|pistol|half|full", "random");

    _pistolPct = core.ConVar.CreateOrFind("retakes_round_type_pct_pistol", "Random round type pct: pistol", 20, 0, 100);
    _halfBuyPct = core.ConVar.CreateOrFind("retakes_round_type_pct_half", "Random round type pct: half", 30, 0, 100);
    _fullBuyPct = core.ConVar.CreateOrFind("retakes_round_type_pct_full", "Random round type pct: full", 50, 0, 100);

    _awpEnabled = core.ConVar.CreateOrFind("retakes_allocation_awp_enabled", "Enable AWP preference allocation on FullBuy", true);
    _awpPerTeam = core.ConVar.CreateOrFind("retakes_allocation_awp_per_team", "Number of AWPs per team on FullBuy", 1, 0, 5);
    _awpAllowEveryone = core.ConVar.CreateOrFind("retakes_allocation_awp_allow_everyone", "Ignore player preference and allow everyone to receive AWP", false);
    _awpLowPlayersThreshold = core.ConVar.CreateOrFind("retakes_allocation_awp_low_players_threshold", "Number of players on a team to consider 'low population'", 4, 0, 64);
    _awpLowPlayersChance = core.ConVar.CreateOrFind("retakes_allocation_awp_low_players_chance", "Chance (0-100) to allocate AWP when player count is low", 50, 0, 100);
    _awpLowPlayersVipChance = core.ConVar.CreateOrFind("retakes_allocation_awp_low_players_vip_chance", "Chance (0-100) to allocate AWP when player count is low and an eligible VIP is present", 60, 0, 100);

    _ssg08Enabled = core.ConVar.CreateOrFind("retakes_allocation_ssg08_enabled", "Enable SSG08 preference allocation on FullBuy", true);
    _ssg08PerTeam = core.ConVar.CreateOrFind("retakes_allocation_ssg08_per_team", "Number of SSG08s per team on FullBuy", 0, 0, 5);
    _ssg08AllowEveryone = core.ConVar.CreateOrFind("retakes_allocation_ssg08_allow_everyone", "Ignore player preference and allow everyone to receive SSG08", false);

    _awpPriorityFlag = core.ConVar.CreateOrFind("retakes_allocation_awp_priority_flag", "Permission flag eligible for AWP priority (empty=disabled)", "");
    _awpPriorityPct = core.ConVar.CreateOrFind("retakes_allocation_awp_priority_pct", "Chance (0-100) to pick a priority player for each AWP slot", 0, 0, 100);

    _stripWeapons = core.ConVar.CreateOrFind("retakes_allocation_strip_weapons", "Drop existing weapons before giving loadout (prevents duplicates)", true);
    _givePistolOnRifleRounds = core.ConVar.CreateOrFind("retakes_allocation_give_pistol_on_rifle_rounds", "Give a configured pistol on half/full buy rounds", true);
    _stripRemove = core.ConVar.CreateOrFind("retakes_allocation_strip_remove", "Remove weapons instead of dropping them (recommended; keeps ground clean)", true);
  }

  public RoundType SelectRoundType()
  {
    var mode = (_roundType.Value ?? string.Empty).Trim();
    if (mode.Equals("pistol", StringComparison.OrdinalIgnoreCase) || mode.Equals("p", StringComparison.OrdinalIgnoreCase)) return RoundType.Pistol;
    if (mode.Equals("half", StringComparison.OrdinalIgnoreCase) || mode.Equals("halfbuy", StringComparison.OrdinalIgnoreCase) || mode.Equals("h", StringComparison.OrdinalIgnoreCase)) return RoundType.HalfBuy;
    if (mode.Equals("full", StringComparison.OrdinalIgnoreCase) || mode.Equals("fullbuy", StringComparison.OrdinalIgnoreCase) || mode.Equals("f", StringComparison.OrdinalIgnoreCase)) return RoundType.FullBuy;

    var pistol = Math.Clamp(_pistolPct.Value, 0, 100);
    var half = Math.Clamp(_halfBuyPct.Value, 0, 100);
    var full = Math.Clamp(_fullBuyPct.Value, 0, 100);

    var total = pistol + half + full;
    if (total <= 0) return RoundType.FullBuy;

    var roll = _random.Next(0, total);
    if (roll < pistol) return RoundType.Pistol;
    if (roll < pistol + half) return RoundType.HalfBuy;
    return RoundType.FullBuy;
  }

  public void AllocateForCurrentPlayers(IPawnLifecycleService pawnLifecycle)
  {
    var roundType = SelectRoundType();
    CurrentRoundType = roundType;

    if (!_enabled.Value) return;

    var players = _core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid && ((Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT))
      .ToList();

    var ct = players.Where(p => (Team)p.Controller.TeamNum == Team.CT).ToList();
    var t = players.Where(p => (Team)p.Controller.TeamNum == Team.T).ToList();

    var awpReceivers = new HashSet<ulong>();
    var ssg08Receivers = new HashSet<ulong>();
    if (roundType == RoundType.FullBuy && _awpEnabled.Value)
    {
      foreach (var steamId in PickAwpReceivers(ct)) awpReceivers.Add(steamId);
      foreach (var steamId in PickAwpReceivers(t)) awpReceivers.Add(steamId);
    }

    if (roundType == RoundType.FullBuy && _ssg08Enabled.Value)
    {
      foreach (var steamId in PickSsg08Receivers(ct, awpReceivers)) ssg08Receivers.Add(steamId);
      foreach (var steamId in PickSsg08Receivers(t, awpReceivers)) ssg08Receivers.Add(steamId);
    }

    var pistolDefuserSlot = -1;
    if (roundType == RoundType.Pistol && ct.Count > 0)
    {
      pistolDefuserSlot = ct[_random.Next(ct.Count)].Slot;
    }

    foreach (var p in players)
    {
      pawnLifecycle.WhenPawnReady(p, _ =>
      {
        try
        {
          GiveLoadout(p, roundType, pistolDefuserSlot, awpReceivers, ssg08Receivers);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Retakes: allocation failed for slot={Slot}", p.Slot);
        }
      });
    }
  }

  private void GiveLoadout(IPlayer player, RoundType roundType, int pistolDefuserSlot, HashSet<ulong> awpReceivers, HashSet<ulong> ssg08Receivers)
  {
    var pawn = player.Pawn;
    if (pawn is null || !pawn.IsValid) return;

    var itemServices = pawn.ItemServices;
    if (itemServices is null) return;

    var team = (Team)player.Controller.TeamNum;

    if (_stripWeapons.Value && !_stripWeaponsDisabled)
    {
      TryStripWeapons(pawn);
    }

    if (roundType == RoundType.Pistol)
    {
      itemServices.GiveItem("item_kevlar");
    }
    else
    {
      itemServices.GiveItem("item_assaultsuit");
    }

    if (team == Team.CT)
    {
      if (roundType != RoundType.Pistol || player.Slot == pistolDefuserSlot)
      {
        if (!player.Controller.PawnHasDefuser)
        {
          itemServices.GiveItem("item_defuser");
        }
      }
    }

    string? primary;
    if (roundType == RoundType.FullBuy && awpReceivers.Contains(player.SteamID))
    {
      primary = "weapon_awp";
    }
    else if (roundType == RoundType.FullBuy && ssg08Receivers.Contains(player.SteamID))
    {
      primary = "weapon_ssg08";
    }
    else
    {
      primary = SelectPrimary(team, roundType, player.SteamID);
    }
    if (primary is not null)
    {
      itemServices.GiveItem(primary);
    }

    if (roundType != RoundType.Pistol && _givePistolOnRifleRounds.Value)
    {
      var secondary = SelectSecondary(team, roundType, player.SteamID);
      if (secondary is not null)
      {
        itemServices.GiveItem(secondary);
      }
    }

    foreach (var nade in SelectUtil(team, roundType))
    {
      itemServices.GiveItem(nade);
    }
  }

  private void TryStripWeapons(CBasePlayerPawn pawn)
  {
    try
    {
      var weaponServices = pawn.WeaponServices;
      if (weaponServices is null) return;

      // Preserve knife (including custom knives) and avoid touching C4.
      // Use slot-based APIs so we don't have to read ref-return MyWeapons directly (binary mismatch risk).
      if (_stripRemove.Value)
      {
        weaponServices.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_RIFLE);
        weaponServices.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_PISTOL);
        weaponServices.RemoveWeaponBySlot(gear_slot_t.GEAR_SLOT_GRENADES);
      }
      else
      {
        weaponServices.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_RIFLE);
        weaponServices.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_PISTOL);
        weaponServices.DropWeaponBySlot(gear_slot_t.GEAR_SLOT_GRENADES);
      }
    }
    catch (Exception ex)
    {
      _stripWeaponsDisabled = true;
      _logger.LogError(ex, "Retakes: weapon strip failed; disabling stripping for this session (set retakes_allocation_strip_weapons 0 to silence)");
    }
  }

  private static bool IsKnifeDesignerName(string designerName)
  {
    // Covers weapon_knife, weapon_knife_t, and most custom knife names.
    if (designerName.Contains("knife", StringComparison.OrdinalIgnoreCase)) return true;
    if (designerName.Contains("bayonet", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
  }

  private string? SelectPrimary(Team team, RoundType roundType, ulong steamId)
  {
    if (roundType == RoundType.Pistol)
    {
      var allowed = _config.Config.Weapons.Pistols;
      var preferred = _prefs.GetPistolPrimary(steamId, team == Team.CT);
      return PreferOrRandom(preferred, allowed);
    }

    if (roundType == RoundType.HalfBuy)
    {
      var allowed = GetAllowedPrimaryWeapons(roundType, team);
      var pack = _prefs.GetHalfBuyPack(steamId, team == Team.CT);
      return PreferOrRandom(pack.Primary, allowed);
    }

    // FullBuy
    var fullAllowed = GetAllowedPrimaryWeapons(roundType, team);
    var fullPack = _prefs.GetFullBuyPack(steamId, team == Team.CT);
    return PreferOrRandom(fullPack.Primary, fullAllowed);
  }

  private string? SelectSecondary(Team team, RoundType roundType, ulong steamId)
  {
    // All round types use the shared pistols list for secondary
    var allowed = _config.Config.Weapons.Pistols;

    if (roundType == RoundType.HalfBuy)
    {
      var pack = _prefs.GetHalfBuyPack(steamId, team == Team.CT);
      return PreferOrRandom(pack.Secondary, allowed);
    }

    if (roundType == RoundType.FullBuy)
    {
      var pack = _prefs.GetFullBuyPack(steamId, team == Team.CT);
      return PreferOrRandom(pack.Secondary, allowed);
    }

    return null;
  }

  private List<string> GetAllowedPrimaryWeapons(RoundType roundType, Team team)
  {
    RoundWeaponsConfig? roundCfg = roundType switch
    {
      RoundType.HalfBuy => _config.Config.Weapons.HalfBuy,
      RoundType.FullBuy => _config.Config.Weapons.FullBuy,
      _ => null,
    };

    if (roundCfg is null) return _config.Config.Weapons.Pistols;

    var teamList = team == Team.CT ? roundCfg.Ct : roundCfg.T;
    if (teamList.Count > 0) return teamList;
    return roundCfg.All.Count > 0 ? roundCfg.All : _config.Config.Weapons.Pistols;
  }

  private IEnumerable<ulong> PickAwpReceivers(List<IPlayer> players)
  {
    var perTeam = Math.Clamp(_awpPerTeam.Value, 0, 10);
    if (perTeam <= 0) return Array.Empty<ulong>();

    var lowThreshold = Math.Clamp(_awpLowPlayersThreshold.Value, 0, 64);
    var lowChance = Math.Clamp(_awpLowPlayersChance.Value, 0, 100);
    var lowVipChance = Math.Clamp(_awpLowPlayersVipChance.Value, 0, 100);

    if (players.Count <= lowThreshold)
    {
      var vipRequiredFlag = (_awpPriorityFlag.Value ?? string.Empty).Trim();
      var vipEligible = false;
      if (!string.IsNullOrWhiteSpace(vipRequiredFlag))
      {
        foreach (var p in players)
        {
          if (!_prefs.WantsAwpPriority(p.SteamID)) continue;
          try
          {
            if (_core.Permission.PlayerHasPermission(p.SteamID, vipRequiredFlag))
            {
              vipEligible = true;
              break;
            }
          }
          catch
          {
            // ignore
          }
        }
      }

      var chance = vipEligible ? lowVipChance : lowChance;
      if (_random.Next(0, 100) >= chance)
      {
        return Array.Empty<ulong>();
      }
    }

    var candidates = _awpAllowEveryone.Value
      ? players
      : players.Where(p => _prefs.WantsAwp(p.SteamID)).ToList();

    if (candidates.Count == 0) return Array.Empty<ulong>();

    if (candidates.Count <= perTeam) return candidates.Select(p => p.SteamID).ToList();

    var selected = new List<ulong>(perTeam);
    var pool = candidates.ToList();

    var requiredFlag = (_awpPriorityFlag.Value ?? string.Empty).Trim();
    var pct = Math.Clamp(_awpPriorityPct.Value, 0, 100);

    for (var i = 0; i < perTeam && pool.Count > 0; i++)
    {
      List<IPlayer>? priorityPool = null;
      if (!string.IsNullOrWhiteSpace(requiredFlag) && pct > 0)
      {
        priorityPool = pool.Where(p =>
        {
          if (!_prefs.WantsAwpPriority(p.SteamID)) return false;
          try
          {
            return _core.Permission.PlayerHasPermission(p.SteamID, requiredFlag);
          }
          catch
          {
            return false;
          }
        }).ToList();
      }

      var usePriority = priorityPool is not null && priorityPool.Count > 0 && _random.Next(0, 100) < pct;
      var source = usePriority ? priorityPool! : pool;

      var idx = _random.Next(source.Count);
      var picked = source[idx];
      selected.Add(picked.SteamID);
      pool.RemoveAll(p => p.SteamID == picked.SteamID);
    }

    return selected;
  }

  private IEnumerable<ulong> PickSsg08Receivers(List<IPlayer> players, HashSet<ulong> excluded)
  {
    var perTeam = Math.Clamp(_ssg08PerTeam.Value, 0, 10);
    if (perTeam <= 0) return Array.Empty<ulong>();

    var basePool = players.Where(p => !excluded.Contains(p.SteamID)).ToList();
    if (basePool.Count == 0) return Array.Empty<ulong>();

    var candidates = _ssg08AllowEveryone.Value
      ? basePool
      : basePool.Where(p => _prefs.WantsSsg08(p.SteamID)).ToList();

    if (candidates.Count == 0) return Array.Empty<ulong>();
    if (candidates.Count <= perTeam) return candidates.Select(p => p.SteamID).ToList();

    var selected = new List<ulong>(perTeam);
    var pool = candidates.ToList();
    for (var i = 0; i < perTeam && pool.Count > 0; i++)
    {
      var idx = _random.Next(pool.Count);
      selected.Add(pool[idx].SteamID);
      pool.RemoveAt(idx);
    }

    return selected;
  }

  private string? PreferOrRandom(string? preferred, IReadOnlyList<string> allowed)
  {
    if (!string.IsNullOrWhiteSpace(preferred) && IsAllowed(preferred, allowed))
    {
      return preferred;
    }

    return allowed.Count == 0 ? null : allowed[_random.Next(allowed.Count)];
  }

  private static bool IsAllowed(string weapon, IReadOnlyList<string> allowed)
  {
    for (var i = 0; i < allowed.Count; i++)
    {
      if (string.Equals(allowed[i], weapon, StringComparison.OrdinalIgnoreCase)) return true;
    }

    return false;
  }

  private IEnumerable<string> SelectUtil(Team team, RoundType roundType)
  {
    if (roundType == RoundType.Pistol)
    {
      return _random.Next(0, 2) == 0
        ? new[] { "weapon_flashbang" }
        : new[] { "weapon_smokegrenade" };
    }

    var extraPool = team == Team.T
      ? new List<string> { "weapon_flashbang", "weapon_hegrenade", "weapon_molotov" }
      : new List<string> { "weapon_flashbang", "weapon_hegrenade", "weapon_incgrenade" };

    var result = new List<string> { "weapon_smokegrenade" };

    if (extraPool.Count > 0)
    {
      var first = extraPool[_random.Next(extraPool.Count)];
      result.Add(first);
      extraPool.Remove(first);
    }

    if (roundType == RoundType.FullBuy && extraPool.Count > 0 && _random.NextDouble() < 0.5)
    {
      result.Add(extraPool[_random.Next(extraPool.Count)]);
    }

    return result;
  }
}
