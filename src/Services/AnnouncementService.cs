using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class AnnouncementService : IAnnouncementService
{
  private readonly ISwiftlyCore _core;
  private readonly IRetakesConfigService _config;
  private readonly IMessageService _messages;
  private readonly IRetakesStateService _state;

  public AnnouncementService(ISwiftlyCore core, IRetakesConfigService config, IMessageService messages, IRetakesStateService state)
  {
    _core = core;
    _config = config;
    _messages = messages;
    _state = state;
  }

  public void ClearAnnouncement()
  {
    foreach (var player in _core.PlayerManager.GetAllPlayers())
    {
      if (player is null || !player.IsValid) continue;
      player.SendCenterHTML(string.Empty, 1);
    }
  }

  public void AnnounceBombsite(Bombsite bombsite, RoundType roundType, Team lastWinner)
  {
    var site = bombsite == Bombsite.A ? "A" : "B";

    var buyMenuEnabled = _core.ConVar.Find<bool>("retakes_buymenu_enabled")?.Value ?? false;

    var alive = _core.PlayerManager.GetAllPlayers()
      .Where(p => p.IsValid && p.Controller.PawnIsAlive)
      .ToList();

    var ctAlive = alive.Count(p => (Team)p.Controller.TeamNum == Team.CT);
    var tAlive = alive.Count(p => (Team)p.Controller.TeamNum == Team.T);

    var roundTypeText = roundType switch
    {
      RoundType.Pistol => "Pistol",
      RoundType.HalfBuy => "HalfBuy",
      _ => "FullBuy"
    };

    var roundModeText = roundType switch
    {
      RoundType.Pistol => "Pistol",
      RoundType.HalfBuy => "Half Buy",
      _ => "Full Buy"
    };

    var img = bombsite == Bombsite.A
      ? _config.Config.Announcement.BombsiteAimg
      : _config.Config.Announcement.BombsiteBimg;

    var htmlMessage =
      $"<div style='text-align:center;'>" +
      $"<img src='{img}' width='320' height='40' style='margin-bottom: 10px;'></img>" +
      $"<br>" +
      $"<font class='fontSize-m' color='white'>Mode: </font><b><font class='fontSize-m' color='#ff4d4d'>{roundModeText}</font></b><br>" +
      $"<font class='fontSize-m' color='white'>" +
      $"<font color='#4da3ff'>{ctAlive}</font> vs " +
      $"<font color='#ff4d4d'>{tAlive}</font>" +
      $"</font>" +
      $"</div>";

    foreach (var player in _core.PlayerManager.GetAllPlayers())
    {
      if (player is null || !player.IsValid) continue;
      var loc = _core.Translation.GetPlayerLocalizer(player);

      // Winner + streak: send as separate chat lines to avoid collapsing
      if (lastWinner == Team.CT)
      {
        _messages.Chat(player, loc["round.win_message", "Counter-Terrorists"].Colored());
      }
      else if (lastWinner == Team.T)
      {
        _messages.Chat(player, loc["round.win_message", "Terrorists"].Colored());
      }

      if (_state.ConsecutiveWins > 0 && _state.LastWinner == lastWinner)
      {
        _messages.Chat(player, loc["round.win_streak", _state.ConsecutiveWins].Colored());
      }

      // Spacer between win block and round info (no prefix)
      player.SendMessage(MessageType.Chat, "\n");

      // Round info: send "Now" and retake/defend as separate chat messages
      _messages.Chat(player, loc["round.now", roundTypeText].Colored());

      var playerTeam = (Team)player.Controller.TeamNum;
      if (playerTeam == Team.CT)
      {
        _messages.Chat(player, loc["round.retake", site, ctAlive, tAlive].Colored());
      }
      else if (playerTeam == Team.T)
      {
        _messages.Chat(player, loc["round.defend", site, tAlive, ctAlive].Colored());
      }

      player.SendCenterHTML(htmlMessage);

      if (buyMenuEnabled)
      {
        player.SendCenter("Press [B] to change weapons");
      }
    }
  }

  public void AnnouncePlantSite(string siteName)
  {
    foreach (var player in _core.PlayerManager.GetAllPlayers())
    {
      if (player is null || !player.IsValid) continue;
      if ((Team)player.Controller.TeamNum == Team.T)
      {
        var loc = _core.Translation.GetPlayerLocalizer(player);
        _messages.Chat(player, loc["announcement.planting_at", siteName].Colored());
      }
    }
  }
}
