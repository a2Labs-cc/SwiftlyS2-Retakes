using SwiftlyS2.Shared;
using SwiftlyS2_Retakes.Configuration;

namespace SwiftlyS2_Retakes.Services;

/// <summary>
/// Applies configuration values to game convars.
/// </summary>
public sealed class ConVarApplicator
{
  private readonly ISwiftlyCore _core;

  public ConVarApplicator(ISwiftlyCore core)
  {
    _core = core;
  }

  /// <summary>
  /// Applies all configuration values to their corresponding convars.
  /// </summary>
  public void ApplyConfig(RetakesConfig config)
  {
    // Allocation settings
    ApplyBool("retakes_allocation_enabled", config.Allocation.Enabled);
    ApplyString("retakes_round_type", config.Allocation.RoundType);
    ApplyInt("retakes_round_type_pct_pistol", config.Allocation.RoundTypePctPistol);
    ApplyInt("retakes_round_type_pct_half", config.Allocation.RoundTypePctHalf);
    ApplyInt("retakes_round_type_pct_full", config.Allocation.RoundTypePctFull);

    // AWP settings
    ApplyBool("retakes_allocation_awp_enabled", config.Allocation.AwpEnabled);
    ApplyInt("retakes_allocation_awp_per_team", config.Allocation.AwpPerTeam);
    ApplyBool("retakes_allocation_awp_allow_everyone", config.Allocation.AwpAllowEveryone);
    ApplyInt("retakes_allocation_awp_low_players_threshold", config.Allocation.AwpLowPlayersThreshold);
    ApplyInt("retakes_allocation_awp_low_players_chance", config.Allocation.AwpLowPlayersChance);
    ApplyInt("retakes_allocation_awp_low_players_vip_chance", config.Allocation.AwpLowPlayersVipChance);

    // SSG08 settings
    ApplyBool("retakes_allocation_ssg08_enabled", config.Allocation.Ssg08Enabled);
    ApplyInt("retakes_allocation_ssg08_per_team", config.Allocation.Ssg08PerTeam);
    ApplyBool("retakes_allocation_ssg08_allow_everyone", config.Allocation.Ssg08AllowEveryone);

    // AWP priority settings
    ApplyString("retakes_allocation_awp_priority_flag", config.Allocation.AwpPriorityFlag);
    ApplyInt("retakes_allocation_awp_priority_pct", config.Allocation.AwpPriorityPct);

    // Bomb settings
    ApplyBool("retakes_auto_plant", config.Bomb.AutoPlant);
    ApplyBool("retakes_enforce_no_c4", config.Bomb.EnforceNoC4);

    ApplyBool("retakes_smoke_scenarios_enabled", config.SmokeScenarios.Enabled);
    ApplyBool("retakes_smoke_scenarios_random_rounds_enabled", config.SmokeScenarios.RandomRoundsEnabled);
    ApplyFloat("retakes_smoke_scenarios_random_round_chance", config.SmokeScenarios.RandomRoundChance);

    // Team balance settings
    ApplyBool("retakes_team_balance_enabled", config.TeamBalance.Enabled);
    ApplyFloat("retakes_team_balance_terrorist_ratio", config.TeamBalance.TerroristRatio);
    ApplyBool("retakes_team_balance_force_even_when_players_mod_10", config.TeamBalance.ForceEvenWhenPlayersMod10);

    // Instant bomb settings
    ApplyBool("retakes_insta_plant", config.InstantBomb.InstaPlant);
    ApplyBool("retakes_insta_defuse", config.InstantBomb.InstaDefuse);
    ApplyBool("retakes_insta_defuse_block_t_alive", config.InstantBomb.BlockDefuseIfTAlive);
    ApplyBool("retakes_insta_defuse_block_molly", config.InstantBomb.BlockDefuseIfMollyNear);
    ApplyFloat("retakes_insta_defuse_molly_radius", config.InstantBomb.MollyRadius);

    // Anti team flash settings
    ApplyBool("retakes_antiteamflash_enabled", config.AntiTeamFlash.Enabled);
    ApplyBool("retakes_antiteamflash_flash_owner", config.AntiTeamFlash.FlashOwner);
    ApplyString("retakes_antiteamflash_access_flag", config.AntiTeamFlash.AccessFlag);

    // Breaker settings
    ApplyBool("retakes_break_breakables", config.Breaker.BreakBreakables);
    ApplyBool("retakes_open_doors", config.Breaker.OpenDoors);

    // Buy menu settings
    ApplyBool("retakes_buymenu_enabled", config.Weapons.BuyMenuEnabled);

    ApplyBool("retakes_solo_bot_enabled", config.SoloBot.Enabled);
    ApplyInt("retakes_solo_bot_difficulty", config.SoloBot.Difficulty);

    // Apply C4 enforcement
    var autoPlant = _core.ConVar.Find<bool>("retakes_auto_plant")?.Value ?? false;
    var enforceNoC4 = _core.ConVar.Find<bool>("retakes_enforce_no_c4")?.Value ?? false;
    if (autoPlant && enforceNoC4)
    {
      _core.Engine.ExecuteCommand("mp_give_player_c4 0");
    }
  }

  public void ApplyBool(string name, bool value)
  {
    var cv = _core.ConVar.Find<bool>(name);
    if (cv is null) return;
    cv.Value = value;
  }

  public void ApplyInt(string name, int value)
  {
    var cv = _core.ConVar.Find<int>(name);
    if (cv is null) return;
    cv.Value = value;
  }

  public void ApplyFloat(string name, float value)
  {
    var cv = _core.ConVar.Find<float>(name);
    if (cv is null) return;
    cv.Value = value;
  }

  public void ApplyString(string name, string value)
  {
    var cv = _core.ConVar.Find<string>(name);
    if (cv is null) return;
    cv.Value = value;
  }
}
