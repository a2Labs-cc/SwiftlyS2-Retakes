using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;

namespace SwiftlyS2_Retakes.Services;

public sealed class ClutchAnnounceService : IClutchAnnounceService
{
  private const int MinOpponents = 3;

  private readonly ISwiftlyCore _core;
  private readonly IMessageService _messages;

  private Team? _clutchTeam;
  private ulong? _clutchSteamId;
  private int _opponents;

  public ClutchAnnounceService(ISwiftlyCore core, IMessageService messages)
  {
    _core = core;
    _messages = messages;
  }

  public void OnRoundStart()
  {
    Reset();
  }

  public void OnPlayerCountMayHaveChanged()
  {
    if (_clutchSteamId is not null) return;

    var alive = _core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid && p.Controller.PawnIsAlive)
      .ToList();

    var aliveT = alive.Where(p => (Team)p.Controller.TeamNum == Team.T).ToList();
    var aliveCt = alive.Where(p => (Team)p.Controller.TeamNum == Team.CT).ToList();

    if (aliveT.Count == 1 && aliveCt.Count >= MinOpponents)
    {
      _clutchTeam = Team.T;
      _clutchSteamId = aliveT[0].SteamID;
      _opponents = aliveCt.Count;
    }
    else if (aliveCt.Count == 1 && aliveT.Count >= MinOpponents)
    {
      _clutchTeam = Team.CT;
      _clutchSteamId = aliveCt[0].SteamID;
      _opponents = aliveT.Count;
    }
  }

  public void OnRoundEnd(Team winner)
  {
    if (_clutchTeam is null || _clutchSteamId is null) { Reset(); return; }

    if (winner == _clutchTeam)
    {
      var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == _clutchSteamId.Value);
      var name = player?.Controller?.PlayerName ?? "unknown";
      _messages.BroadcastChat($"{name} clutched 1v{_opponents}");
    }

    Reset();
  }

  private void Reset()
  {
    _clutchTeam = null;
    _clutchSteamId = null;
    _opponents = 0;
  }
}
