using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;

namespace SwiftlyS2_Retakes.Services;

public sealed class PawnLifecycleService : IPawnLifecycleService
{
  private readonly Dictionary<int, List<PendingAction>> _pendingBySlot = new();
  private int _roundToken;

  private readonly record struct PendingAction(int RoundToken, Action<IPlayer> Action);

  public void OnRoundPrestart()
  {
    _roundToken++;
  }

  public void Reset()
  {
    _pendingBySlot.Clear();
    _roundToken = 0;
  }

  public string DebugSummary()
  {
    var queued = _pendingBySlot.Values.Sum(l => l.Count);
    var slots = _pendingBySlot.Count;
    return $"Retakes: pawn queue => roundToken={_roundToken}, queued={queued}, slots={slots}";
  }

  public void WhenPawnReady(IPlayer player, Action<IPlayer> action)
  {
    if (player is null) return;
    if (action is null) return;

    if (player.IsValid && player.Pawn is not null)
    {
      action(player);
      return;
    }

    var slot = player.Slot;
    if (!_pendingBySlot.TryGetValue(slot, out var list))
    {
      list = new List<PendingAction>();
      _pendingBySlot[slot] = list;
    }

    list.Add(new PendingAction(_roundToken, action));
  }

  public void OnPlayerSpawn(IPlayer player)
  {
    if (player is null) return;

    var slot = player.Slot;
    if (!_pendingBySlot.TryGetValue(slot, out var list) || list.Count == 0)
    {
      return;
    }

    if (!player.IsValid || player.Pawn is null)
    {
      return;
    }

    var toRun = list.Where(x => x.RoundToken == _roundToken).ToList();
    _pendingBySlot.Remove(slot);

    foreach (var pending in toRun)
    {
      pending.Action(player);
    }
  }
}
