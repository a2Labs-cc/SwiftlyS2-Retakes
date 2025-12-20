using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Retakes.Configuration;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
using SwiftlyS2_Retakes.Models;
using System.Globalization;

namespace SwiftlyS2_Retakes.Services;

public sealed class BuyMenuService : IBuyMenuService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IRetakesConfigService _config;
  private readonly IAllocationService _allocation;
  private readonly IPlayerPreferencesService _prefs;
  private readonly IMessageService _messages;

  private readonly IConVar<bool> _enabled;

  private readonly IConVar<int> _pistolMoney;
  private readonly IConVar<int> _halfBuyMoney;
  private readonly IConVar<int> _fullBuyMoney;
  private readonly IConVar<float> _buyTimeSeconds;

  private HashSet<string> _allowedWeapons = new(StringComparer.OrdinalIgnoreCase);

  private Guid _itemPurchaseHook;

  public BuyMenuService(ISwiftlyCore core, ILogger logger, IRetakesConfigService config, IAllocationService allocation, IPlayerPreferencesService prefs, IMessageService messages)
  {
    _core = core;
    _logger = logger;
    _config = config;
    _allocation = allocation;
    _prefs = prefs;
    _messages = messages;

    _enabled = core.ConVar.CreateOrFind("retakes_buymenu_enabled", "Enable in-game buy menu with round type restrictions", false);

    _pistolMoney = core.ConVar.CreateOrFind("retakes_buymenu_money_pistol", "Money to give on pistol rounds (0-16000)", 800, 0, 16000);
    _halfBuyMoney = core.ConVar.CreateOrFind("retakes_buymenu_money_half", "Money to give on half-buy rounds (0-16000)", 2500, 0, 16000);
    _fullBuyMoney = core.ConVar.CreateOrFind("retakes_buymenu_money_full", "Money to give on full-buy rounds (0-16000)", 5000, 0, 16000);
    _buyTimeSeconds = core.ConVar.CreateOrFind("retakes_buymenu_buytime_seconds", "Buy time in seconds (applied each round)", 5.0f, 0.0f, 300.0f);
  }

  public void Initialize()
  {
    _itemPurchaseHook = _core.GameEvent.HookPre<EventItemPurchase>(OnItemPurchase);

    _logger.LogPluginDebug("Retakes: BuyMenuService initialized. retakes_buymenu_enabled={Enabled}", _enabled.Value);

    _core.Scheduler.DelayBySeconds(2.0f, ApplyEnforcement);
    _core.Scheduler.DelayBySeconds(6.0f, ApplyEnforcement);
  }

  private void ApplyEnforcement()
  {
    if (!_enabled.Value)
    {
      return;
    }

    ApplyBuyMenuConvars();
  }

  public void Unregister()
  {
    if (_itemPurchaseHook != Guid.Empty)
    {
      _core.GameEvent.Unhook(_itemPurchaseHook);
      _itemPurchaseHook = Guid.Empty;
    }
  }

  public void ApplyBuyMenuConvars()
  {
    if (!_enabled.Value)
    {
      return;
    }

    var pistol = Math.Clamp(_pistolMoney.Value, 0, 16000);
    var half = Math.Clamp(_halfBuyMoney.Value, 0, 16000);
    var full = Math.Clamp(_fullBuyMoney.Value, 0, 16000);
    var maxMoney = Math.Max(pistol, Math.Max(half, full));

    // Buy time (in seconds -> minutes for mp_buytime)
    var buyTimeSec = Math.Clamp(_buyTimeSeconds.Value, 0f, 300f);
    var buyTimeMinutes = buyTimeSec / 60f;

    // Retakes default cfg may clamp money to 0; ensure economy allows purchases.
    _core.Engine.ExecuteCommand($"mp_maxmoney {maxMoney}");
    _core.Engine.ExecuteCommand($"mp_startmoney {maxMoney}");
    _core.Engine.ExecuteCommand($"mp_afterroundmoney {maxMoney}");
    _core.Engine.ExecuteCommand("mp_playercashawards 0");
    _core.Engine.ExecuteCommand("mp_teamcashawards 0");

    _core.Engine.ExecuteCommand($"mp_buytime {buyTimeMinutes.ToString(CultureInfo.InvariantCulture)}");
    _core.Engine.ExecuteCommand("mp_buy_anywhere 1");
    _core.Engine.ExecuteCommand("mp_buy_allow_grenades 0");

    _logger.LogPluginDebug("Retakes: Buy menu enabled - mp_buy_anywhere=1 buytime={BuyTimeSec}s", buyTimeSec);
  }

  public void OnRoundStart()
  {
    if (!_enabled.Value)
    {
      return;
    }

    ApplyBuyMenuConvars();

    _core.Scheduler.DelayBySeconds(1.0f, ApplyEnforcement);
  }

  public void OnRoundTypeSelected()
  {
    if (!_enabled.Value)
    {
      return;
    }

    UpdateAllowedWeapons();
    EnsurePlayersHaveMoney();
  }

  private void EnsurePlayersHaveMoney()
  {
    try
    {
      var roundType = _allocation.CurrentRoundType ?? RoundType.FullBuy;
      var desired = roundType switch
      {
        RoundType.Pistol => Math.Clamp(_pistolMoney.Value, 0, 16000),
        RoundType.HalfBuy => Math.Clamp(_halfBuyMoney.Value, 0, 16000),
        _ => Math.Clamp(_fullBuyMoney.Value, 0, 16000),
      };

      foreach (var player in _core.PlayerManager.GetAllPlayers())
      {
        if (player is null || !player.IsValid) continue;

        var team = (Team)player.Controller.TeamNum;
        if (team != Team.T && team != Team.CT) continue;

        var money = player.Controller.InGameMoneyServices;
        if (money is null) continue;

        if (money.Account < desired)
        {
          money.Account = desired;
          money.AccountUpdated();
        }

        if (money.StartAccount < desired)
        {
          money.StartAccount = desired;
          money.StartAccountUpdated();
        }
      }
    }
    catch
    {
    }
  }

  private void UpdateAllowedWeapons()
  {
    var roundType = _allocation.CurrentRoundType ?? RoundType.FullBuy;
    var weapons = _config.Config.Weapons;

    // Build allowed weapons list from unified config (pistols + round-specific primaries)
    _allowedWeapons = roundType switch
    {
      RoundType.Pistol => new HashSet<string>(weapons.Pistols, StringComparer.OrdinalIgnoreCase),
      RoundType.HalfBuy => BuildAllowedSet(weapons.Pistols, weapons.HalfBuy),
      RoundType.FullBuy => BuildAllowedSet(weapons.Pistols, weapons.FullBuy),
      _ => BuildAllowedSet(weapons.Pistols, weapons.FullBuy),
    };

    // Apply mp_buy_allow_guns to restrict buy menu categories
    // Bitmask: 1=pistols, 2=SMGs, 4=rifles, 8=shotguns, 16=snipers, 32=machine guns
    var allowGuns = roundType switch
    {
      RoundType.Pistol => 1,           // Pistols only
      RoundType.HalfBuy => 1 + 2 + 4,  // Pistols + SMGs + Rifles (for famas/galil)
      RoundType.FullBuy => 1 + 4,      // Pistols + Rifles
      _ => 1 + 4,
    };
    _core.Engine.ExecuteCommand($"mp_buy_allow_guns {allowGuns}");

    _logger.LogDebug("Retakes: Buy menu updated for {RoundType} round - {Count} weapons allowed, mp_buy_allow_guns={AllowGuns}", roundType, _allowedWeapons.Count, allowGuns);
  }

  private static HashSet<string> BuildAllowedSet(List<string> pistols, RoundWeaponsConfig round)
  {
    var set = new HashSet<string>(pistols, StringComparer.OrdinalIgnoreCase);
    foreach (var w in round.All) set.Add(w);
    foreach (var w in round.T) set.Add(w);
    foreach (var w in round.Ct) set.Add(w);
    return set;
  }

  private HookResult OnItemPurchase(EventItemPurchase @event)
  {
    if (!_enabled.Value)
    {
      return HookResult.Continue;
    }

    var weaponName = @event.Weapon;
    if (string.IsNullOrEmpty(weaponName))
    {
      return HookResult.Continue;
    }

    // Normalize weapon name (event may not have weapon_ prefix)
    var normalizedName = weaponName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)
      ? weaponName
      : $"weapon_{weaponName}";

    if (IsAlwaysAllowed(normalizedName))
    {
      return HookResult.Continue;
    }

    var player = @event.UserIdPlayer;
    if (player is null || !player.IsValid)
    {
      return HookResult.Continue;
    }

    if (_allowedWeapons.Contains(normalizedName) || _allowedWeapons.Contains(weaponName))
    {
      // Game drops old weapon before event fires - schedule cleanup of dropped weapons near player
      var playerPos = player.Pawn?.AbsOrigin;
      if (playerPos.HasValue)
      {
        var pos = playerPos.Value;
        var purchaseSlot = GetWeaponSlot(normalizedName);
        _core.Scheduler.DelayBySeconds(0.1f, () => RemoveDroppedWeaponsNearPlayer(player, pos, purchaseSlot));
      }

      // Save purchased weapon to preferences for next round of same type
      SavePurchasedWeapon(player, normalizedName);
      return HookResult.Continue;
    }

    // Weapon not allowed - schedule removal after purchase completes
    var slot = GetWeaponSlot(normalizedName);
    var roundType = _allocation.CurrentRoundType ?? RoundType.FullBuy;

    _core.Scheduler.DelayBySeconds(0.1f, () =>
    {
      if (player is null || !player.IsValid) return;

      var pawn = player.Pawn;
      if (pawn is null || !pawn.IsValid) return;

      var weaponServices = pawn.WeaponServices;
      if (weaponServices is null) return;

      try
      {
        if (slot.HasValue)
        {
          weaponServices.RemoveWeaponBySlot(slot.Value);
        }
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Retakes: Failed to remove restricted weapon {Weapon}", normalizedName);
      }

      var loc = _core.Translation.GetPlayerLocalizer(player);
      _messages.Chat(player, loc["buy.restricted", GetWeaponDisplayName(normalizedName), roundType]);
    });

    return HookResult.Continue;
  }

  private static readonly HashSet<string> PistolKeywords = new(StringComparer.OrdinalIgnoreCase)
  {
    "glock", "usp", "hkp2000", "p250", "fiveseven", "tec9", "cz75a", "deagle", "revolver", "elite"
  };

  private static readonly HashSet<string> PrimaryKeywords = new(StringComparer.OrdinalIgnoreCase)
  {
    "ak47", "m4a1", "awp", "aug", "sg556", "famas", "galilar",
    "ssg08", "scar20", "g3sg1",
    "mac10", "mp9", "mp7", "mp5sd", "ump45", "p90", "bizon",
    "nova", "xm1014", "sawedoff", "mag7",
    "negev", "m249"
  };

  private static readonly HashSet<string> AlwaysAllowedKeywords = new(StringComparer.OrdinalIgnoreCase)
  {
    "knife", "bayonet", "flashbang", "smokegrenade", "hegrenade", "molotov", "incgrenade", "decoy"
  };

  private static readonly HashSet<string> AlwaysAllowedExact = new(StringComparer.OrdinalIgnoreCase)
  {
    "weapon_c4", "weapon_taser"
  };

  private static gear_slot_t? GetWeaponSlot(string weaponName)
  {
    if (ContainsAny(weaponName, PistolKeywords))
      return gear_slot_t.GEAR_SLOT_PISTOL;

    if (ContainsAny(weaponName, PrimaryKeywords))
      return gear_slot_t.GEAR_SLOT_RIFLE;

    return null;
  }

  private static bool IsAlwaysAllowed(string weaponName)
  {
    if (AlwaysAllowedExact.Contains(weaponName)) return true;
    if (weaponName.StartsWith("item_", StringComparison.OrdinalIgnoreCase)) return true;
    return ContainsAny(weaponName, AlwaysAllowedKeywords);
  }

  private static bool ContainsAny(string text, HashSet<string> keywords)
  {
    foreach (var keyword in keywords)
    {
      if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        return true;
    }
    return false;
  }

  private static string GetWeaponDisplayName(string weaponName)
  {
    if (string.IsNullOrEmpty(weaponName)) return "Unknown";

    var name = weaponName.Replace("weapon_", "", StringComparison.OrdinalIgnoreCase);
    if (name.Length > 0)
    {
      name = char.ToUpper(name[0]) + name.Substring(1);
    }

    return name;
  }

  private void SavePurchasedWeapon(IPlayer? player, string weaponName)
  {
    if (player is null || !player.IsValid) return;

    var roundType = _allocation.CurrentRoundType ?? RoundType.FullBuy;
    var isCt = (Team)player.Controller.TeamNum == Team.CT;
    var isPistol = IsPistolWeapon(weaponName);

    try
    {
      if (roundType == RoundType.Pistol)
      {
        // Pistol round - save as pistol preference
        _prefs.SetPistolPrimary(player.SteamID, isCt, weaponName);
      }
      else if (isPistol)
      {
        // Bought a pistol on half/full buy - save as secondary
        if (roundType == RoundType.HalfBuy)
          _prefs.SetHalfBuySecondary(player.SteamID, isCt, weaponName);
        else
          _prefs.SetFullBuySecondary(player.SteamID, isCt, weaponName);
      }
      else
      {
        // Bought a primary weapon on half/full buy
        if (roundType == RoundType.HalfBuy)
          _prefs.SetHalfBuyPrimary(player.SteamID, isCt, weaponName);
        else
          _prefs.SetFullBuyPrimary(player.SteamID, isCt, weaponName);
      }

      _logger.LogPluginDebug("Retakes: Saved weapon preference {Weapon} for {RoundType} ({Slot}) steamId={SteamId}",
        weaponName, roundType, isPistol ? "secondary" : "primary", player.SteamID);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: Failed to save weapon preference for steamId={SteamId}", player.SteamID);
    }
  }

  private static bool IsPistolWeapon(string weaponName) => ContainsAny(weaponName, PistolKeywords);

  private void RemoveDroppedWeaponsNearPlayer(IPlayer? player, Vector purchasePos, gear_slot_t? slot)
  {
    if (player is null || !player.IsValid) return;
    if (!slot.HasValue) return;

    try
    {
      // Find dropped weapons of the same slot type near the purchase position
      var designerNames = slot.Value == gear_slot_t.GEAR_SLOT_PISTOL
        ? new[] { "weapon_glock", "weapon_usp_silencer", "weapon_hkp2000", "weapon_p250", "weapon_fiveseven", "weapon_tec9", "weapon_cz75a", "weapon_deagle", "weapon_revolver", "weapon_elite" }
        : new[] { "weapon_ak47", "weapon_m4a1", "weapon_m4a1_silencer", "weapon_awp", "weapon_aug", "weapon_sg556", "weapon_famas", "weapon_galilar", "weapon_mac10", "weapon_mp9", "weapon_mp7", "weapon_mp5sd", "weapon_ump45", "weapon_p90", "weapon_bizon", "weapon_ssg08", "weapon_scar20", "weapon_g3sg1", "weapon_nova", "weapon_xm1014", "weapon_sawedoff", "weapon_mag7", "weapon_negev", "weapon_m249" };

      foreach (var designerName in designerNames)
      {
        var weapons = _core.EntitySystem.GetAllEntitiesByDesignerName<CBasePlayerWeapon>(designerName);
        foreach (var weapon in weapons)
        {
          if (weapon is null || !weapon.IsValid) continue;

          // Check if weapon is dropped (no owner)
          var owner = weapon.OwnerEntity.Value;
          if (owner is not null && owner.IsValid) continue;

          // Check distance from purchase position
          var weaponPos = weapon.AbsOrigin;
          if (!weaponPos.HasValue) continue;

          var wPos = weaponPos.Value;
          var dx = wPos.X - purchasePos.X;
          var dy = wPos.Y - purchasePos.Y;
          var dz = wPos.Z - purchasePos.Z;
          var distSq = dx * dx + dy * dy + dz * dz;

          // Within 150 units of where player was standing
          if (distSq < 150 * 150)
          {
            weapon.Despawn();
            _logger.LogPluginDebug("Retakes: Removed dropped weapon {Weapon} near player", designerName);
            return; // Only remove one weapon per purchase
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogPluginDebug(ex, "Retakes: Failed to remove dropped weapons");
    }
  }
}
