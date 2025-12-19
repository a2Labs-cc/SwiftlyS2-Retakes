using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared;

namespace SwiftlyS2_Retakes.Handlers;

public sealed class MapEventHandlers
{
  private readonly Action<string> _onMapLoad;

  public MapEventHandlers(Action<string> onMapLoad)
  {
    _onMapLoad = onMapLoad;
  }

  public void Register(ISwiftlyCore core)
  {
    core.Event.OnMapLoad += OnMapLoad;
  }

  public void Unregister(ISwiftlyCore core)
  {
    core.Event.OnMapLoad -= OnMapLoad;
  }

  private void OnMapLoad(IOnMapLoadEvent @event)
  {
    _onMapLoad(@event.MapName);
  }
}
