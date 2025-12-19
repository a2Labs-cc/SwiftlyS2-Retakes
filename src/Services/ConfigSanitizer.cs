using System.Text.Json.Nodes;

namespace SwiftlyS2_Retakes.Services;

/// <summary>
/// Utility class for sanitizing JSON configuration files.
/// Handles colon-delimited keys, case-insensitive duplicates, and section normalization.
/// </summary>
public static class ConfigSanitizer
{
  /// <summary>
  /// Normalizes the section key to the expected casing.
  /// </summary>
  public static bool NormalizeSectionKey(JsonObject rootObj, string sectionName)
  {
    var existingExact = rootObj.ContainsKey(sectionName);
    var otherKey = rootObj.Select(kvp => kvp.Key)
      .FirstOrDefault(k => string.Equals(k, sectionName, StringComparison.OrdinalIgnoreCase) && !string.Equals(k, sectionName, StringComparison.Ordinal));

    if (otherKey is null)
    {
      return false;
    }

    if (!rootObj.TryGetPropertyValue(otherKey, out var otherNode) || otherNode is null)
    {
      rootObj.Remove(otherKey);
      return true;
    }

    if (!existingExact)
    {
      rootObj[sectionName] = otherNode;
      rootObj.Remove(otherKey);
      return true;
    }

    if (rootObj[sectionName] is JsonObject winnerObj && otherNode is JsonObject loserObj)
    {
      foreach (var kvp in loserObj.ToList())
      {
        if (!winnerObj.TryGetPropertyValue(kvp.Key, out var existing) || existing is null)
        {
          winnerObj[kvp.Key] = kvp.Value?.DeepClone();
        }
      }
    }

    rootObj.Remove(otherKey);
    return true;
  }

  /// <summary>
  /// Converts colon-delimited keys (e.g., "section:key") into nested JSON objects.
  /// </summary>
  public static bool SanitizeColonDelimitedKeys(JsonObject obj)
  {
    var changed = false;

    var colonKeys = obj.Select(kvp => kvp.Key)
      .Where(k => k.Contains(':', StringComparison.Ordinal))
      .ToList();

    foreach (var key in colonKeys)
    {
      if (!obj.TryGetPropertyValue(key, out var valueNode) || valueNode is null) continue;

      var parts = key.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      if (parts.Length < 2) continue;

      JsonObject cursor = obj;
      for (var i = 0; i < parts.Length - 1; i++)
      {
        var part = parts[i];
        if (cursor[part] is not JsonObject next)
        {
          next = new JsonObject();
          cursor[part] = next;
          changed = true;
        }
        cursor = next;
      }

      var leaf = parts[^1];
      if (cursor[leaf] is null)
      {
        cursor[leaf] = valueNode.DeepClone();
        changed = true;
      }

      obj.Remove(key);
      changed = true;
    }

    foreach (var kvp in obj.ToList())
    {
      if (kvp.Value is JsonObject child)
      {
        if (SanitizeColonDelimitedKeys(child)) changed = true;
      }
    }

    return changed;
  }

  /// <summary>
  /// Merges case-insensitive duplicate keys, keeping the first occurrence.
  /// </summary>
  public static bool SanitizeCaseInsensitiveDuplicateKeys(JsonObject obj)
  {
    var changed = false;

    static bool IsAllLower(string s)
    {
      foreach (var ch in s)
      {
        if (char.IsLetter(ch) && char.IsUpper(ch)) return false;
      }
      return true;
    }

    void MergeInto(JsonNode? winner, JsonNode? loser)
    {
      if (winner is JsonObject winnerObj && loser is JsonObject loserObj)
      {
        foreach (var kvp in loserObj.ToList())
        {
          if (!winnerObj.TryGetPropertyValue(kvp.Key, out var existing) || existing is null)
          {
            winnerObj[kvp.Key] = kvp.Value?.DeepClone();
            changed = true;
            continue;
          }

          MergeInto(existing, kvp.Value);
        }

        return;
      }
    }

    var keys = obj.Select(kvp => kvp.Key).ToList();
    var groups = keys.GroupBy(k => k.ToLowerInvariant()).Where(g => g.Count() > 1).ToList();

    foreach (var group in groups)
    {
      var groupKeys = group.ToList();
      var winnerKey = groupKeys
        .OrderBy(k => IsAllLower(k) ? 1 : 0)
        .ThenBy(k => k, StringComparer.Ordinal)
        .First();

      if (!obj.TryGetPropertyValue(winnerKey, out var winnerNode)) continue;

      foreach (var loserKey in groupKeys.Where(k => !string.Equals(k, winnerKey, StringComparison.Ordinal)).ToList())
      {
        if (!obj.TryGetPropertyValue(loserKey, out var loserNode))
        {
          obj.Remove(loserKey);
          changed = true;
          continue;
        }

        MergeInto(winnerNode, loserNode);
        obj.Remove(loserKey);
        changed = true;
      }
    }

    foreach (var kvp in obj.ToList())
    {
      if (kvp.Value is JsonObject child)
      {
        if (SanitizeCaseInsensitiveDuplicateKeys(child)) changed = true;
      }
    }

    return changed;
  }

  /// <summary>
  /// Runs all sanitization passes on the JSON object.
  /// </summary>
  public static bool SanitizeAll(JsonObject rootObj, string sectionName)
  {
    var changed = SanitizeColonDelimitedKeys(rootObj);
    changed |= SanitizeCaseInsensitiveDuplicateKeys(rootObj);
    changed |= NormalizeSectionKey(rootObj, sectionName);
    return changed;
  }
}
