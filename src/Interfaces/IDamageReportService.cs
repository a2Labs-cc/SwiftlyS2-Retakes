using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Interfaces;

public interface IDamageReportService
{
  void OnRoundStart(bool isWarmup);

  void OnRoundEnd();

  void OnPlayerHurt(IPlayer attacker, IPlayer victim, int dmgHealth);

  float GetPlayerScore(ulong steamId);

  float GetPlayerScore(IPlayer player);

  void PrintRoundReport();
}
