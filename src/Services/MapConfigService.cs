using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

public sealed class MapConfigService : IMapConfigService
{
  private readonly ISwiftlyCore _core;
  private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
  {
    WriteIndented = true
  };

  private List<Spawn> _spawns = new();

  public string? LoadedMapName { get; private set; }
  public IReadOnlyList<Spawn> Spawns => _spawns;

  public MapConfigService(ISwiftlyCore core)
  {
    _core = core;
  }

  public void Reset()
  {
    LoadedMapName = null;
    _spawns = new();
  }

  public bool Load(string mapName)
  {
    Reset();

    if (string.IsNullOrWhiteSpace(mapName))
    {
      return false;
    }

    var mapPath = Path.Combine(_core.PluginPath, "resources", "maps", $"{mapName}.json");

    try
    {
      if (!File.Exists(mapPath))
      {
        _core.Logger.LogWarning("Retakes: Map config not found: {Path}", mapPath);
        return false;
      }

      var json = File.ReadAllText(mapPath);
      var config = JsonSerializer.Deserialize<MapConfig>(json, _jsonOptions);

      if (config is null)
      {
        _core.Logger.LogWarning("Retakes: Map config could not be parsed: {Path}", mapPath);
        return false;
      }

      LoadedMapName = mapName;
      _spawns = config.Spawns;

      _core.Logger.LogInformation("Retakes: Loaded {Count} spawns for map {Map}", Spawns.Count, mapName);
      return true;
    }
    catch (Exception ex)
    {
      _core.Logger.LogError(ex, "Retakes: Failed to load map config for {Map} from {Path}", mapName, mapPath);
      return false;
    }
  }

  public bool Save()
  {
    if (string.IsNullOrWhiteSpace(LoadedMapName))
    {
      _core.Logger.LogWarning("Retakes: Cannot save - no map loaded");
      return false;
    }

    var mapPath = Path.Combine(_core.PluginPath, "resources", "maps", $"{LoadedMapName}.json");

    try
    {
      var config = new MapConfig { Spawns = _spawns };
      var json = JsonSerializer.Serialize(config, _jsonOptions);
      File.WriteAllText(mapPath, json);

      _core.Logger.LogInformation("Retakes: Saved {Count} spawns for map {Map}", _spawns.Count, LoadedMapName);
      return true;
    }
    catch (Exception ex)
    {
      _core.Logger.LogError(ex, "Retakes: Failed to save map config for {Map} to {Path}", LoadedMapName, mapPath);
      return false;
    }
  }

  public Spawn? GetSpawnById(int id)
  {
    return _spawns.FirstOrDefault(s => s.Id == id);
  }

  public int AddSpawn(Vector position, QAngle angle, Team team, Bombsite bombsite, bool canBePlanter)
  {
    var maxId = _spawns.Count > 0 ? _spawns.Max(s => s.Id) : 0;
    var newId = maxId + 1;

    var spawn = new Spawn
    {
      Id = newId,
      Vector = $"{position.X} {position.Y} {position.Z}",
      QAngle = $"{angle.Pitch} {angle.Yaw} {angle.Roll}",
      Team = team,
      Bombsite = bombsite,
      CanBePlanter = canBePlanter
    };

    _spawns.Add(spawn);
    return newId;
  }

  public bool RemoveSpawn(int id)
  {
    var spawn = _spawns.FirstOrDefault(s => s.Id == id);
    if (spawn is null) return false;

    _spawns.Remove(spawn);
    return true;
  }

  public bool SetSpawnName(int id, string? name)
  {
    var spawn = _spawns.FirstOrDefault(s => s.Id == id);
    if (spawn is null) return false;

    spawn.Name = string.IsNullOrWhiteSpace(name) ? null : name;
    return true;
  }
}
