using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;

using SwiftlyS2_Retakes.DependencyInjection;
using SwiftlyS2_Retakes.Handlers;
using SwiftlyS2_Retakes.Interfaces;
using SwiftlyS2_Retakes.Logging;

namespace SwiftlyS2_Retakes;

[PluginMetadata(Id = "Retakes", Version = "1.0.0", Name = "Retakes", Author = "aga", Description = "No description.")]

public partial class SwiftlyS2_Retakes : BasePlugin
{
  private IServiceProvider? _serviceProvider;

  // Services resolved from DI container
  private IRetakesConfigService? _config;
  private IMapConfigService? _mapConfig;
  private IPawnLifecycleService? _pawnLifecycle;
  private IRetakesStateService? _state;
  private IPlayerPreferencesService? _prefs;
  private ISpawnManager? _spawnManager;
  private ISpawnVisualizationService? _spawnViz;
  private IAllocationService? _allocation;
  private IAnnouncementService? _announcement;
  private IAutoPlantService? _autoPlant;
  private IClutchAnnounceService? _clutch;
  private IQueueService? _queue;
  private IBuyMenuService? _buyMenu;
  private IBreakerService? _breaker;
  private IInstantBombService? _instantBomb;
  private IAntiTeamFlashService? _antiTeamFlash;
  private IDamageReportService? _damageReport;
  private ISmokeScenarioService? _smokeScenario;
  private IMessageService? _messages;
  private ISoloBotService? _soloBot;
  private IAfkManagerService? _afkManager;
  private IGameMessageSuppressionService? _gameMsgSuppression;

  // Handlers
  private MapEventHandlers? _mapEventHandlers;
  private RoundEventHandlers? _roundEventHandlers;
  private PlayerEventHandlers? _playerEventHandlers;
  private CommandHandlers? _commandHandlers;

  public SwiftlyS2_Retakes(ISwiftlyCore core) : base(core)
  {
    InitializeServices();
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager)
  {
  }

  public override void Load(bool hotReload)
  {
    if (_serviceProvider == null)
    {
      Core.Logger.LogPluginError("Retakes: services not initialized.");
      return;
    }

    // Resolve services from DI container
    _config = _serviceProvider.GetRequiredService<IRetakesConfigService>();
    _mapConfig = _serviceProvider.GetRequiredService<IMapConfigService>();
    _pawnLifecycle = _serviceProvider.GetRequiredService<IPawnLifecycleService>();
    _state = _serviceProvider.GetRequiredService<IRetakesStateService>();
    _prefs = _serviceProvider.GetRequiredService<IPlayerPreferencesService>();
    _spawnManager = _serviceProvider.GetRequiredService<ISpawnManager>();
    _spawnViz = _serviceProvider.GetRequiredService<ISpawnVisualizationService>();
    _allocation = _serviceProvider.GetRequiredService<IAllocationService>();
    _announcement = _serviceProvider.GetRequiredService<IAnnouncementService>();
    _autoPlant = _serviceProvider.GetRequiredService<IAutoPlantService>();
    _clutch = _serviceProvider.GetRequiredService<IClutchAnnounceService>();
    _queue = _serviceProvider.GetRequiredService<IQueueService>();
    _buyMenu = _serviceProvider.GetRequiredService<IBuyMenuService>();
    _breaker = _serviceProvider.GetRequiredService<IBreakerService>();
    _instantBomb = _serviceProvider.GetRequiredService<IInstantBombService>();
    _antiTeamFlash = _serviceProvider.GetRequiredService<IAntiTeamFlashService>();
    _damageReport = _serviceProvider.GetRequiredService<IDamageReportService>();
    _smokeScenario = _serviceProvider.GetRequiredService<ISmokeScenarioService>();
    _messages = _serviceProvider.GetRequiredService<IMessageService>();
    _soloBot = _serviceProvider.GetRequiredService<ISoloBotService>();
    _afkManager = _serviceProvider.GetRequiredService<IAfkManagerService>();
    _gameMsgSuppression = _serviceProvider.GetRequiredService<IGameMessageSuppressionService>();

    // Initialize services that need explicit initialization
    _config.LoadOrCreate();
    _prefs.Initialize();

    // Create handlers with resolved services
    var random = _serviceProvider.GetRequiredService<Random>();

    _roundEventHandlers = new RoundEventHandlers(
      _pawnLifecycle, _spawnManager, _state, _config, _soloBot, _announcement,
      _messages, _allocation, _autoPlant, _clutch, _damageReport, _breaker, random, _queue, _buyMenu, _smokeScenario);

    _playerEventHandlers = new PlayerEventHandlers(
      _pawnLifecycle, _clutch, _prefs, _state, _config, _queue, _damageReport, _soloBot);

    _commandHandlers = new CommandHandlers(
      _mapConfig, _spawnManager, _pawnLifecycle, _spawnViz, _state, _prefs, _config, _smokeScenario);

    _mapEventHandlers = new MapEventHandlers(mapName =>
    {
      _pawnLifecycle!.Reset();
      _state!.ResetMatchState();
      _queue!.Reset();
      _mapConfig!.Load(mapName);
      _spawnManager!.SetSpawns(_mapConfig.Spawns);
      
      // Apply immediately and then with a short delay for reliability
      _config!.ApplyToConvars(false);
      Core.Scheduler.DelayBySeconds(0.5f, () => 
      {
        _config!.ApplyToConvars(false);
      });
    });

    // Register handlers
    _mapEventHandlers.Register(Core);
    _instantBomb.Register();
    _antiTeamFlash.Register();
    _afkManager.Register();
    _gameMsgSuppression.Register();
    _roundEventHandlers.Register(Core);
    _playerEventHandlers.Register(Core);
    _commandHandlers.Register(Core);
    _config.ApplyToConvars(false);
    _buyMenu.Initialize();

    Core.Scheduler.DelayBySeconds(1.0f, () =>
    {
      _config?.ApplyToConvars(false);
    });

    Core.Logger.LogPluginInformation("Retakes: plugin loaded successfully via DI.");
  }

  public override void Unload()
  {
    if (_mapEventHandlers is not null) _mapEventHandlers.Unregister(Core);
    if (_instantBomb is not null) _instantBomb.Unregister();
    if (_antiTeamFlash is not null) _antiTeamFlash.Unregister();
    if (_gameMsgSuppression is not null) _gameMsgSuppression.Unregister();
    if (_roundEventHandlers is not null) _roundEventHandlers.Unregister(Core);
    if (_playerEventHandlers is not null) _playerEventHandlers.Unregister(Core);
    if (_commandHandlers is not null) _commandHandlers.Unregister(Core);
    if (_buyMenu is not null) _buyMenu.Unregister();
    if (_afkManager is not null) _afkManager.Unregister();

    // Clear handler references
    _mapEventHandlers = null;
    _roundEventHandlers = null;
    _playerEventHandlers = null;
    _commandHandlers = null;

    // Clear service references
    _config = null;
    _mapConfig = null;
    _pawnLifecycle = null;
    _state = null;
    _prefs = null;
    _spawnManager = null;
    _spawnViz = null;
    _allocation = null;
    _announcement = null;
    _autoPlant = null;
    _clutch = null;
    _queue = null;
    _buyMenu = null;
    _breaker = null;
    _instantBomb = null;
    _antiTeamFlash = null;
    _damageReport = null;
    _smokeScenario = null;
    _messages = null;
    _soloBot = null;
    _afkManager = null;
    _gameMsgSuppression = null;

    // Dispose service provider and all registered services
    ServiceProviderFactory.DisposeServiceProvider(_serviceProvider);
    _serviceProvider = null;
  }

  /// <summary>
  /// Initializes the dependency injection container and resolves services.
  /// </summary>
  private void InitializeServices()
  {
    try
    {
      _serviceProvider = ServiceProviderFactory.CreateServiceProvider(Core, Core.Logger);
      Core.Logger.LogPluginInformation("Retakes: services initialized successfully via DI.");
    }
    catch (Exception ex)
    {
      Core.Logger.LogPluginError(ex, "Retakes: failed to initialize services.");
      throw;
    }
  }
}