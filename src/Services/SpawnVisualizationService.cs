using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class SpawnVisualizationService : ISpawnVisualizationService
{
  private readonly ILogger _logger;
  private readonly ISwiftlyCore _core;

  private readonly List<uint> _beamEntityIndices = new();

  private readonly Dictionary<int, List<uint>> _textEntityIndicesByViewer = new();
  private readonly Dictionary<uint, Vector> _textPositions = new();
  private readonly HashSet<int> _activeViewers = new();

  private bool _tickHandlerRegistered;

  public SpawnVisualizationService(ILogger logger, ISwiftlyCore core)
  {
    _logger = logger;
    _core = core;
  }

  private void EnsureTickHandlerRegistered()
  {
    if (_tickHandlerRegistered) return;
    _core.Event.OnTick += OnTick;
    _tickHandlerRegistered = true;
  }

  private void UnregisterTickHandlerIfNotNeeded()
  {
    if (!_tickHandlerRegistered) return;
    if (_activeViewers.Count != 0) return;

    _core.Event.OnTick -= OnTick;
    _tickHandlerRegistered = false;
  }

  private void OnTick()
  {
    foreach (var viewerSlot in _activeViewers)
    {
      var player = _core.PlayerManager.GetPlayer(viewerSlot);
      if (player is null || !player.IsValid || player.PlayerPawn is null) continue;

      var playerPos = player.PlayerPawn.AbsOrigin;
      if (playerPos is null) continue;

      if (!_textEntityIndicesByViewer.TryGetValue(viewerSlot, out var textIndices)) continue;

      foreach (var index in textIndices)
      {
        var text = _core.EntitySystem.GetEntityByIndex<CPointWorldText>(index);
        if (text is null || !text.IsValid) continue;

        if (!_textPositions.TryGetValue(index, out var textPos)) continue;

        var dx = playerPos.Value.X - textPos.X;
        var dy = playerPos.Value.Y - textPos.Y;
        var yaw = MathF.Atan2(dy, dx) * (180f / MathF.PI) + 90f;

        var newAngles = new QAngle(0f, yaw, 90f);
        text.Teleport(textPos, newAngles, Vector.Zero);
      }
    }
  }

  public void ShowSpawns(IEnumerable<Spawn> spawns, Bombsite bombsite)
  {
    HideSpawns();

    var spawnList = spawns.Where(s => (bombsite == Bombsite.Both || s.Bombsite == bombsite) && (s.Team == Team.T || s.Team == Team.CT)).ToList();
    if (spawnList.Count == 0)
    {
      return;
    }

    foreach (var spawn in spawnList)
    {
      CreateBeam(spawn);
    }

    var viewers = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
    foreach (var viewer in viewers)
    {
      EnsureViewerInitialized(viewer);
      foreach (var spawn in spawnList)
      {
        CreateLabelForViewer(viewer, spawn, viewers);
      }
    }

    _logger.LogInformation("Retakes: Showing {Count} spawns for {Bombsite}", spawnList.Count, bombsite);
  }

  public void HideSpawns()
  {
    foreach (var idx in _beamEntityIndices)
    {
      var beam = _core.EntitySystem.GetEntityByIndex<CBeam>(idx);
      if (beam is not null && beam.IsValid)
      {
        beam.Despawn();
      }
    }

    foreach (var kvp in _textEntityIndicesByViewer)
    {
      foreach (var idx in kvp.Value)
      {
        var text = _core.EntitySystem.GetEntityByIndex<CPointWorldText>(idx);
        if (text is not null && text.IsValid)
        {
          text.Despawn();
        }

        _textPositions.Remove(idx);
      }
      kvp.Value.Clear();
    }

    _beamEntityIndices.Clear();

    _textEntityIndicesByViewer.Clear();
    _textPositions.Clear();
    _activeViewers.Clear();
    UnregisterTickHandlerIfNotNeeded();
  }

  private void EnsureViewerInitialized(IPlayer viewer)
  {
    if (!_textEntityIndicesByViewer.ContainsKey(viewer.Slot))
    {
      _textEntityIndicesByViewer[viewer.Slot] = new List<uint>();
    }

    _activeViewers.Add(viewer.Slot);
    EnsureTickHandlerRegistered();
  }

  private void CreateLabelForViewer(IPlayer viewer, Spawn spawn, List<IPlayer> allViewers)
  {
    try
    {
      var text = _core.EntitySystem.CreateEntityByDesignerName<CPointWorldText>("point_worldtext");
      if (text is null)
      {
        return;
      }

      text.DispatchSpawn();

      text.MessageText = BuildLabel(spawn);
      text.Enabled = true;
      text.Color = new Color(255, 255, 255, 255);
      text.FontSize = 48f;
      text.Fullbright = true;
      text.WorldUnitsPerPx = 0.1f;
      text.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
      text.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;

      var pos = new Vector(spawn.Position.X, spawn.Position.Y, spawn.Position.Z + 50f);

      var viewerPos = viewer.PlayerPawn?.AbsOrigin;
      var yaw = spawn.Angle.Yaw;
      if (viewerPos is not null)
      {
        var dx = viewerPos.Value.X - pos.X;
        var dy = viewerPos.Value.Y - pos.Y;
        yaw = MathF.Atan2(dy, dx) * (180f / MathF.PI) + 90f;
      }

      var angles = new QAngle(0f, yaw, 90f);
      text.Teleport(pos, angles, Vector.Zero);

      if (_textEntityIndicesByViewer.TryGetValue(viewer.Slot, out var list))
      {
        list.Add(text.Index);
      }

      _textPositions[text.Index] = pos;

      foreach (var other in allViewers)
      {
        if (other.Slot == viewer.Slot) continue;
        other.ShouldBlockTransmitEntity((int)text.Index, true);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Retakes: Failed to create spawn label for spawn {Id}", spawn.Id);
    }
  }

  private static string BuildLabel(Spawn spawn)
  {
    var teamLabel = spawn.Team == Team.CT ? "CT" : "T";
    var nameLabel = string.IsNullOrWhiteSpace(spawn.Name) ? "" : $" \"{spawn.Name}\"";
    return $"[{teamLabel}] {spawn.Bombsite} | ID {spawn.Id}{nameLabel}";
  }

  private void CreateBeam(Spawn spawn)
  {
    var start = spawn.Position;

    var color = spawn.Team == Team.CT
      ? new Color(0, 128, 255, 255)
      : (spawn.CanBePlanter ? new Color(255, 0, 0, 255) : new Color(255, 140, 0, 255));

    try
    {
      var beam = _core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
      if (beam is null)
      {
        return;
      }

      beam.StartFrame = 0;
      beam.FrameRate = 0;
      beam.LifeState = 1;
      beam.Width = 5.0f;
      beam.EndWidth = 5.0f;
      beam.Amplitude = 0;
      beam.Speed = 50;
      beam.BeamFlags = 0;
      beam.BeamType = BeamType_t.BEAM_HOSE;
      beam.FadeLength = 10.0f;
      beam.Render = color;
      beam.TurnedOff = false;

      beam.EndPos.X = start.X;
      beam.EndPos.Y = start.Y;
      beam.EndPos.Z = start.Z + 100.0f;

      beam.Teleport(start, new QAngle(0, 0, 0), Vector.Zero);
      beam.DispatchSpawn();

      beam.LifeStateUpdated();
      beam.StartFrameUpdated();
      beam.FrameRateUpdated();
      beam.WidthUpdated();
      beam.EndWidthUpdated();
      beam.AmplitudeUpdated();
      beam.SpeedUpdated();
      beam.BeamFlagsUpdated();
      beam.BeamTypeUpdated();
      beam.FadeLengthUpdated();
      beam.TurnedOffUpdated();
      beam.EndPosUpdated();
      beam.RenderUpdated();

      _beamEntityIndices.Add(beam.Index);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Retakes: Failed to create beam for spawn {Id}", spawn.Id);
    }
  }
}
