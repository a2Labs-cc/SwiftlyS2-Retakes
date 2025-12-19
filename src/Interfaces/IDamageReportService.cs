using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Interfaces;

public interface IDamageReportService
{
  void OnRoundStart(bool isWarmup);

  void OnPlayerHurt(IPlayer attacker, IPlayer victim, int dmgHealth);

  void PrintRoundReport();
}
