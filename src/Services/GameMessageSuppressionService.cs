using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;

namespace SwiftlyS2_Retakes.Services;

public class GameMessageSuppressionService : IGameMessageSuppressionService
{
  private readonly ISwiftlyCore _core;
  private IConVar<bool>? _enabled;
  private IConVar<bool>? _debug;
  private Guid? _textMsgHookId;
  private Guid? _sayText2HookId;
  private Guid? _radioTextHookId;
  private Guid? _hintTextHookId;
  private Guid? _hudTextHookId;
  private Guid? _hudMsgHookId;

  public GameMessageSuppressionService(ISwiftlyCore core)
  {
    _core = core;
  }

  public void Register()
  {
    _enabled ??= _core.ConVar.CreateOrFind(
      "retakes_suppress_game_messages_enabled",
      "Enable game message suppression",
      false);

    _debug ??= _core.ConVar.CreateOrFind(
      "retakes_suppress_game_messages_debug",
      "Debug log suppressed game messages",
      false);

    if (_enabled is null || !_enabled.Value)
    {
      return;
    }

    // These are server -> client usermessages, so we must hook server messages.
    _textMsgHookId = _core.NetMessage.HookServerMessage<CUserMessageTextMsg>(OnServerTextMessage);
    _sayText2HookId = _core.NetMessage.HookServerMessage<CUserMessageSayText2>(OnServerSayText2);

    // CS2 commonly uses CCSUsrMsg_* variants for chat/HUD notices.
    _radioTextHookId = _core.NetMessage.HookServerMessage<CCSUsrMsg_RadioText>(OnServerRadioText);
    _hintTextHookId = _core.NetMessage.HookServerMessage<CCSUsrMsg_HintText>(OnServerHintText);
    _hudTextHookId = _core.NetMessage.HookServerMessage<CCSUsrMsg_HudText>(OnServerHudText);
    _hudMsgHookId = _core.NetMessage.HookServerMessage<CCSUsrMsg_HudMsg>(OnServerHudMsg);
  }

  public void Unregister()
  {
    if (_textMsgHookId.HasValue)
    {
      _core.NetMessage.Unhook(_textMsgHookId.Value);
      _textMsgHookId = null;
    }

    if (_sayText2HookId.HasValue)
    {
      _core.NetMessage.Unhook(_sayText2HookId.Value);
      _sayText2HookId = null;
    }

    if (_radioTextHookId.HasValue)
    {
      _core.NetMessage.Unhook(_radioTextHookId.Value);
      _radioTextHookId = null;
    }

    if (_hintTextHookId.HasValue)
    {
      _core.NetMessage.Unhook(_hintTextHookId.Value);
      _hintTextHookId = null;
    }

    if (_hudTextHookId.HasValue)
    {
      _core.NetMessage.Unhook(_hudTextHookId.Value);
      _hudTextHookId = null;
    }

    if (_hudMsgHookId.HasValue)
    {
      _core.NetMessage.Unhook(_hudMsgHookId.Value);
      _hudMsgHookId = null;
    }
  }

  private static bool ShouldSuppressToken(string token)
  {
    if (string.IsNullOrEmpty(token)) return false;

    var normalized = token.Trim();
    var t = normalized.StartsWith('#') ? normalized[1..] : normalized;

    return
      // Award-related message keys (covers the full IgnoreMessages-style lists)
      t.StartsWith("Player_Cash_Award_", StringComparison.OrdinalIgnoreCase) ||
      t.StartsWith("Player_Point_Award_", StringComparison.OrdinalIgnoreCase) ||
      t.StartsWith("Player_Team_Award_", StringComparison.OrdinalIgnoreCase) ||
      t.StartsWith("Team_Cash_Award_", StringComparison.OrdinalIgnoreCase) ||

      // Existing broader matches for other commonly-spammed notices
      t.Contains("Cash_Award", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("Bomb_Defused", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("Bomb_Planted", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("CTs_Win", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("Ts_Win", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("CounterTerrorists_Win", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("Terrorists_Win", StringComparison.OrdinalIgnoreCase);
  }

  private static bool ShouldSuppressPlainText(string text)
  {
    if (string.IsNullOrEmpty(text)) return false;

    // Catch localized money messages that are sent as plain text (no #Token)
    // Example: "+$1900 team income for losing"
    var t = text.Trim();

    var hasMoney = t.Contains("+$", StringComparison.OrdinalIgnoreCase) || t.Contains("$", StringComparison.OrdinalIgnoreCase);
    if (!hasMoney) return false;

    // Variants observed across different game states (e.g. first round):
    // - "+$1900 team income for losing"
    // - "+$1900 income for losing"
    // - "... loser bonus ..."
    var mentionsIncome =
      t.Contains("income", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("loser bonus", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("losers bonus", StringComparison.OrdinalIgnoreCase);

    var mentionsLosing =
      t.Contains("for losing", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("losing", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("lose", StringComparison.OrdinalIgnoreCase) ||
      t.Contains("loser", StringComparison.OrdinalIgnoreCase);

    return mentionsIncome && mentionsLosing;
  }

  private HookResult OnServerTextMessage(CUserMessageTextMsg msg)
  {
    if (msg.Param.Count == 0)
    {
      return HookResult.Continue;
    }

    // TextMsg can carry the token in any Param index depending on message type.
    // Example tokens include #Player_Cash_Award_* / #Team_Cash_Award_* and various round-end notices.
    foreach (var param in msg.Param)
    {
      if (!ShouldSuppressToken(param) && !ShouldSuppressPlainText(param))
      {
        continue;
      }

      // Suppress safely by clearing recipients (send to nobody) and allowing the original send to continue.
      // This avoids cancelling the underlying native send function, which can be crash-prone for some messages.
      msg.Recipients.RemoveAllPlayers();

      if (_debug?.Value == true)
      {
        _core.Logger.LogPluginInformation($"[DEBUG] SUPPRESSING TextMsg (recipient mask cleared). Matched='{param}'");
      }
      return HookResult.Continue;
    }

    return HookResult.Continue;
  }

  private HookResult OnServerSayText2(CUserMessageSayText2 msg)
  {
    // SayText2 is used for many chat-like system messages.
    // It can be either a token (e.g. #Cstrike_* ) or already-resolved text depending on sender.
    var tokens = new[]
    {
      msg.Messagename,
      msg.Param1,
      msg.Param2,
      msg.Param3,
      msg.Param4,
    };

    foreach (var t in tokens)
    {
      if (string.IsNullOrEmpty(t)) continue;

      // Token-based suppression
      if (ShouldSuppressToken(t))
      {
        msg.Recipients.RemoveAllPlayers();

        if (_debug?.Value == true)
        {
          _core.Logger.LogPluginInformation(
            $"[DEBUG] SUPPRESSING SayText2 (recipient mask cleared). Messagename='{msg.Messagename}'");
        }
        return HookResult.Continue;
      }

      // Fallback: plain-text suppression (covers cases where server already localized the message).
      if (t.Contains("defused", StringComparison.OrdinalIgnoreCase) ||
          t.Contains("team has won", StringComparison.OrdinalIgnoreCase) ||
          t.Contains("for defusing", StringComparison.OrdinalIgnoreCase) ||
          ShouldSuppressPlainText(t))
      {
        msg.Recipients.RemoveAllPlayers();

        if (_debug?.Value == true)
        {
          _core.Logger.LogPluginInformation(
            $"[DEBUG] SUPPRESSING SayText2 (plain-text match, recipient mask cleared). Messagename='{msg.Messagename}'");
        }
        return HookResult.Continue;
      }
    }

    return HookResult.Continue;
  }

  private HookResult OnServerRadioText(CCSUsrMsg_RadioText msg)
  {
    // RadioText carries a message key + params, often used for system notices.
    if (ShouldSuppressToken(msg.MsgName) || ShouldSuppressPlainText(msg.MsgName))
    {
      msg.Recipients.RemoveAllPlayers();
      if (_debug?.Value == true)
      {
        _core.Logger.LogPluginInformation($"[DEBUG] SUPPRESSING RadioText (recipient mask cleared). MsgName='{msg.MsgName}'");
      }
      return HookResult.Continue;
    }

    foreach (var p in msg.Params)
    {
      if (!ShouldSuppressToken(p) && !ShouldSuppressPlainText(p))
      {
        continue;
      }

      msg.Recipients.RemoveAllPlayers();
      if (_debug?.Value == true)
      {
        _core.Logger.LogPluginInformation($"[DEBUG] SUPPRESSING RadioText (recipient mask cleared). Matched='{p}' MsgName='{msg.MsgName}'");
      }
      return HookResult.Continue;
    }

    return HookResult.Continue;
  }

  private HookResult OnServerHintText(CCSUsrMsg_HintText msg)
  {
    if (!ShouldSuppressToken(msg.Message) && !ShouldSuppressPlainText(msg.Message))
    {
      return HookResult.Continue;
    }

    msg.Recipients.RemoveAllPlayers();
    if (_debug?.Value == true)
    {
      _core.Logger.LogPluginInformation($"[DEBUG] SUPPRESSING HintText (recipient mask cleared). Message='{msg.Message}'");
    }
    return HookResult.Continue;
  }

  private HookResult OnServerHudText(CCSUsrMsg_HudText msg)
  {
    if (!ShouldSuppressToken(msg.Text) && !ShouldSuppressPlainText(msg.Text))
    {
      return HookResult.Continue;
    }

    msg.Recipients.RemoveAllPlayers();
    if (_debug?.Value == true)
    {
      _core.Logger.LogPluginInformation($"[DEBUG] SUPPRESSING HudText (recipient mask cleared). Text='{msg.Text}'");
    }
    return HookResult.Continue;
  }

  private HookResult OnServerHudMsg(CCSUsrMsg_HudMsg msg)
  {
    if (!ShouldSuppressToken(msg.Text) && !ShouldSuppressPlainText(msg.Text))
    {
      return HookResult.Continue;
    }

    msg.Recipients.RemoveAllPlayers();
    if (_debug?.Value == true)
    {
      _core.Logger.LogPluginInformation($"[DEBUG] SUPPRESSING HudMsg (recipient mask cleared). Text='{msg.Text}'");
    }
    return HookResult.Continue;
  }
}
