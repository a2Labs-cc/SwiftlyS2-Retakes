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
      return;
    }
  }

  public void OnPlayerHurt(IPlayer attacker, IPlayer victim, int dmgHealth)
  {
    if (attacker is null || victim is null) return;
    if (!attacker.IsValid || !victim.IsValid) return;
    if (attacker.SteamID == victim.SteamID) return;

    // Only count T/CT interactions
    var attackerTeam = (Team)attacker.Controller.TeamNum;
    var victimTeam = (Team)victim.Controller.TeamNum;
    if ((attackerTeam != Team.T && attackerTeam != Team.CT) || (victimTeam != Team.T && victimTeam != Team.CT)) return;

    // Ignore team damage
    if (attackerTeam == victimTeam) return;

    if (dmgHealth <= 0) return;

    if (!_byAttacker.TryGetValue(attacker.SteamID, out var byVictim))
    {
      byVictim = new Dictionary<ulong, PairStats>();
      _byAttacker[attacker.SteamID] = byVictim;
    }

    if (!byVictim.TryGetValue(victim.SteamID, out var stats))
    {
      stats = new PairStats();
      byVictim[victim.SteamID] = stats;
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
      _messages.Chat(viewer, loc["damage.report.header"].Colored());

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
