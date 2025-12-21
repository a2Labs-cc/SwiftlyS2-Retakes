using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Shared;
using SwiftlyS2_Retakes.Configuration;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;

namespace SwiftlyS2_Retakes.Services;

public sealed class RetakesConfigService : IRetakesConfigService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly string _path;
  private readonly ConVarApplicator _conVarApplicator;
  private readonly RetakesCfgGenerator _cfgGenerator;

  public string ConfigPath => _path;

  private const string ConfigFileName = "config.json";
  private const string SectionName = "retakes";

  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  };

  public RetakesConfig Config { get; private set; } = new();

  public RetakesConfigService(ISwiftlyCore core, ILogger logger)
  {
    _core = core;
    _logger = logger;
    _conVarApplicator = new ConVarApplicator(core);
    _cfgGenerator = new RetakesCfgGenerator(core, logger);

    _path = _core.Configuration.GetConfigPath(ConfigFileName);
    TrySanitizeConfigJsonFile();

    _core.Configuration.InitializeJsonWithModel<RetakesConfig>(ConfigFileName, SectionName);
    TrySanitizeConfigJsonFile();

    _core.Configuration.Configure(builder =>
    {
      builder.AddJsonFile(_path, optional: false, reloadOnChange: false);
    });
  }

  private void TrySanitizeConfigJsonFile()
  {
    try
    {
      if (!File.Exists(_path)) return;

      var text = File.ReadAllText(_path);
      if (string.IsNullOrWhiteSpace(text)) return;

      var node = JsonNode.Parse(text);
      if (node is not JsonObject rootObj) return;

      var changed = ConfigSanitizer.SanitizeAll(rootObj, SectionName);
      if (!changed) return;

      var updated = rootObj.ToJsonString(JsonOptions);
      File.WriteAllText(_path, updated);
      _logger.LogWarning("Retakes: sanitized config.json to remove ':' keys (prevents duplicate key load errors)");
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Retakes: failed to sanitize config.json before loading");
    }
  }

  public void LoadOrCreate()
  {
    try
    {
      _logger.LogPluginDebug("Retakes: Swiftly config base path: {Base}", _core.Configuration.BasePath);
      _logger.LogPluginDebug("Retakes: config.json path: {Path}", _path);

      if (!_core.Configuration.BasePathExists)
      {
        _logger.LogPluginWarning("Retakes: Swiftly config base path does not exist yet: {Base}", _core.Configuration.BasePath);
      }

      var section = _core.Configuration.Manager.GetSection(SectionName);
      var cfg = section.Get<RetakesConfig>();
      Config = cfg ?? new RetakesConfig();

      if (cfg is null)
      {
        _logger.LogPluginWarning("Retakes: config section '{Section}' was not found or could not be parsed. Config will use defaults.", SectionName);
      }

      if (!File.Exists(_path))
      {
        _logger.LogPluginWarning("Retakes: config.json was not found after initialization. Expected at {Path}", _path);
      }

      EnsureTeamBalanceConfigPresent();
      EnsureSmokeScenariosConfigPresent();
      ApplyLoggingToggles(Config.Server);
    }
    catch (Exception ex)
    {
      _logger.LogPluginError(ex, "Retakes: failed to load config.json from {Path}", _path);
      Config = new RetakesConfig();
    }
  }

  private void EnsureTeamBalanceConfigPresent()
  {
    try
    {
      if (!File.Exists(_path))
      {
        return;
      }

      var text = File.ReadAllText(_path);
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      var rootNode = JsonNode.Parse(text);
      if (rootNode is not JsonObject rootObj)
      {
        return;
      }

      // Ensure we never keep colon-delimited keys alongside nested objects.
      ConfigSanitizer.SanitizeColonDelimitedKeys(rootObj);
      ConfigSanitizer.SanitizeCaseInsensitiveDuplicateKeys(rootObj);

      if (rootObj[SectionName] is not JsonObject sectionObj)
      {
        sectionObj = new JsonObject();
        rootObj[SectionName] = sectionObj;
      }

      var teamBalanceKey = sectionObj.ContainsKey("TeamBalance") ? "TeamBalance"
        : sectionObj.ContainsKey("teamBalance") ? "teamBalance"
        : "TeamBalance";

      if (sectionObj[teamBalanceKey] is not JsonObject teamBalanceObj)
      {
        teamBalanceObj = new JsonObject();
        sectionObj[teamBalanceKey] = teamBalanceObj;
      }

      string Key(string pascal, string camel) => teamBalanceObj.ContainsKey(pascal) ? pascal : teamBalanceObj.ContainsKey(camel) ? camel : pascal;

      if (teamBalanceObj[Key("Enabled", "enabled")] is null) teamBalanceObj[Key("Enabled", "enabled")] = Config.TeamBalance.Enabled;
      if (teamBalanceObj[Key("TerroristRatio", "terroristRatio")] is null) teamBalanceObj[Key("TerroristRatio", "terroristRatio")] = Config.TeamBalance.TerroristRatio;
      if (teamBalanceObj[Key("ForceEvenWhenPlayersMod10", "forceEvenWhenPlayersMod10")] is null) teamBalanceObj[Key("ForceEvenWhenPlayersMod10", "forceEvenWhenPlayersMod10")] = Config.TeamBalance.ForceEvenWhenPlayersMod10;

      if (teamBalanceObj[Key("ScrambleEnabled", "scrambleEnabled")] is null) teamBalanceObj[Key("ScrambleEnabled", "scrambleEnabled")] = Config.TeamBalance.ScrambleEnabled;
      if (teamBalanceObj[Key("RoundsToScramble", "roundsToScramble")] is null) teamBalanceObj[Key("RoundsToScramble", "roundsToScramble")] = Config.TeamBalance.RoundsToScramble;

      var updated = rootObj.ToJsonString(JsonOptions);
      File.WriteAllText(_path, updated);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: failed to ensure TeamBalance exists in config.json");
    }
  }

  private void EnsureSmokeScenariosConfigPresent()
  {
    try
    {
      if (!File.Exists(_path))
      {
        return;
      }

      var text = File.ReadAllText(_path);
      if (string.IsNullOrWhiteSpace(text))
      {
        return;
      }

      var rootNode = JsonNode.Parse(text);
      if (rootNode is not JsonObject rootObj)
      {
        return;
      }

      ConfigSanitizer.SanitizeColonDelimitedKeys(rootObj);
      ConfigSanitizer.SanitizeCaseInsensitiveDuplicateKeys(rootObj);

      if (rootObj[SectionName] is not JsonObject sectionObj)
      {
        sectionObj = new JsonObject();
        rootObj[SectionName] = sectionObj;
      }

      var smokeScenariosKey = sectionObj.ContainsKey("SmokeScenarios") ? "SmokeScenarios"
        : sectionObj.ContainsKey("smokeScenarios") ? "smokeScenarios"
        : "SmokeScenarios";

      if (sectionObj[smokeScenariosKey] is not JsonObject smokeScenariosObj)
      {
        smokeScenariosObj = new JsonObject();
        sectionObj[smokeScenariosKey] = smokeScenariosObj;
      }

      string Key(string pascal, string camel) => smokeScenariosObj.ContainsKey(pascal) ? pascal : smokeScenariosObj.ContainsKey(camel) ? camel : pascal;

      if (smokeScenariosObj[Key("RandomRoundsEnabled", "randomRoundsEnabled")] is null) smokeScenariosObj[Key("RandomRoundsEnabled", "randomRoundsEnabled")] = Config.SmokeScenarios.RandomRoundsEnabled;
      if (smokeScenariosObj[Key("RandomRoundChance", "randomRoundChance")] is null) smokeScenariosObj[Key("RandomRoundChance", "randomRoundChance")] = Config.SmokeScenarios.RandomRoundChance;

      var updated = rootObj.ToJsonString(JsonOptions);
      File.WriteAllText(_path, updated);
    }
    catch (Exception ex)
    {
      _logger.LogPluginWarning(ex, "Retakes: failed to ensure SmokeScenarios exists in config.json");
    }
  }

  public void Save()
  {
    try
    {
      var dir = Path.GetDirectoryName(_path);
      if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
      {
        Directory.CreateDirectory(dir);
      }

      var wrapped = new System.Collections.Generic.Dictionary<string, object?>
      {
        [SectionName] = Config
      };

      var json = JsonSerializer.Serialize(wrapped, JsonOptions);
      File.WriteAllText(_path, json);
      _logger.LogPluginInformation("Retakes: config.json saved to {Path}", _path);
    }
    catch (Exception ex)
    {
      _logger.LogPluginError(ex, "Retakes: failed to save config.json to {Path}", _path);
    }
  }

  public void ApplyToConvars(bool restartGame = false)
  {
    _conVarApplicator.ApplyConfig(Config);
    _cfgGenerator.Apply(Config, restartGame);
    ApplyLoggingToggles(Config.Server);
  }

  private static void ApplyLoggingToggles(ServerConfig server)
  {
    LoggingToggle.DebugEnabled = server.DebugEnabled;
  }
}
