using System.IO;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2_Retakes.Configuration;
using SwiftlyS2_Retakes.Logging;

namespace SwiftlyS2_Retakes.Services;

/// <summary>
/// Generates and manages the retakes.cfg file for server configuration.
/// </summary>
public sealed class RetakesCfgGenerator
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;

  private const string CfgFolderName = "Retakes";
  private const string CfgFileName = "retakes.cfg";

  public RetakesCfgGenerator(ISwiftlyCore core, ILogger logger)
  {
    _core = core;
    _logger = logger;
  }

  /// <summary>
  /// Applies the retakes.cfg configuration and optionally restarts the game.
  /// </summary>
  public void Apply(RetakesConfig config, bool restartGame = false)
  {
    try
    {
      var freezeTime = Math.Clamp(config.Server.FreezeTimeSeconds, 0, 60);

      var cfgDir = Path.Combine(_core.CSGODirectory, "cfg", CfgFolderName);
      var cfgPath = Path.Combine(cfgDir, CfgFileName);

      _logger.LogPluginDebug("Retakes: applying freeze time. FreezeTimeSeconds={Freeze} CfgPath={CfgPath}", freezeTime, cfgPath);

      if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);

      GenerateCfgFile(cfgPath, freezeTime);

      void ExecuteConfig()
      {
        _core.Engine.ExecuteCommand($"exec {CfgFolderName}/{CfgFileName}");
        _core.Engine.ExecuteCommand($"mp_freezetime {freezeTime}");
      }

      // Execute immediately
      ExecuteConfig();

      if (restartGame)
      {
        _core.Engine.ExecuteCommand("mp_restartgame 1");

        _core.Scheduler.DelayBySeconds(1.2f, ExecuteConfig);
        _core.Scheduler.DelayBySeconds(3.5f, ExecuteConfig);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Retakes: failed to apply retakes.cfg");
    }
  }

  private void GenerateCfgFile(string cfgPath, int freezeTime)
  {
    var buyMenuEnabled = _core.ConVar.Find<bool>("retakes_buymenu_enabled")?.Value ?? false;
    var pistol = _core.ConVar.Find<int>("retakes_buymenu_money_pistol")?.Value ?? 800;
    var half = _core.ConVar.Find<int>("retakes_buymenu_money_half")?.Value ?? 2500;
    var full = _core.ConVar.Find<int>("retakes_buymenu_money_full")?.Value ?? 5000;
    var maxMoney = Math.Max(Math.Clamp(pistol, 0, 16000), Math.Max(Math.Clamp(half, 0, 16000), Math.Clamp(full, 0, 16000)));

    var maxMoneyLine = buyMenuEnabled ? "mp_maxmoney 16000\n" : "mp_maxmoney 0\n";
    var startMoneyLine = "mp_startmoney 0\n";
    var playerAwardsLine = "mp_playercashawards 0\n";
    var teamAwardsLine = "mp_teamcashawards 0\n";

    var contents = $"""
      // Things you shouldn't change:
      mp_autoteambalance 0
      mp_forcecamera 1
      mp_give_player_c4 0
      mp_defuser_allocation 0
      mp_halftime 0
      mp_ignore_round_win_conditions 0
      mp_join_grace_time 0
      mp_match_can_clinch 0
      {maxMoneyLine.TrimEnd()}
      {startMoneyLine.TrimEnd()}
      mp_afterroundmoney 0
      mp_playercashawards 0
      mp_teamcashawards 0
      cash_player_respawn_amount 0
      cash_player_get_killed 0
      cash_player_bomb_planted 0
      cash_player_bomb_defused 0
      cash_player_damage_hostage 0
      cash_player_interact_with_hostage 0
      cash_player_killed_enemy_default 0
      cash_player_killed_enemy_factor 0
      cash_player_killed_hostage 1
      cash_player_rescued_hostage 0
      cash_team_elimination_bomb_map 0
      cash_team_elimination_hostage_map_ct 0
      cash_team_elimination_hostage_map_t 0
      cash_team_hostage_alive 0
      cash_team_hostage_interaction 0
      cash_team_loser_bonus 0
      cash_team_loser_bonus_consecutive_rounds 0
      cash_team_planted_bomb_but_defused 0
      cash_team_rescued_hostage 0
      cash_team_survive_guardian_wave 0
      cash_team_t_no_p_bomb_planted_ct_win 0
      cash_team_terrorist_win_bomb 0
      cash_team_win_by_defusing_bomb 0
      cash_team_win_by_hostage_rescue 0
      cash_team_win_by_time_limit 0
      {playerAwardsLine.TrimEnd()}
      mp_respawn_on_death_ct 0
      mp_respawn_on_death_t 0
      mp_solid_teammates 1
      {teamAwardsLine.TrimEnd()}
      mp_buytime 0
      mp_warmup_pausetimer 0
      sv_skirmish_id 0

      // Things you can change, and may want to:
      mp_roundtime_defuse 0.25
      mp_autokick 0
      mp_c4timer 40
      mp_freezetime {freezeTime}
      mp_friendlyfire 0
      mp_round_restart_delay 2
      sv_talk_enemy_dead 0
      sv_talk_enemy_living 0
      sv_deadtalk 1
      spec_replay_enable 0
      mp_maxrounds 30
      mp_match_end_restart 0
      mp_timelimit 0
      mp_match_restart_delay 10
      mp_death_drop_gun 1
      mp_death_drop_defuser 1
      mp_death_drop_grenade 1
      mp_warmuptime 15

      echo [Retakes] Config loaded!
      """;

    File.WriteAllText(cfgPath, contents);
  }
}
