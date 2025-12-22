using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Services;

namespace SwiftlyS2_Retakes.DependencyInjection;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
  /// <summary>
  /// Registers all Retakes services into the service collection.
  /// </summary>
  /// <param name="services">The service collection</param>
  /// <returns>The service collection for chaining</returns>
  public static IServiceCollection AddRetakesServices(this IServiceCollection services)
  {
    // Register Random as singleton
    services.AddSingleton<Random>();

    // Register database service
    services.AddSingleton<IDatabaseService, DatabaseService>();

    // Register core services
    services.AddSingleton<IRetakesConfigService, RetakesConfigService>();
    services.AddSingleton<IMapConfigService, MapConfigService>();
    services.AddSingleton<IPawnLifecycleService, PawnLifecycleService>();
    services.AddSingleton<IRetakesStateService, RetakesStateService>();
    services.AddSingleton<IPlayerPreferencesService, PlayerPreferencesService>();

    // Register messaging service
    services.AddSingleton<IMessageService, MessageService>();

    services.AddSingleton<IDamageReportService, DamageReportService>();

    // Register game services
    services.AddSingleton<ISpawnManager, SpawnManager>();
    services.AddSingleton<ISpawnVisualizationService, SpawnVisualizationService>();
    services.AddSingleton<IAllocationService, AllocationService>();
    services.AddSingleton<IAnnouncementService, AnnouncementService>();
    services.AddSingleton<IAutoPlantService, AutoPlantService>();
    services.AddSingleton<IClutchAnnounceService, ClutchAnnounceService>();
    services.AddSingleton<IQueueService, QueueService>();
    services.AddSingleton<IBuyMenuService, BuyMenuService>();

    // Register feature services
    services.AddSingleton<IBreakerService, BreakerService>();
    services.AddSingleton<IInstantBombService, InstantBombService>();
    services.AddSingleton<IAntiTeamFlashService, AntiTeamFlashService>();
    services.AddSingleton<ISmokeScenarioService, SmokeScenarioService>();
    services.AddSingleton<ISoloBotService, SoloBotService>();
    services.AddSingleton<IAfkManagerService, AfkManagerService>();

    return services;
  }
}
