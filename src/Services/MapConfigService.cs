using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;
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
  private List<SmokeScenario> _smokeScenarios = new();

  public string? LoadedMapName { get; private set; }
  public IReadOnlyList<Spawn> Spawns => _spawns;
  public IReadOnlyList<SmokeScenario> SmokeScenarios => _smokeScenarios;

  public MapConfigService(ISwiftlyCore core)
  {
    _core = core;
  }

  public void Reset()
  {
    LoadedMapName = null;
    _spawns = new();
    _smokeScenarios = new();
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
        _core.Logger.LogPluginWarning("Retakes: Map config not found: {Path}", mapPath);
        return false;
      }

      var json = File.ReadAllText(mapPath);
      var config = JsonSerializer.Deserialize<MapConfig>(json, _jsonOptions);

      if (config is null)
      {
        _core.Logger.LogPluginWarning("Retakes: Map config could not be parsed: {Path}", mapPath);
        return false;
      }

      LoadedMapName = mapName;
      _spawns = config.Spawns;
      _smokeScenarios = config.SmokeScenarios ?? new();

      EnsureSmokeScenarioIds();

      _core.Logger.LogPluginInformation("Retakes: Loaded {Count} spawns and {SmokeCount} smoke scenarios for map {Map}", Spawns.Count, _smokeScenarios.Count, mapName);
      return true;
    }
    catch (Exception ex)
    {
      _core.Logger.LogPluginError(ex, "Retakes: Failed to load map config for {Map} from {Path}", mapName, mapPath);
      return false;
    }
  }

  public bool Save()
  {
    if (string.IsNullOrWhiteSpace(LoadedMapName))
    {
      _core.Logger.LogPluginWarning("Retakes: Cannot save - no map loaded");
      return false;
    }

    var mapPath = Path.Combine(_core.PluginPath, "resources", "maps", $"{LoadedMapName}.json");

    try
    {
      var config = new MapConfig { Spawns = _spawns, SmokeScenarios = _smokeScenarios };
      var json = JsonSerializer.Serialize(config, _jsonOptions);
      File.WriteAllText(mapPath, json);

      _core.Logger.LogPluginInformation("Retakes: Saved {Count} spawns for map {Map}", _spawns.Count, LoadedMapName);
      return true;
    }
    catch (Exception ex)
    {
      _core.Logger.LogPluginError(ex, "Retakes: Failed to save map config for {Map} to {Path}", LoadedMapName, mapPath);
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

  public int AddSmokeScenario(Vector position, Bombsite bombsite, string? name = null)
  {
    EnsureSmokeScenarioIds();
    var maxId = _smokeScenarios.Count > 0 ? _smokeScenarios.Max(s => s.Id) : 0;
    var newId = maxId + 1;

    var smoke = new SmokeScenario
    {
      Id = newId,
      Vector = $"{position.X} {position.Y} {position.Z}",
      Bombsite = bombsite,
      Name = string.IsNullOrWhiteSpace(name) ? null : name
    };

    _smokeScenarios.Add(smoke);
    return newId;
  }

  public bool RemoveSmokeScenario(int smokeId)
  {
    var smoke = _smokeScenarios.FirstOrDefault(s => s.Id == smokeId);
    if (smoke is null) return false;

    _smokeScenarios.Remove(smoke);
    return true;
  }

  private void EnsureSmokeScenarioIds()
  {
    var maxId = 0;
    for (var i = 0; i < _smokeScenarios.Count; i++)
    {
      if (_smokeScenarios[i].Id > maxId) maxId = _smokeScenarios[i].Id;
    }

    for (var i = 0; i < _smokeScenarios.Count; i++)
    {
      if (_smokeScenarios[i].Id > 0) continue;
      maxId++;
      _smokeScenarios[i].Id = maxId;
    }
  }
}
