using System.Reflection;
using SwiftlyS2.Shared.Players;

namespace SwiftlyS2_Retakes.Utils;

public static class PlayerUtil
{
  private const ulong MinPlausibleSteamId64 = 76561197960265728UL;

  public static bool IsBot(IPlayer player)
  {
    if (player is null || !player.IsValid) return false;

    if (TryGetBool(player, "IsBot", "IsFakeClient", "FakeClient", "IsFake")) return true;

    var controller = player.Controller;
    if (controller is not null)
    {
      if (TryGetBool(controller, "IsBot", "IsFakeClient", "FakeClient", "IsFake")) return true;
    }

    var steamId = player.SteamID;
    if (steamId == 0) return true;

    if (steamId < MinPlausibleSteamId64) return true;

    return false;
  }

  public static bool IsHuman(IPlayer player)
  {
    if (player is null || !player.IsValid) return false;
    return !IsBot(player);
  }

  private static bool TryGetBool(object obj, params string[] memberNames)
  {
    try
    {
      var type = obj.GetType();

      foreach (var name in memberNames)
      {
        var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop is not null)
        {
          var value = prop.GetValue(obj);
          if (IsTruthy(value)) return true;
        }

        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null);
        if (method is not null)
        {
          var value = method.Invoke(obj, null);
          if (IsTruthy(value)) return true;
        }
      }
    }
    catch
    {
      return false;
    }

    return false;
  }

  private static bool IsTruthy(object? value)
  {
    if (value is null) return false;
    if (value is bool b) return b;

    try
    {
      // Many engines expose fake-client flags as int/byte/etc.
      if (value is byte by) return by != 0;
      if (value is sbyte sb) return sb != 0;
      if (value is short s) return s != 0;
      if (value is ushort us) return us != 0;
      if (value is int i) return i != 0;
      if (value is uint ui) return ui != 0;
      if (value is long l) return l != 0;
      if (value is ulong ul) return ul != 0;
    }
    catch
    {
      return false;
    }

    return false;
  }
}
