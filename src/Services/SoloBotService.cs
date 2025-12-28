using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
using SwiftlyS2_Retakes.Utils;

namespace SwiftlyS2_Retakes.Services;

public sealed class SoloBotService : ISoloBotService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;

  private readonly IConVar<bool> _enabled;
  private readonly IConVar<int> _difficulty;

  private long _lastActionTick;
  private const int ActionCooldownMs = 2000;

  private bool _wasEnabled;

  public SoloBotService(ISwiftlyCore core, ILogger logger)
  {
    _core = core;
    _logger = logger;

    _enabled = core.ConVar.CreateOrFind("retakes_solo_bot_enabled", "Spawn 1 bot when there is exactly 1 human player", false);
    _difficulty = core.ConVar.CreateOrFind("retakes_solo_bot_difficulty", "Solo bot difficulty", 2, 0, 5);
  }

  public void UpdateSoloBot()
  {
    try
    {
      var nowTick = Environment.TickCount64;
      if (!_enabled.Value)
      {
        var botQuota = _core.ConVar.Find<int>("bot_quota")?.Value ?? 0;
        var botsPresent = _core.PlayerManager.GetAllPlayers().Any(p => p.IsValid && PlayerUtil.IsBot(p));

        if ((_wasEnabled || botQuota > 0 || botsPresent) && nowTick - _lastActionTick >= ActionCooldownMs)
        {
          _lastActionTick = nowTick;
          _logger.LogPluginDebug("Retakes: solo-bot: disabling bots (feature disabled)");
          DisableBots();
        }

        _wasEnabled = false;
        return;
      }

      _wasEnabled = true;
      var players = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

      var humansPlaying = players
        .Where(PlayerUtil.IsHuman)
        .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
        .ToList();

      if (humansPlaying.Count >= 2)
      {
        if (nowTick - _lastActionTick >= ActionCooldownMs)
        {
          _lastActionTick = nowTick;
          _logger.LogPluginDebug("Retakes: solo-bot: disabling bots (humansPlaying={Humans})", humansPlaying.Count);
          DisableBots();
        }
        return;
      }

      if (humansPlaying.Count == 0)
      {
        return;
      }

      var human = humansPlaying[0];
      var humanTeam = (Team)human.Controller.TeamNum;
      var desiredBotTeam = humanTeam == Team.T ? Team.CT : Team.T;

      var bots = players
        .Where(PlayerUtil.IsBot)
        .ToList();

      var botsPlaying = bots
        .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
        .ToList();

      if (botsPlaying.Count > 0)
      {
        var bot = botsPlaying[0];
        var botTeam = (Team)bot.Controller.TeamNum;
        if (botTeam == humanTeam)
        {
          _logger.LogPluginDebug("Retakes: solo-bot: moving existing bot to opposite team (botTeam={BotTeam}, humanTeam={HumanTeam})", botTeam, humanTeam);
          bot.ChangeTeam(desiredBotTeam);
          _lastActionTick = nowTick;
        }
        return;
      }

      if (nowTick - _lastActionTick < ActionCooldownMs)
      {
        return;
      }

      _lastActionTick = nowTick;

      var diff = Math.Clamp(_difficulty.Value, 0, 5);
      _core.Engine.ExecuteCommand($"bot_difficulty {diff}");
      _core.Engine.ExecuteCommand("bot_quota_mode normal");

      EnsureBotsCanMove();

      var joinTeam = desiredBotTeam == Team.CT ? "CT" : "T";
      _core.Engine.ExecuteCommand($"bot_join_team {joinTeam}");
      _core.Engine.ExecuteCommand("bot_quota 1");

      var quotaVar = _core.ConVar.Find<int>("bot_quota");
      if (quotaVar is not null)
      {
        _logger.LogPluginDebug(
          "Retakes: solo-bot: enforced quota (humansPlaying={Humans}) desiredTeam={Team} bot_quota={Quota}",
          humansPlaying.Count,
          joinTeam,
          quotaVar.Value);
      }
    }
    catch (Exception ex)
    {
      _logger.LogPluginError(ex, "Retakes: solo-bot exception");
    }
  }

  private void DisableBots()
  {
    _core.Engine.ExecuteCommand("bot_join_team any");
    _core.Engine.ExecuteCommand("bot_quota_mode normal");
    _core.Engine.ExecuteCommand("bot_quota 0");
    _core.Engine.ExecuteCommand("bot_kick");
  }

  private void EnsureBotsCanMove()
  {
    _core.Engine.ExecuteCommand("bot_stop 0");
    _core.Engine.ExecuteCommand("bot_freeze 0");
    _core.Engine.ExecuteCommand("bot_zombie 0");
    _core.Engine.ExecuteCommand("bot_mimic 0");
    _core.Engine.ExecuteCommand("bot_mimic_yaw_offset 0");
    _core.Engine.ExecuteCommand("bot_dont_shoot 0");
  }
}
