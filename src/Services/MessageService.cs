using System;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2_Retakes.Interfaces;
using System.Globalization;

namespace SwiftlyS2_Retakes.Services;

public sealed class MessageService : IMessageService
{
  private readonly ISwiftlyCore _core;
  private readonly IRetakesConfigService _config;

  public MessageService(ISwiftlyCore core, IRetakesConfigService config)
  {
    _core = core;
    _config = config;
  }

  public string FormatChat(string message)
  {
    var prefix = GetPrefix();
    var colorTag = GetPrefixColorTag();
    var formattedPrefix = GetFormattedPrefix(prefix, colorTag);

    if (string.IsNullOrWhiteSpace(message)) return formattedPrefix ?? prefix;

    var trimmed = message.TrimStart();

    // Normalize legacy prefixes into the new standardized prefix.
    if (trimmed.StartsWith("Retakes:", StringComparison.OrdinalIgnoreCase))
    {
      trimmed = trimmed["Retakes:".Length..].TrimStart();
    }
    else if (trimmed.StartsWith("[Retake]", StringComparison.OrdinalIgnoreCase))
    {
      trimmed = trimmed["[Retake]".Length..].TrimStart();
    }
    else if (trimmed.StartsWith("[Retakes]", StringComparison.OrdinalIgnoreCase))
    {
      trimmed = trimmed["[Retakes]".Length..].TrimStart();
    }
    else if (trimmed.StartsWith("Retakes |", StringComparison.OrdinalIgnoreCase))
    {
      trimmed = trimmed["Retakes |".Length..].TrimStart();
    }
    else if (!string.IsNullOrEmpty(formattedPrefix) && trimmed.StartsWith(formattedPrefix, StringComparison.OrdinalIgnoreCase))
    {
      // Already formatted.
      return trimmed;
    }
    else if (!string.IsNullOrEmpty(prefix) && trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
      // Already formatted (no color).
      return trimmed;
    }

    if (string.IsNullOrWhiteSpace(trimmed)) return formattedPrefix ?? prefix;
    var appliedPrefix = formattedPrefix ?? prefix;
    var combined = string.IsNullOrEmpty(appliedPrefix) ? trimmed : $"{appliedPrefix} {trimmed}";
    return NeedsColorize(colorTag) ? combined.Colored() : combined;
  }

  public void Chat(IPlayer player, string message)
  {
    if (player is null || !player.IsValid || string.IsNullOrEmpty(message)) return;

    var lines = message.Split('\n');
    foreach (var line in lines)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        player.SendMessage(MessageType.Chat, " ");
        continue;
      }
      player.SendMessage(MessageType.Chat, FormatChat(line));
    }
  }

  public void BroadcastChat(string message)
  {
    if (string.IsNullOrEmpty(message)) return;

    var lines = message.Split('\n');
    foreach (var line in lines)
    {
      if (string.IsNullOrWhiteSpace(line))
      {
        _core.PlayerManager.SendChat(" ");
        continue;
      }
      _core.PlayerManager.SendChat(FormatChat(line));
    }
  }

  private string GetPrefix()
  {
    var configured = _config?.Config?.Server?.ChatPrefix;
    if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd();

    // Fallback to legacy prefix if config missing.
    return "Retakes |";
  }

  private string? GetFormattedPrefix(string prefix, string? colorTag)
  {
    if (string.IsNullOrWhiteSpace(prefix)) return null;

    if (NeedsColorize(colorTag))
    {
      return $"[{colorTag}]{prefix}[white]";
    }

    return prefix;
  }

  private static bool NeedsColorize(string? colorTag) => !string.IsNullOrWhiteSpace(colorTag);

  private string? GetPrefixColorTag()
  {
    var color = _config?.Config?.Server?.ChatPrefixColor;
    if (string.IsNullOrWhiteSpace(color)) return "green"; // default green

    var c = color.Trim().ToLowerInvariant();
    return c switch
    {
      "green" => "green",
      "blue" => "blue",
      "red" => "red",
      "lightgreen" => "lightgreen",
      "default" or "none" => null,
      _ => "green",
    };
  }
}
