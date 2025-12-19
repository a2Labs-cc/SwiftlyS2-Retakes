using System.Data;

namespace SwiftlyS2_Retakes.Interfaces;

/// <summary>
/// Service for database operations.
/// </summary>
public interface IDatabaseService
{
  /// <summary>
  /// Gets a database connection.
  /// </summary>
  /// <param name="connectionName">Optional connection name (defaults to configured name)</param>
  /// <returns>An open database connection</returns>
  IDbConnection GetConnection(string? connectionName = null);

  /// <summary>
  /// Initializes the database schema (creates tables if needed).
  /// </summary>
  void InitializeSchema();

  /// <summary>
  /// Executes a non-query SQL command.
  /// </summary>
  /// <param name="sql">The SQL to execute</param>
  /// <param name="param">Optional parameters</param>
  /// <returns>Number of affected rows</returns>
  int Execute(string sql, object? param = null);

  /// <summary>
  /// Queries for a single row.
  /// </summary>
  /// <typeparam name="T">The type to map to</typeparam>
  /// <param name="sql">The SQL query</param>
  /// <param name="param">Optional parameters</param>
  /// <returns>The mapped object or null</returns>
  T? QuerySingleOrDefault<T>(string sql, object? param = null) where T : class;

  /// <summary>
  /// Queries for multiple rows.
  /// </summary>
  /// <typeparam name="T">The type to map to</typeparam>
  /// <param name="sql">The SQL query</param>
  /// <param name="param">Optional parameters</param>
  /// <returns>Enumerable of mapped objects</returns>
  IEnumerable<T> Query<T>(string sql, object? param = null);
}
