using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Models;

namespace SwiftlyS2_Retakes.Services;

/// <summary>
/// Service for database operations.
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
  private readonly ISwiftlyCore _core;
  private readonly ILogger _logger;
  private readonly IRetakesConfigService _config;

  private const string UserSettingsTable = "retakes_user_settings";

  public DatabaseService(ISwiftlyCore core, ILogger logger, IRetakesConfigService config)
  {
    _core = core;
    _logger = logger;
    _config = config;

    // Configure Dapper to match snake_case column names
    DefaultTypeMap.MatchNamesWithUnderscores = true;
  }

  public IDbConnection GetConnection(string? connectionName = null)
  {
    var name = connectionName ?? _config.Config.Preferences.DatabaseConnectionName;
    if (string.IsNullOrWhiteSpace(name)) name = "default";
    return _core.Database.GetConnection(name);
  }

  public void InitializeSchema()
  {
    try
    {
      using var connection = GetConnection();
      connection.Open();

      // Create user settings table
      connection.Execute($@"
CREATE TABLE IF NOT EXISTS {UserSettingsTable} (
  steam_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
  updated_at BIGINT NOT NULL,
  wants_awp TINYINT NOT NULL DEFAULT 0,
  wants_ssg08 TINYINT NOT NULL DEFAULT 0,
  wants_awp_priority TINYINT NOT NULL DEFAULT 0,
  wants_ct_spawn_menu TINYINT NOT NULL DEFAULT 1,
  t_spawn_a INT NULL,
  t_spawn_b INT NULL,
  ct_spawn_a INT NULL,
  ct_spawn_b INT NULL,
  t_pistol_primary VARCHAR(64) NULL,
  t_half_primary VARCHAR(64) NULL,
  t_half_secondary VARCHAR(64) NULL,
  t_full_primary VARCHAR(64) NULL,
  t_full_secondary VARCHAR(64) NULL,
  ct_pistol_primary VARCHAR(64) NULL,
  ct_half_primary VARCHAR(64) NULL,
  ct_half_secondary VARCHAR(64) NULL,
  ct_full_primary VARCHAR(64) NULL,
  ct_full_secondary VARCHAR(64) NULL
);");

      // Add columns that may not exist in older schemas
      TryAddColumn(connection, UserSettingsTable, "wants_ssg08 TINYINT NOT NULL DEFAULT 0");
      TryAddColumn(connection, UserSettingsTable, "wants_awp_priority TINYINT NOT NULL DEFAULT 0");
      TryAddColumn(connection, UserSettingsTable, "wants_ct_spawn_menu TINYINT NOT NULL DEFAULT 1");
      TryAddColumn(connection, UserSettingsTable, "t_spawn_a INT NULL");
      TryAddColumn(connection, UserSettingsTable, "t_spawn_b INT NULL");
      TryAddColumn(connection, UserSettingsTable, "ct_spawn_a INT NULL");
      TryAddColumn(connection, UserSettingsTable, "ct_spawn_b INT NULL");

      _logger.LogInformation("Retakes: database schema initialized");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Retakes: failed to initialize database schema");
    }
  }

  public int Execute(string sql, object? param = null)
  {
    using var connection = GetConnection();
    connection.Open();
    return connection.Execute(sql, param);
  }

  public T? QuerySingleOrDefault<T>(string sql, object? param = null) where T : class
  {
    using var connection = GetConnection();
    connection.Open();
    return connection.QuerySingleOrDefault<T>(sql, param);
  }

  public IEnumerable<T> Query<T>(string sql, object? param = null)
  {
    using var connection = GetConnection();
    connection.Open();
    return connection.Query<T>(sql, param).ToList();
  }

  private void TryAddColumn(IDbConnection connection, string tableName, string columnDef)
  {
    try
    {
      connection.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnDef}");
    }
    catch
    {
      // Column likely already exists - ignore
    }
  }
}
