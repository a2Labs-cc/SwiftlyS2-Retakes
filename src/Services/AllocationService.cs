using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Configuration;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;
using SwiftlyS2_Retakes.Utils;

namespace SwiftlyS2_Retakes.Services;

public sealed class AllocationService : IAllocationService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly Random _random;
  private readonly IPlayerPreferencesService _prefs;
  private readonly IRetakesConfigService _config;

  public RoundType? CurrentRoundType { get; private set; }
  public bool InstantSwapEnabled => _instantSwap.Value;

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

  private readonly IConVar<bool> _ssg08HalfEnabled;
  private readonly IConVar<int> _ssg08HalfPerTeam;
  private readonly IConVar<bool> _ssg08HalfAllowEveryone;

  private readonly IConVar<string> _awpPriorityFlag;
  private readonly IConVar<int> _awpPriorityPct;

  private readonly IConVar<bool> _instantSwap;
  private readonly IConVar<bool> _stripWeapons;
  private readonly IConVar<bool> _givePistolOnRifleRounds;
  private readonly IConVar<bool> _stripRemove;
  private readonly IRetakesStateService _state;
  private readonly IDamageReportService _damageReport;

  public AllocationService(ISwiftlyCore core, ILogger logger, Random random, IPlayerPreferencesService prefs, IRetakesConfigService config, IRetakesStateService state, IDamageReportService damageReport)
  {
    _core = core;
    _logger = logger;
    _random = random;
    _prefs = prefs;
    _config = config;
    _state = state;
    _damageReport = damageReport;

    _enabled = core.ConVar.CreateOrFind("retakes_allocation_enabled", "Enable weapon allocation", true);
    _roundType = core.ConVar.CreateOrFind("retakes_round_type", "Round type: random|pistol|half|full|sequence", "random");

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

    _ssg08HalfEnabled = core.ConVar.CreateOrFind("retakes_allocation_ssg08_half_enabled", "Enable SSG08 preference allocation on HalfBuy", true);
    _ssg08HalfPerTeam = core.ConVar.CreateOrFind("retakes_allocation_ssg08_half_per_team", "Number of SSG08s per team on HalfBuy", 0, 0, 5);
    _ssg08HalfAllowEveryone = core.ConVar.CreateOrFind("retakes_allocation_ssg08_half_allow_everyone", "Ignore player preference and allow everyone to receive SSG08 on HalfBuy", false);

    _awpPriorityFlag = core.ConVar.CreateOrFind("retakes_allocation_awp_priority_flag", "Permission flag eligible for AWP priority (empty=disabled)", "");
    _awpPriorityPct = core.ConVar.CreateOrFind("retakes_allocation_awp_priority_pct", "Chance (0-100) to pick a priority player for each AWP slot", 0, 0, 100);

    _instantSwap = core.ConVar.CreateOrFind("retakes_allocation_instant_swap", "Instantly swap weapons when player changes preference mid-round", true);
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

    if (mode.Equals("sequence", StringComparison.OrdinalIgnoreCase))
      return SelectRoundTypeFromSequence();

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

  private RoundType SelectRoundTypeFromSequence()
  {
    var sequence = _config.Config.Allocation.RoundTypeSequence;
    if (sequence is not { Count: > 0 }) return RoundType.FullBuy;

    // Use actual game scores instead of internal counter — scores reflect completed rounds,
    // so current round = completed + 1. This stays accurate after mp_restartgame resets.
    var matchData = _core.Game.MatchData;
    var roundNumber = Math.Max(1, matchData.CTScoreTotal + matchData.TerroristScoreTotal + 1);
    var cumulative = 0;
    for (var i = 0; i < sequence.Count; i++)
    {
      cumulative += Math.Max(1, sequence[i].Count);
      if (roundNumber <= cumulative)
        return ParseRoundType(sequence[i].Type);
    }

    // Beyond the total — stay on the last entry's type.
    return ParseRoundType(sequence[^1].Type);
  }

  private static RoundType ParseRoundType(string? type)
  {
    if (type is null) return RoundType.FullBuy;
    if (type.Equals("pistol", StringComparison.OrdinalIgnoreCase) || type.Equals("p", StringComparison.OrdinalIgnoreCase)) return RoundType.Pistol;
    if (type.Equals("half", StringComparison.OrdinalIgnoreCase) || type.Equals("halfbuy", StringComparison.OrdinalIgnoreCase) || type.Equals("h", StringComparison.OrdinalIgnoreCase)) return RoundType.HalfBuy;
    if (type.Equals("full", StringComparison.OrdinalIgnoreCase) || type.Equals("fullbuy", StringComparison.OrdinalIgnoreCase) || type.Equals("f", StringComparison.OrdinalIgnoreCase)) return RoundType.FullBuy;
    return RoundType.FullBuy;
  }

  public void PreSelectRoundType()
  {
    CurrentRoundType = SelectRoundType();
  }

  public void AllocateForCurrentPlayers(IPawnLifecycleService pawnLifecycle)
  {
    if (CurrentRoundType is null)
    {
      PreSelectRoundType();
    }

    var roundType = CurrentRoundType!.Value;

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
      foreach (var steamId in PickAwpReceivers(ct.Where(PlayerUtil.IsHuman).ToList())) awpReceivers.Add(steamId);
      foreach (var steamId in PickAwpReceivers(t.Where(PlayerUtil.IsHuman).ToList())) awpReceivers.Add(steamId);
    }

    if (roundType == RoundType.FullBuy && _ssg08Enabled.Value)
    {
      foreach (var steamId in PickSsg08Receivers(ct.Where(PlayerUtil.IsHuman).ToList(), awpReceivers)) ssg08Receivers.Add(steamId);
      foreach (var steamId in PickSsg08Receivers(t.Where(PlayerUtil.IsHuman).ToList(), awpReceivers)) ssg08Receivers.Add(steamId);
    }

    if (roundType == RoundType.HalfBuy && _ssg08HalfEnabled.Value)
    {
      foreach (var steamId in PickSsg08HalfBuyReceivers(ct.Where(PlayerUtil.IsHuman).ToList())) ssg08Receivers.Add(steamId);
      foreach (var steamId in PickSsg08HalfBuyReceivers(t.Where(PlayerUtil.IsHuman).ToList())) ssg08Receivers.Add(steamId);
    }

    var pistolDefuserSlot = -1;
    if (roundType == RoundType.Pistol)
    {
      var humanCt = ct.Where(PlayerUtil.IsHuman).ToList();
      if (humanCt.Count > 0)
      {
        pistolDefuserSlot = humanCt[_random.Next(humanCt.Count)].Slot;
      }
    }

    // Pre-compute dynamic grenade assignments (requires full team lists, so done here).
    var grenadeAssignments = ComputeDynamicGrenadeAssignments(ct, t, roundType);

    foreach (var p in players)
    {
      pawnLifecycle.WhenPawnReady(p, _ =>
      {
        // Defer loadout to NextWorldUpdate so it runs AFTER the spawn teleport
        // scheduled by PlayerEventHandlers.OnPlayerSpawnPost (also NextWorldUpdate,
        // queued earlier — FIFO guarantees teleport runs first). Otherwise items
        // like item_defuser are created at the engine's default spawn position and
        // get left behind when the player teleports to the retake spawn.
        _core.Scheduler.NextWorldUpdate(() =>
        {
          if (p is null || !p.IsValid) return;
          try
          {
            GiveLoadout(p, roundType, pistolDefuserSlot, awpReceivers, ssg08Receivers, grenadeAssignments);

            // After loadout is given, schedule a delayed helmet strip for pistol rounds.
            // The engine may re-apply helmet after our GiveItem calls, so we need to
            // strip it on the next tick to ensure it sticks.
            if (roundType == RoundType.Pistol && !_config.Config.Allocation.PistolHelmet)
            {
              _core.Scheduler.NextTick(() =>
              {
                if (p is null || !p.IsValid) return;
                var pawn = p.Pawn;
                if (pawn is null || !pawn.IsValid) return;
                if (pawn.ItemServices is CCSPlayer_ItemServices svc && svc.HasHelmet)
                {
                  svc.HasHelmet = false;
                  svc.HasHelmetUpdated();
                }
              });
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Retakes: allocation failed for slot={Slot}", p.Slot);
          }
        });
      });
    }
  }

  private void GiveLoadout(IPlayer player, RoundType roundType, int pistolDefuserSlot, HashSet<ulong> awpReceivers, HashSet<ulong> ssg08Receivers, IReadOnlyDictionary<ulong, List<string>> grenadeAssignments)
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

    if (roundType == RoundType.Pistol && !_config.Config.Allocation.PistolHelmet)
    {
      itemServices.GiveItem("item_kevlar");
      if (itemServices is CCSPlayer_ItemServices csItemServices)
      {
        csItemServices.HasHelmet = false;
        csItemServices.HasHelmetUpdated();
      }
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
    else if (roundType == RoundType.HalfBuy && ssg08Receivers.Contains(player.SteamID))
    {
      primary = "weapon_ssg08";
    }
    else if (PlayerUtil.IsHuman(player))
    {
      primary = SelectPrimary(team, roundType, player.SteamID);
    }
    else
    {
      var allowed = GetAllowedPrimaryWeapons(roundType, team);
      primary = allowed.Count == 0 ? null : allowed[_random.Next(allowed.Count)];
    }
    if (primary is not null)
    {
      itemServices.GiveItem(primary);
    }

    if (roundType != RoundType.Pistol && _givePistolOnRifleRounds.Value)
    {
      string? secondary;
      if (PlayerUtil.IsHuman(player))
      {
        secondary = SelectSecondary(team, roundType, player.SteamID);
      }
      else
      {
        var allowed = _config.Config.Weapons.Pistols;
        secondary = allowed.Count == 0 ? null : allowed[_random.Next(allowed.Count)];
      }

      if (secondary is not null)
      {
        itemServices.GiveItem(secondary);
      }
    }

    foreach (var nade in SelectGrenades(team, roundType, player.SteamID, grenadeAssignments))
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
      var configuredDefault = GetConfiguredDefaultPrimary(team, roundType);
      return PreferOrDefaultOrRandom(preferred, configuredDefault, allowed);
    }

    if (roundType == RoundType.HalfBuy)
    {
      var allowed = GetAllowedPrimaryWeapons(roundType, team);
      var pack = _prefs.GetHalfBuyPack(steamId, team == Team.CT);
      var configuredDefault = GetConfiguredDefaultPrimary(team, roundType);
      return PreferOrDefaultOrRandom(pack.Primary, configuredDefault, allowed);
    }

    // FullBuy
    var fullAllowed = GetAllowedPrimaryWeapons(roundType, team);
    var fullPack = _prefs.GetFullBuyPack(steamId, team == Team.CT);
    var fullConfiguredDefault = GetConfiguredDefaultPrimary(team, roundType);
    return PreferOrDefaultOrRandom(fullPack.Primary, fullConfiguredDefault, fullAllowed);
  }

  private string? SelectSecondary(Team team, RoundType roundType, ulong steamId)
  {
    // All round types use the shared pistols list for secondary
    var allowed = _config.Config.Weapons.Pistols;

    if (roundType == RoundType.HalfBuy)
    {
      var pack = _prefs.GetHalfBuyPack(steamId, team == Team.CT);
      var configuredDefault = GetConfiguredDefaultSecondary(team, roundType);
      return PreferOrDefaultOrRandom(pack.Secondary, configuredDefault, allowed);
    }

    if (roundType == RoundType.FullBuy)
    {
      var pack = _prefs.GetFullBuyPack(steamId, team == Team.CT);
      var configuredDefault = GetConfiguredDefaultSecondary(team, roundType);
      return PreferOrDefaultOrRandom(pack.Secondary, configuredDefault, allowed);
    }

    return null;
  }

  private string? GetConfiguredDefaultPrimary(Team team, RoundType roundType)
  {
    var defaults = roundType switch
    {
      RoundType.Pistol => _config.Config.Weapons.Defaults.Pistol,
      RoundType.HalfBuy => _config.Config.Weapons.Defaults.HalfBuy,
      RoundType.FullBuy => _config.Config.Weapons.Defaults.FullBuy,
      _ => null,
    };

    return defaults is null ? null : ResolveSelection(defaults.Primary, team);
  }

  private string? GetConfiguredDefaultSecondary(Team team, RoundType roundType)
  {
    var defaults = roundType switch
    {
      RoundType.HalfBuy => _config.Config.Weapons.Defaults.HalfBuy,
      RoundType.FullBuy => _config.Config.Weapons.Defaults.FullBuy,
      _ => null,
    };

    return defaults is null ? null : ResolveSelection(defaults.Secondary, team);
  }

  private static string? ResolveSelection(DefaultWeaponSelectionConfig selection, Team team)
  {
    if (team == Team.CT && !string.IsNullOrWhiteSpace(selection.Ct))
      return selection.Ct;

    if (team == Team.T && !string.IsNullOrWhiteSpace(selection.T))
      return selection.T;

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

  private IEnumerable<ulong> PickSsg08HalfBuyReceivers(List<IPlayer> players)
  {
    var perTeam = Math.Clamp(_ssg08HalfPerTeam.Value, 0, 10);
    if (perTeam <= 0) return Array.Empty<ulong>();

    if (players.Count == 0) return Array.Empty<ulong>();

    var candidates = _ssg08HalfAllowEveryone.Value
      ? players
      : players.Where(p => _prefs.WantsSsg08HalfBuy(p.SteamID)).ToList();

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

  private string? PreferOrDefaultOrRandom(string? preferred, string? configuredDefault, IReadOnlyList<string> allowed)
  {
    if (!string.IsNullOrWhiteSpace(preferred) && IsAllowed(preferred, allowed))
    {
      return preferred;
    }

    if (!string.IsNullOrWhiteSpace(configuredDefault) && IsAllowed(configuredDefault, allowed))
    {
      return configuredDefault;
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

  private IReadOnlyDictionary<ulong, List<string>> ComputeDynamicGrenadeAssignments(
    List<IPlayer> ct, List<IPlayer> t, RoundType roundType)
  {
    var cfg = _config.Config.Grenades;
    var allocType = (cfg.AllocationType ?? "random").Trim();
    if (!allocType.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
      return new Dictionary<ulong, List<string>>();

    var assignments = new Dictionary<ulong, List<string>>();
    var roundCfg = GetRoundGrenadeConfig(roundType, cfg);
    var minDmg = Math.Max(0, cfg.DynamicMinDamage);
    var maxPerPlayer = cfg.DynamicMaxPerPlayer;
    var topFraction = Math.Clamp(cfg.DynamicTopFraction, 0f, 1f);
    var maxPerGrenade = cfg.MaxPerGrenade;

    AssignDynamicGrenadesForTeam(t, roundCfg.T.DynamicPool, minDmg, maxPerPlayer, topFraction, cfg.DynamicUseCumulativeScore, maxPerGrenade, assignments);
    AssignDynamicGrenadesForTeam(ct, roundCfg.Ct.DynamicPool, minDmg, maxPerPlayer, topFraction, cfg.DynamicUseCumulativeScore, maxPerGrenade, assignments);

    return assignments;
  }

  private void AssignDynamicGrenadesForTeam(
    List<IPlayer> players,
    List<string> pool,
    int minDmg,
    int maxPerPlayer,
    float topFraction,
    bool useCumulativeScore,
    IReadOnlyDictionary<string, int> maxPerGrenade,
    Dictionary<ulong, List<string>> assignments)
  {
    if (pool.Count == 0) return;

    // Filter to players who dealt at least minDmg last round, then sort by priority descending.
    var eligible = players
      .Select(p => (
        Player: p,
        LastDmg: _damageReport.GetLastRoundDamage(p.SteamID),
        Score: useCumulativeScore ? _damageReport.GetPlayerScore(p.SteamID) : 0f))
      .Where(x => x.LastDmg >= minDmg)
      .OrderByDescending(x => useCumulativeScore ? (double)x.Score : x.LastDmg)
      .Select(x => x.Player)
      .ToList();

    // Apply TopFraction cap: only allow the top fraction of performers to receive grenades.
    // Round up so that e.g. 1 out of 1 eligible is always included.
    if (topFraction < 1f && eligible.Count > 0)
    {
      var cap = Math.Max(1, (int)Math.Ceiling(eligible.Count * topFraction));
      if (eligible.Count > cap)
        eligible = eligible.Take(cap).ToList();
    }

    if (eligible.Count == 0) return;

    // Shuffle a copy of the pool so the prefix doesn't always win when MaxPerPlayer/MaxPerGrenade
    // caps prevent the full pool from being distributed.
    var shuffledPool = new List<string>(pool);
    for (var i = shuffledPool.Count - 1; i > 0; i--)
    {
      var j = _random.Next(i + 1);
      (shuffledPool[i], shuffledPool[j]) = (shuffledPool[j], shuffledPool[i]);
    }

    // Distribute pool items round-robin across eligible players (highest priority first).
    var totalCounts = new Dictionary<ulong, int>(eligible.Count);
    // grenade type -> (steamId -> count)
    var grenadeCounts = new Dictionary<string, Dictionary<ulong, int>>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < shuffledPool.Count; i++)
    {
      var grenade = shuffledPool[i];

      // Find the next eligible player who hasn't hit the per-player cap or the per-grenade cap.
      IPlayer? recipient = null;
      for (var attempt = 0; attempt < eligible.Count; attempt++)
      {
        var candidate = eligible[(i + attempt) % eligible.Count];
        var sid = candidate.SteamID;

        totalCounts.TryGetValue(sid, out var total);
        if (maxPerPlayer > 0 && total >= maxPerPlayer) continue;

        if (maxPerGrenade.TryGetValue(grenade, out var grenadeMax))
        {
          if (!grenadeCounts.TryGetValue(grenade, out var byPlayer))
            byPlayer = new Dictionary<ulong, int>();
          byPlayer.TryGetValue(sid, out var grenadeCount);
          if (grenadeCount >= grenadeMax) continue;
        }

        recipient = candidate;
        break;
      }
      if (recipient is null) continue; // no eligible player can receive this grenade

      var recipientId = recipient.SteamID;
      if (!assignments.TryGetValue(recipientId, out var list))
      {
        list = new List<string>();
        assignments[recipientId] = list;
      }
      list.Add(grenade);
      totalCounts[recipientId] = (totalCounts.TryGetValue(recipientId, out var tc) ? tc : 0) + 1;

      if (!grenadeCounts.TryGetValue(grenade, out var gc))
      {
        gc = new Dictionary<ulong, int>();
        grenadeCounts[grenade] = gc;
      }
      gc[recipientId] = (gc.TryGetValue(recipientId, out var prev) ? prev : 0) + 1;
    }
  }

  private IEnumerable<string> SelectGrenades(
    Team team,
    RoundType roundType,
    ulong steamId,
    IReadOnlyDictionary<ulong, List<string>> dynamicAssignments)
  {
    var cfg = _config.Config.Grenades;
    var allocType = (cfg.AllocationType ?? "random").Trim();

    var roundCfg = GetRoundGrenadeConfig(roundType, cfg);
    var teamCfg = team == Team.CT ? roundCfg.Ct : roundCfg.T;

    if (allocType.Equals("fixed", StringComparison.OrdinalIgnoreCase))
      return ApplyMaxPerGrenade(teamCfg.Fixed, cfg.MaxPerGrenade);

    if (allocType.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
    {
      var grenades = dynamicAssignments.TryGetValue(steamId, out var assigned) ? assigned : (IEnumerable<string>)Array.Empty<string>();
      return ApplyMaxPerGrenade(grenades, cfg.MaxPerGrenade);
    }

    // Default: random — roll each grenade independently.
    var result = new List<string>(teamCfg.RandomChances.Count);
    var rolledCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var (grenade, chance) in teamCfg.RandomChances)
    {
      if (_random.Next(0, 100) >= Math.Clamp(chance, 0, 100)) continue;
      if (cfg.MaxPerGrenade.TryGetValue(grenade, out var cap))
      {
        rolledCounts.TryGetValue(grenade, out var already);
        if (already >= cap) continue;
        rolledCounts[grenade] = already + 1;
      }
      result.Add(grenade);
    }
    return result;
  }

  private static IEnumerable<string> ApplyMaxPerGrenade(
    IEnumerable<string> grenades,
    IReadOnlyDictionary<string, int> maxPerGrenade)
  {
    var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var result = new List<string>();
    foreach (var grenade in grenades)
    {
      if (maxPerGrenade.TryGetValue(grenade, out var cap))
      {
        counts.TryGetValue(grenade, out var already);
        if (already >= cap) continue;
        counts[grenade] = already + 1;
      }
      result.Add(grenade);
    }
    return result;
  }

  private static RoundTypeGrenadeConfig GetRoundGrenadeConfig(RoundType roundType, GrenadeConfig cfg)
  {
    return roundType switch
    {
      RoundType.Pistol => cfg.Pistol,
      RoundType.HalfBuy => cfg.HalfBuy,
      _ => cfg.FullBuy,
    };
  }
}
