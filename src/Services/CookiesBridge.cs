using System.Runtime.CompilerServices;
using Cookies.Contract;
using SwiftlyS2.Shared;

namespace SwiftlyS2_Retakes.Services;

/// <summary>
/// Bridge to still allow Retakes to load if Cookies fails to load or is missing.
/// </summary>
public sealed class CookiesBridge
{
  private const string PlayerV1Key = "Cookies.Player.v1";

  private readonly IPlayerCookiesAPIv1 _api;

  private CookiesBridge(IPlayerCookiesAPIv1 api)
  {
    _api = api;
  }

  [MethodImpl(MethodImplOptions.NoInlining)]
  public static CookiesBridge? TryCreate(IInterfaceManager interfaceManager) =>
    interfaceManager.HasSharedInterface(PlayerV1Key)
      ? new CookiesBridge(interfaceManager.GetSharedInterface<IPlayerCookiesAPIv1>(PlayerV1Key))
      : null;

  [MethodImpl(MethodImplOptions.NoInlining)]
  public T? Get<T>(long steamId, string key) => _api.Get<T>(steamId, key);

  [MethodImpl(MethodImplOptions.NoInlining)]
  public T? GetOrDefault<T>(long steamId, string key, T defaultValue) => _api.GetOrDefault(steamId, key, defaultValue);

  [MethodImpl(MethodImplOptions.NoInlining)]
  public bool Has(long steamId, string key) => _api.Has(steamId, key);

  [MethodImpl(MethodImplOptions.NoInlining)]
  public void Set<T>(long steamId, string key, T value) => _api.Set(steamId, key, value);

  [MethodImpl(MethodImplOptions.NoInlining)]
  public void Unset(long steamId, string key) => _api.Unset(steamId, key);
}
