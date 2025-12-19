using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Interfaces;

public interface IMessageService
{
  string FormatChat(string message);

  void Chat(IPlayer player, string message);

  void BroadcastChat(string message);
}
