using Jarvis.Core.Configuration;
using Jarvis.SDK.Events;
using Jarvis.SDK.Events.SystemEvents;
using Jarvis.SDK.Host;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Default implementation of <see cref="IPluginManager"/>. Loads plugins from the configured
/// directory, drives each plugin through its lifecycle and publishes
/// <see cref="PluginLifecycleEvent"/> so other modules can observe plugin state changes.
/// </summary>
public sealed class PluginManager : IPluginManager, IAsyncDisposable
{
    private readonly PluginLoader _loader;
    private readonly IJarvisHost _host;
    private readonly IEventBus _eventBus;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginManager> _logger;
    private readonly PluginRegistry _registry;
    private readonly JarvisConfiguration _configuration;
    private readonly List<PluginDescriptor> _descriptors = new();

    public PluginManager(
        PluginLoader loader,
        IJarvisHost host,
        IEventBus eventBus,
        ILoggerFactory loggerFactory,
        ILogger<PluginManager> logger,
        PluginRegistry registry,
        JarvisConfiguration configuration)
    {
        _loader = loader;
        _host = host;
        _eventBus = eventBus;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _registry = registry;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public IReadOnlyList<IJarvisPlugin> Plugins => _registry.Snapshot();

    /// <inheritdoc />
    public async Task LoadAndStartPluginsAsync(CancellationToken cancellationToken = default)
    {
        PluginOptions options = _configuration.Plugins;
        if (!options.Enabled)
        {
            _logger.LogInformation("Plugin loading is disabled by configuration.");
            return;
        }

        string rootDirectory = ResolvePluginDirectory(options.Directory);
        _logger.LogInformation("Discovering plugins in {Directory}.", rootDirectory);

        IReadOnlyList<PluginDescriptor> discovered = _loader.Discover(rootDirectory);
        foreach (PluginDescriptor descriptor in discovered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await LoadAndStartPluginAsync(descriptor, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin in {Directory} failed to load.", descriptor.Directory);
            }
        }
    }

    /// <inheritdoc />
    public async Task StopAndUnloadPluginsAsync(CancellationToken cancellationToken = default)
    {
        foreach (IJarvisPlugin plugin in _registry.Snapshot().Reverse())
        {
            try
            {
                _logger.LogInformation("Stopping plugin {Plugin} ({Version}).",
                    plugin.Manifest.Id, plugin.Manifest.Version);

                await plugin.StopAsync(cancellationToken);
                await plugin.DisposeAsync();

                await PublishLifecycleAsync(plugin.Manifest.Id, PluginLifecycleState.Stopped, plugin.Manifest.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin {Plugin} failed to stop.", plugin.Manifest.Id);
            }
        }

        _registry.Clear();

        foreach (PluginDescriptor descriptor in _descriptors)
        {
            descriptor.Unload();
        }

        _descriptors.Clear();
    }

    private async Task LoadAndStartPluginAsync(PluginDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.PluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            _logger.LogWarning("Plugin {Type} has no parameterless constructor and was skipped.",
                descriptor.PluginType.FullName);
            return;
        }

        var plugin = (IJarvisPlugin)Activator.CreateInstance(descriptor.PluginType)!;

        var context = new PluginContext
        {
            Manifest = plugin.Manifest,
            PluginDirectory = descriptor.Directory,
            Logger = _loggerFactory.CreateLogger($"Jarvis.Plugin.{plugin.Manifest.Id}"),
            Host = _host,
        };

        _logger.LogInformation("Initializing plugin {Plugin} ({Version}).",
            plugin.Manifest.Id, plugin.Manifest.Version);

        await PublishLifecycleAsync(plugin.Manifest.Id, PluginLifecycleState.Initialized, plugin.Manifest.Version);
        await plugin.InitializeAsync(context, cancellationToken);

        await PublishLifecycleAsync(plugin.Manifest.Id, PluginLifecycleState.Starting, plugin.Manifest.Version);
        await plugin.StartAsync(cancellationToken);

        _registry.Add(plugin);
        _descriptors.Add(descriptor);

        await PublishLifecycleAsync(plugin.Manifest.Id, PluginLifecycleState.Running, plugin.Manifest.Version);
        _logger.LogInformation("Plugin {Plugin} is running.", plugin.Manifest.Id);
    }

    private Task PublishLifecycleAsync(string pluginId, PluginLifecycleState state, string version)
        => _eventBus.PublishAsync(new PluginLifecycleEvent(pluginId, state, version));

    private static string ResolvePluginDirectory(string configuredDirectory)
    {
        if (Path.IsPathRooted(configuredDirectory))
        {
            return configuredDirectory;
        }

        return Path.Combine(AppContext.BaseDirectory, configuredDirectory);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
