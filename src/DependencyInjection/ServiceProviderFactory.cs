using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace SwiftlyS2_Retakes.DependencyInjection;

/// <summary>
/// Factory for creating and configuring the service provider.
/// </summary>
public static class ServiceProviderFactory
{
  /// <summary>
  /// Creates a configured service provider with all plugin services.
  /// </summary>
  /// <param name="core">The Swiftly core instance</param>
  /// <param name="logger">The logger instance</param>
  /// <returns>Configured service provider</returns>
  public static IServiceProvider CreateServiceProvider(ISwiftlyCore core, ILogger logger)
  {
    var services = new ServiceCollection();

    // Register Swiftly dependencies
    services.AddSingleton(core);
    services.AddSingleton(logger);

    // Register plugin services
    services.AddRetakesServices();

    return services.BuildServiceProvider();
  }

  /// <summary>
  /// Safely disposes a service provider if it implements IDisposable.
  /// </summary>
  /// <param name="serviceProvider">The service provider to dispose</param>
  public static void DisposeServiceProvider(IServiceProvider? serviceProvider)
  {
    if (serviceProvider is IDisposable disposable)
    {
      disposable.Dispose();
    }
  }
}
