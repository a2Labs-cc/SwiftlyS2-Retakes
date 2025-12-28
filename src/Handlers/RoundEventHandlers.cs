using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;
using SwiftlyS2_Retakes.Utils;
using System;
using System.Linq;

namespace SwiftlyS2_Retakes.Handlers;

public sealed class RoundEventHandlers
{
  private ISwiftlyCore? _core;
  private readonly IPawnLifecycleService _pawnLifecycle;
  private readonly ISpawnManager _spawnManager;
  private readonly IRetakesStateService _state;
  private readonly IRetakesConfigService _config;
  private readonly ISoloBotService _soloBot;
  private readonly IAnnouncementService _announcement;
  private readonly IMessageService _messages;
  private readonly IAllocationService _allocation;
  private readonly IAutoPlantService _autoPlant;
  private readonly IClutchAnnounceService _clutch;
  private readonly IDamageReportService _damageReport;
  private readonly IBreakerService? _breaker;
  private readonly Random _random;
  private readonly IQueueService _queue;
  private readonly IBuyMenuService _buyMenu;
  private readonly ISmokeScenarioService _smokeScenario;

  private Bombsite? _currentBombsite;

  private IConVar<bool>? _teamBalanceEnabled;
  private IConVar<float>? _teamBalanceTerroristRatio;
  private IConVar<bool>? _teamBalanceForceEvenOn10;

  private IConVar<bool>? _smokeScenariosEnabled;
  private IConVar<bool>? _smokeScenarioRandomRoundsEnabled;
  private IConVar<float>? _smokeScenarioRandomRoundChance;

  private int _consecutiveTWins;

  private Guid _roundAnnounceMatchStartHook;
  private Guid _roundPrestartHook;
  private Guid _roundStartHook;
  private Guid _roundPoststartHook;
  private Guid _roundFreezeEndHook;
  private Guid _roundEndHook;
  private Guid _warmupEndHook;

  public RoundEventHandlers(
    IPawnLifecycleService pawnLifecycle,
    ISpawnManager spawnManager,
    IRetakesStateService state,
    IRetakesConfigService config,
    ISoloBotService soloBot,
    IAnnouncementService announcement,
    IMessageService messages,
    IAllocationService allocation,
    IAutoPlantService autoPlant,
    IClutchAnnounceService clutch,
    IDamageReportService damageReport,
    IBreakerService? breaker,
    Random random,
    IQueueService queue,
    IBuyMenuService buyMenu,
    ISmokeScenarioService smokeScenario)
  {
    _pawnLifecycle = pawnLifecycle;
    _spawnManager = spawnManager;
    _state = state;
    _config = config;
    _soloBot = soloBot;
    _announcement = announcement;
    _messages = messages;
    _allocation = allocation;
    _autoPlant = autoPlant;
    _clutch = clutch;
    _damageReport = damageReport;
    _breaker = breaker;
    _random = random;
    _queue = queue;
    _buyMenu = buyMenu;
    _smokeScenario = smokeScenario;
  }

  public void Register(ISwiftlyCore core)
  {
    _core = core;

    _teamBalanceEnabled = core.ConVar.CreateOrFind("retakes_team_balance_enabled", "Enable team balance", true);
    _teamBalanceTerroristRatio = core.ConVar.CreateOrFind("retakes_team_balance_terrorist_ratio", "Team balance terrorist ratio", 0.45f, 0f, 1f);
    _teamBalanceForceEvenOn10 = core.ConVar.CreateOrFind("retakes_team_balance_force_even_when_players_mod_10", "Force even teams when player count is multiple of 10", true);

    _smokeScenariosEnabled = core.ConVar.CreateOrFind(
      "retakes_smoke_scenarios_enabled",
      "Enable smoke scenarios",
      true);
    _smokeScenarioRandomRoundsEnabled = core.ConVar.CreateOrFind(
      "retakes_smoke_scenarios_random_rounds_enabled",
      "Only spawn smoke scenarios on random rounds",
      false);
    _smokeScenarioRandomRoundChance = core.ConVar.CreateOrFind(
      "retakes_smoke_scenarios_random_round_chance",
      "Chance [0-1] to spawn smoke scenarios when random rounds are enabled",
      0.25f,
      0f,
      1f);

    _roundPrestartHook = core.GameEvent.HookPre<EventRoundPrestart>(OnRoundPrestart);
    _roundAnnounceMatchStartHook = core.GameEvent.HookPre<EventRoundAnnounceMatchStart>(OnRoundAnnounceMatchStart);
    _roundStartHook = core.GameEvent.HookPre<EventRoundStart>(OnRoundStart);
    _roundPoststartHook = core.GameEvent.HookPre<EventRoundPoststart>(OnRoundPoststart);
    _roundFreezeEndHook = core.GameEvent.HookPost<EventRoundFreezeEnd>(OnRoundFreezeEnd);
    _roundEndHook = core.GameEvent.HookPre<EventRoundEnd>(OnRoundEnd);
    _warmupEndHook = core.GameEvent.HookPre<EventWarmupEnd>(OnWarmupEnd);
  }

  public void Unregister(ISwiftlyCore core)
  {
    if (_roundPrestartHook != Guid.Empty) core.GameEvent.Unhook(_roundPrestartHook);
    if (_roundStartHook != Guid.Empty) core.GameEvent.Unhook(_roundStartHook);
    if (_roundPoststartHook != Guid.Empty) core.GameEvent.Unhook(_roundPoststartHook);
    if (_roundFreezeEndHook != Guid.Empty) core.GameEvent.Unhook(_roundFreezeEndHook);
    if (_roundEndHook != Guid.Empty) core.GameEvent.Unhook(_roundEndHook);
    if (_warmupEndHook != Guid.Empty) core.GameEvent.Unhook(_warmupEndHook);

    _roundPrestartHook = Guid.Empty;
    _roundStartHook = Guid.Empty;
    _roundPoststartHook = Guid.Empty;
    _roundFreezeEndHook = Guid.Empty;
    _roundEndHook = Guid.Empty;
    _warmupEndHook = Guid.Empty;

    _core = null;
  }

  private HookResult OnWarmupEnd(EventWarmupEnd @event)
  {
    var core = _core;
    if (core is null) return HookResult.Continue;

    // Warmup-end can fire before CCSGameRules.WarmupPeriod flips, so delay slightly.
    core.Scheduler.DelayBySeconds(1.0f, () =>
    {
      _config.ApplyToConvars(restartGame: false);
    });

    return HookResult.Continue;
  }

  private void AnnounceSmokeScenario(Bombsite bombsite, bool shouldSpawnSmokes, SmokeScenario? chosenScenario)
  {
    var core = _core;
    if (core is null) return;

    var enabled = _config.Config.SmokeScenarios.Enabled;
    var forced = _state.SmokesForced;

    if (!enabled && !forced)
    {
      return;
    }

    var players = core.PlayerManager.GetAllPlayers()
      .Where(p => p is not null && p.IsValid)
      .Where(p => (Team)p.Controller.TeamNum == Team.T)
      .ToList();

    if (players.Count == 0) return;

    foreach (var player in players)
    {
      var loc = core.Translation.GetPlayerLocalizer(player);
      if (!shouldSpawnSmokes || chosenScenario is null)
      {
        _messages.Chat(player, loc["smokes.scenario.none"].Colored());
        continue;
      }

      var scenarioName = string.IsNullOrWhiteSpace(chosenScenario.Name) ? "Unnamed" : chosenScenario.Name.Trim();
      _messages.Chat(player, loc["smokes.scenario.active", scenarioName].Colored());
    }
  }

  private HookResult OnRoundAnnounceMatchStart(EventRoundAnnounceMatchStart @event)
  {
    _buyMenu.OnRoundStart();
    return HookResult.Continue;
  }

  private HookResult OnRoundPrestart(EventRoundPrestart @event)
  {
    _autoPlant.EnforceNoC4();
    
    // Update queue - move players from queue to active
    if (_config.Config.Queue.Enabled)
    {
      _queue.Update();
    }
    
    TryScrambleTeams();
    TryBalanceTeams();
    _soloBot.UpdateSoloBot();
    _pawnLifecycle.OnRoundPrestart();
    return HookResult.Continue;
  }

  private void TryScrambleTeams()
  {
    var core = _core;
    if (core is null) return;

    var rules = core.EntitySystem.GetGameRules();
    if (rules is not null && rules.WarmupPeriod) return;

    var cfg = _config.Config.TeamBalance;
    if (!cfg.ScrambleEnabled && !_state.ScrambleNextRound)
    {
      return;
    }

    // Auto scramble: consecutive T wins
    if (cfg.ScrambleEnabled)
    {
      if (_state.LastWinner == Team.T)
      {
        _consecutiveTWins++;
      }
      else if (_state.LastWinner == Team.CT)
      {
        _consecutiveTWins = 0;
      }

      var roundsToScramble = Math.Clamp(cfg.RoundsToScramble, 1, 100);
      if (_consecutiveTWins >= roundsToScramble)
      {
        _state.ScrambleNextRound = true;
      }
    }

    if (!_state.ScrambleNextRound)
    {
      return;
    }

    var players = core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid)
      .Where(PlayerUtil.IsHuman)
      .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
      .OrderBy(_ => _random.Next())
      .ToList();

    var total = players.Count;
    if (total < 2)
    {
      _state.ScrambleNextRound = false;
      _consecutiveTWins = 0;
      return;
    }

    var targetT = GetTargetTCount(total);

    var newT = players.Take(targetT).ToList();
    var newCt = players.Skip(targetT).ToList();

    _state.BeginTeamChangeBypass();
    try
    {
      foreach (var p in newT)
      {
        p.ChangeTeam(Team.T);
      }

      foreach (var p in newCt)
      {
        p.ChangeTeam(Team.CT);
      }
    }
    finally
    {
      _state.EndTeamChangeBypass();
    }

    _state.ScrambleNextRound = false;
    _consecutiveTWins = 0;
  }

  private int GetTargetTCount(int totalPlayers)
  {
    totalPlayers = Math.Max(0, totalPlayers);
    if (totalPlayers <= 1) return 0;

    var forceEvenVar = _teamBalanceForceEvenOn10;
    var ratioVar = _teamBalanceTerroristRatio;

    var forceEven = forceEvenVar?.Value ?? true;
    var ratio = ratioVar?.Value ?? 0.45f;

    var useEven = forceEven && totalPlayers % 10 == 0;
    var targetRatio = useEven ? 0.5f : Math.Clamp(ratio, 0f, 1f);

    var targetT = (int)MathF.Round(targetRatio * totalPlayers);
    return Math.Clamp(targetT, 1, totalPlayers - 1);
  }

  private void TryBalanceTeams()
  {
    var core = _core;
    if (core is null) return;

    var enabled = _teamBalanceEnabled;
    var ratioVar = _teamBalanceTerroristRatio;
    var forceEven = _teamBalanceForceEvenOn10;
    if (enabled is null || ratioVar is null || forceEven is null) return;
    if (!enabled.Value) return;

    var rules = core.EntitySystem.GetGameRules();
    if (rules is not null && rules.WarmupPeriod) return;

    var players = core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid)
      .Where(PlayerUtil.IsHuman)
      .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
      .ToList();

    var total = players.Count;
    if (total < 2) return;

    var useEven = forceEven.Value && total % 10 == 0;
    var ratio = useEven ? 0.5f : Math.Clamp(ratioVar.Value, 0f, 1f);

    var targetT = (int)MathF.Round(ratio * total);
    targetT = Math.Clamp(targetT, 1, total - 1);

    var currentT = players.Count(p => (Team)p.Controller.TeamNum == Team.T);
    if (currentT == targetT) return;

    if (currentT > targetT)
    {
      var moveCount = currentT - targetT;
      var candidates = players
        .Where(p => (Team)p.Controller.TeamNum == Team.T)
        .OrderBy(_ => _random.Next())
        .Take(moveCount)
        .ToList();

      foreach (var p in candidates)
      {
        _state.BeginTeamChangeBypass();
        try
        {
          p.ChangeTeam(Team.CT);
        }
        finally
        {
          _state.EndTeamChangeBypass();
        }
      }
    }
    else
    {
      var moveCount = targetT - currentT;
      var candidates = players
        .Where(p => (Team)p.Controller.TeamNum == Team.CT)
        .OrderBy(_ => _random.Next())
        .Take(moveCount)
        .ToList();

      foreach (var p in candidates)
      {
        _state.BeginTeamChangeBypass();
        try
        {
          p.ChangeTeam(Team.T);
        }
        finally
        {
          _state.EndTeamChangeBypass();
        }
      }
    }
  }

  private HookResult OnRoundStart(EventRoundStart @event)
  {
    _autoPlant.EnforceNoC4();
    _clutch.OnRoundStart();
    var isWarmup = false;
    var core = _core;
    if (core is not null)
    {
      var rules = core.EntitySystem.GetGameRules();
      isWarmup = rules is not null && rules.WarmupPeriod;
    }

    _damageReport.OnRoundStart(isWarmup);

    _state.OnRoundStart(isWarmup);

    _buyMenu.OnRoundStart();

    if (isWarmup)
    {
      return HookResult.Continue;
    }

    _breaker?.HandleRoundStart();

    // Lock teams for mid-round protection
    if (_config.Config.Queue.Enabled)
    {
      _queue.SetRoundTeams();
    }

    if (core is not null)
    {
      var participants = core.PlayerManager.GetAllPlayers()
        .Where(p => p.IsValid)
        .Where(PlayerUtil.IsHuman)
        .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
        .Select(p => (p.SteamID, (Team)p.Controller.TeamNum))
        .ToList();
      _state.SetRoundParticipants(participants);
    }

    var bombsite = _state.ForcedBombsite ?? (_random.Next(0, 2) == 0 ? Bombsite.A : Bombsite.B);
    _currentBombsite = bombsite;
    _spawnManager.HandleRoundSpawns(bombsite);
    _spawnManager.OpenCtSpawnSelectionMenu(bombsite);
    _allocation.AllocateForCurrentPlayers(_pawnLifecycle);

    var shouldSpawnSmokes = true;
    if (_state.SmokesForced)
    {
      shouldSpawnSmokes = true;
    }
    else
    {
      if (!_config.Config.SmokeScenarios.Enabled)
      {
        shouldSpawnSmokes = false;
      }
      else if (_smokeScenarioRandomRoundsEnabled?.Value == true)
      {
        var chance = Math.Clamp(_smokeScenarioRandomRoundChance?.Value ?? 0f, 0f, 1f);
        shouldSpawnSmokes = _random.NextDouble() < chance;
      }
    }

    SmokeScenario? chosenScenario = null;
    if (shouldSpawnSmokes)
    {
      chosenScenario = _smokeScenario.SpawnSmokesForBombsite(bombsite);
    }

    var roundType = _allocation.CurrentRoundType ?? RoundType.FullBuy;
    _announcement.AnnounceBombsite(bombsite, roundType, _state.LastWinner);

    if (_spawnManager.TryGetAssignedPlanter(out _, out var planterSpawn) && !string.IsNullOrEmpty(planterSpawn.Name))
    {
      _announcement.AnnouncePlantSite(planterSpawn.Name);
    }

    _buyMenu.OnRoundTypeSelected();

    // Announce smoke scenario last
    AnnounceSmokeScenario(bombsite, shouldSpawnSmokes, chosenScenario);

    return HookResult.Continue;
  }

  private HookResult OnRoundPoststart(EventRoundPoststart @event)
  {
    return HookResult.Continue;
  }

  private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
  {
    _spawnManager.CloseSpawnMenus();
    _announcement.ClearAnnouncement();

    if (_currentBombsite is not null)
    {
      if (_spawnManager.TryGetAssignedPlanter(out var steamId, out var spawn))
      {
        _autoPlant.TryAutoPlant(_currentBombsite.Value, steamId, spawn);
      }
      else
      {
        _autoPlant.TryAutoPlant(_currentBombsite.Value);
      }
    }
    return HookResult.Continue;
  }

  private HookResult OnRoundEnd(EventRoundEnd @event)
  {
    var winner = (Team)@event.Winner;
    if (winner == Team.T || winner == Team.CT)
    {
      _state.OnRoundEnd(winner, @event.Reason, @event.Message);
    }
    else
    {
      _state.OnRoundEnd(Team.None, @event.Reason, @event.Message);
    }

    // Announce team win and streak
    if (winner == Team.T || winner == Team.CT)
    {
      _announcement.AnnounceTeamWin(winner, _state.ConsecutiveWins);
    }

    // Clear round team locks
    if (_config.Config.Queue.Enabled)
    {
      _queue.ClearRoundTeams();
    }

    // Move late joiners from spectator to a team after the round ends.
    var core = _core;
    if (core is not null)
    {
      var joiners = _state.DrainPendingJoiners();
      if (joiners.Count > 0)
      {
        var teamPlayers = core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
        var tCount = teamPlayers.Count(p => (Team)p.Controller.TeamNum == Team.T);
        var ctCount = teamPlayers.Count(p => (Team)p.Controller.TeamNum == Team.CT);

        foreach (var steamId in joiners)
        {
          var p = teamPlayers.FirstOrDefault(x => x.SteamID == steamId);
          if (p is null || !p.IsValid) continue;

          // Only move if still spectator.
          var team = (Team)p.Controller.TeamNum;
          if (team != Team.Spectator && team != Team.None) continue;

          var target = tCount <= ctCount ? Team.T : Team.CT;
          p.ChangeTeam(target);

          if (target == Team.T) tCount++;
          else ctCount++;
        }
      }
    }

    if (winner == Team.T || winner == Team.CT)
    {
      _clutch.OnRoundEnd(winner);
    }
    else
    {
      _clutch.OnRoundEnd(Team.None);
    }

    _damageReport.PrintRoundReport();

    return HookResult.Continue;
  }
}
