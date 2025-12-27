using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for announcing bombsite and round information.
/// </summary>
public interface IAnnouncementService
{
  /// <summary>
  /// Announces the bombsite and round type to all players.
  /// </summary>
  /// <param name="bombsite">The bombsite for this round</param>
  /// <param name="roundType">The round type</param>
  /// <param name="lastWinner">The winner of the last round</param>
  void AnnounceBombsite(Bombsite bombsite, RoundType roundType, Team lastWinner);

  /// <summary>
  /// Announces the specific plant position name to Terrorist players.
  /// </summary>
  /// <param name="siteName">The name of the spawn point where the bomb is being planted</param>
  void AnnouncePlantSite(string siteName);

  /// <summary>
  /// Announces team win and streak information to all players.
  /// </summary>
  /// <param name="winner">The winning team</param>
  /// <param name="consecutiveWins">Number of consecutive wins</param>
  void AnnounceTeamWin(Team winner, int consecutiveWins);

  void ClearAnnouncement();
}
