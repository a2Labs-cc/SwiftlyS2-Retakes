using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace SwiftlyS2_Retakes.Services;

public sealed class DamageReportService : IDamageReportService
{
  private readonly ISwiftlyCore _core;
  private readonly IMessageService _messages;

  private readonly Dictionary<ulong, float> _scoreByPlayer = new();
  private const float ScoreDecay = 0.80f;

  private static ulong GetScoreKey(IPlayer player)
  {
    if (player is null || !player.IsValid) return 0;

    var steamId = player.SteamID;
    if (steamId != 0) return steamId;

    // Bots can have SteamID=0; use a stable key based on slot.
    // Prefix with a high bit range so it doesn't overlap plausible SteamIDs.
    return 0xF000000000000000UL | (uint)player.Slot;
  }

  private sealed class PairStats
  {
    public int Damage;
    public int Hits;
  }

  // attacker -> victim -> stats
  private readonly Dictionary<ulong, Dictionary<ulong, PairStats>> _byAttacker = new();

  public DamageReportService(ISwiftlyCore core, IMessageService messages)
  {
    _core = core;
    _messages = messages;
  }

  public void OnRoundStart(bool isWarmup)
  {
    _byAttacker.Clear();
    if (isWarmup)
    {
      _scoreByPlayer.Clear();
      return;
    }

    // Prune stale scores (e.g. bots disconnecting/rejoining and reusing slots).
    var activeKeys = _core.PlayerManager.GetAllPlayers()
      .Where(p => p is not null && p.IsValid)
      .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
      .Select(GetScoreKey)
      .Where(k => k != 0)
      .ToHashSet();

    if (_scoreByPlayer.Count > 0)
    {
      var keys = _scoreByPlayer.Keys.ToList();
      foreach (var k in keys)
      {
        if (!activeKeys.Contains(k)) _scoreByPlayer.Remove(k);
      }
    }
  }

  public void OnRoundEnd()
  {
    if (_scoreByPlayer.Count > 0)
    {
      var keys = _scoreByPlayer.Keys.ToList();
      foreach (var key in keys)
      {
        _scoreByPlayer[key] *= ScoreDecay;
      }
    }

    foreach (var (attackerSteamId, byVictim) in _byAttacker)
    {
      var dmg = 0;
      foreach (var (_, stats) in byVictim)
      {
        dmg += stats.Damage;
      }

      if (dmg <= 0) continue;

      if (_scoreByPlayer.TryGetValue(attackerSteamId, out var prev))
      {
        _scoreByPlayer[attackerSteamId] = prev + dmg;
      }
      else
      {
        _scoreByPlayer[attackerSteamId] = dmg;
      }
    }
  }

  public float GetPlayerScore(ulong steamId)
  {
    return _scoreByPlayer.TryGetValue(steamId, out var score) ? score : 0f;
  }

  public float GetPlayerScore(IPlayer player)
  {
    var key = GetScoreKey(player);
    if (key == 0) return 0f;
    return _scoreByPlayer.TryGetValue(key, out var score) ? score : 0f;
  }

  public void OnPlayerHurt(IPlayer attacker, IPlayer victim, int dmgHealth)
  {
    if (attacker is null || victim is null) return;
    if (!attacker.IsValid || !victim.IsValid) return;

    var attackerKey = GetScoreKey(attacker);
    var victimKey = GetScoreKey(victim);
    if (attackerKey == 0 || victimKey == 0) return;
    if (attackerKey == victimKey) return;

    // Only count T/CT interactions
    var attackerTeam = (Team)attacker.Controller.TeamNum;
    var victimTeam = (Team)victim.Controller.TeamNum;
    if ((attackerTeam != Team.T && attackerTeam != Team.CT) || (victimTeam != Team.T && victimTeam != Team.CT)) return;

    // Ignore team damage
    if (attackerTeam == victimTeam) return;

    if (dmgHealth <= 0) return;

    if (!_byAttacker.TryGetValue(attackerKey, out var byVictim))
    {
      byVictim = new Dictionary<ulong, PairStats>();
      _byAttacker[attackerKey] = byVictim;
    }

    if (!byVictim.TryGetValue(victimKey, out var stats))
    {
      stats = new PairStats();
      byVictim[victimKey] = stats;
    }

    stats.Damage += dmgHealth;
    stats.Hits += 1;
  }

  public void PrintRoundReport()
  {
    var players = _core.PlayerManager.GetAllPlayers()
      .Where(p => p is not null && p.IsValid)
      .Where(p => (Team)p.Controller.TeamNum == Team.T || (Team)p.Controller.TeamNum == Team.CT)
      .ToList();

    if (players.Count == 0) return;

    foreach (var viewer in players)
    {
      var viewerTeam = (Team)viewer.Controller.TeamNum;

      var opponents = players
        .Where(p => p.SteamID != viewer.SteamID)
        .Where(p => (Team)p.Controller.TeamNum != viewerTeam)
        .ToList();

      if (opponents.Count == 0) continue;

      var loc = _core.Translation.GetPlayerLocalizer(viewer);
      _messages.Chat(viewer, "\n" + loc["damage.report.header"].Colored());

      foreach (var opp in opponents)
      {
        GetStats(viewer.SteamID, opp.SteamID, out var dealtDmg, out var dealtHits);
        GetStats(opp.SteamID, viewer.SteamID, out var takenDmg, out var takenHits);

        var hp = 0;
        if (opp.Pawn is not null && opp.Pawn.IsValid)
        {
          try
          {
            hp = opp.Pawn.Health;
          }
          catch
          {
            hp = 0;
          }
        }

        _messages.Chat(viewer, loc["damage.report.line", dealtDmg, dealtHits, takenDmg, takenHits, opp.Controller.PlayerName, hp].Colored());
      }
    }
  }

  private void GetStats(ulong attackerSteamId, ulong victimSteamId, out int dmg, out int hits)
  {
    dmg = 0;
    hits = 0;

    if (!_byAttacker.TryGetValue(attackerSteamId, out var byVictim)) return;
    if (!byVictim.TryGetValue(victimSteamId, out var stats)) return;

    dmg = stats.Damage;
    hits = stats.Hits;
  }
}
