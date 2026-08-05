using System.Collections.Concurrent;
using Jarvis.Core.Configuration;
using Jarvis.SDK;
using Jarvis.SDK.Events;
using Jarvis.SDK.Events.SystemEvents;
using Jarvis.SDK.Host;
using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Default implementation of <see cref="IPluginManager"/>.
///
/// Loads plugins from the configured directory, then for each plugin:
/// <list type="bullet">
///   <item>validates the declared version against the running core,</item>
///   <item>orders plugins by their declared dependencies,</item>
///   <item>checks the requested permissions against the host permission policy,</item>
///   <item>drives the plugin through its lifecycle,</item>
///   <item>publishes <see cref="PluginLifecycleEvent"/> so other modules can observe changes.</item>
/// </list>
/// </summary>
public sealed class PluginManager : IPluginManager, IAsyncDisposable
{
    private readonly PluginLoader _loader;
    private readonly IJarvisHost _host;
    private readonly IEventBus _eventBus;
    private readonly IPermissionService _permissions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PluginManager> _logger;
    private readonly PluginRegistry _registry;
    private readonly JarvisConfiguration _configuration;

    private readonly ConcurrentDictionary<string, PluginRecord> _plugins = new();

    public PluginManager(
        PluginLoader loader,
        IJarvisHost host,
        IEventBus eventBus,
        IPermissionService permissions,
        ILoggerFactory loggerFactory,
        ILogger<PluginManager> logger,
        PluginRegistry registry,
        JarvisConfiguration configuration)
    {
        _loader = loader;
        _host = host;
        _eventBus = eventBus;
        _permissions = permissions;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _registry = registry;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public IReadOnlyList<IJarvisPlugin> Plugins => _registry.Snapshot();

    /// <inheritdoc />
    public IReadOnlyList<PluginInfo> GetPluginInfos()
        => _plugins.Values
            .Select(record => new PluginInfo(
                record.Plugin.Manifest.Id,
                record.Plugin.Manifest.Name,
                record.Plugin.Manifest.Version,
                record.Plugin.State,
                record.Directory,
                record.GrantedPermissions,
                record.Plugin.Commands.Select(command => command.Name).ToArray()))
            .ToArray();

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
        (IReadOnlyList<PluginDescriptor> ordered, IReadOnlyList<string> errors) =
            PluginDependencyResolver.Resolve(discovered);

        foreach (string error in errors)
        {
            _logger.LogWarning("Plugin dependency resolution: {Error}", error);
        }

        foreach (PluginDescriptor descriptor in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadPluginCoreAsync(descriptor, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IJarvisPlugin?> LoadPluginAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        PluginDescriptor? descriptor = _loader.DiscoverSingle(pluginDirectory);
        if (descriptor is null)
        {
            _logger.LogWarning("No plugin found in {Directory}.", pluginDirectory);
            return null;
        }

        return await LoadPluginCoreAsync(descriptor, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryRemove(pluginId, out PluginRecord? record))
        {
            _logger.LogDebug("Plugin '{Plugin}' is not loaded.", pluginId);
            return false;
        }

        await StopPluginAsync(record, cancellationToken);
        _registry.Remove(record.Plugin);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> UnloadPluginByDirectoryAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        PluginRecord? record = _plugins.Values
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Directory, pluginDirectory, StringComparison.OrdinalIgnoreCase));

        return record is null
            ? Task.FromResult(false)
            : UnloadPluginAsync(record.Plugin.Manifest.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAndUnloadPluginsAsync(CancellationToken cancellationToken = default)
    {
        foreach (PluginRecord record in _plugins.Values.Reverse())
        {
            await StopPluginAsync(record, cancellationToken);
        }

        _plugins.Clear();
        _registry.Clear();
    }

    /// <inheritdoc />
    public async Task<object?> ExecuteCommandAsync(
        string pluginId,
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out PluginRecord? record))
        {
            throw new PluginException(pluginId, $"Plugin '{pluginId}' is not loaded.");
        }

        try
        {
            object? result = await record.Plugin.ExecuteCommandAsync(command, parameters, cancellationToken);
            await PublishCommandEventAsync(pluginId, command, succeeded: true, error: null, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await PublishCommandEventAsync(pluginId, command, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<IJarvisPlugin?> LoadPluginCoreAsync(PluginDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.PluginType.GetConstructor(Type.EmptyTypes) is null)
        {
            _logger.LogWarning("Plugin {Type} has no parameterless constructor and was skipped.",
                descriptor.PluginType.FullName);
            return null;
        }

        var plugin = (IJarvisPlugin)Activator.CreateInstance(descriptor.PluginType)!;
        PluginManifest manifest = plugin.Manifest;

        if (_plugins.ContainsKey(manifest.Id))
        {
            _logger.LogWarning("Plugin '{Plugin}' is already loaded. Skipping duplicate.", manifest.Id);
            await plugin.DisposeAsync();
            descriptor.Unload();
            return null;
        }

        (bool valid, string? error) = PluginVersionValidator.Validate(manifest, JarvisVersions.Platform);
        if (!valid)
        {
            _logger.LogError("{Error}", error);
            await plugin.DisposeAsync();
            descriptor.Unload();
            return null;
        }

        IReadOnlyList<string> grantedPermissions;
        try
        {
            grantedPermissions = GrantPermissions(manifest);
        }
        catch (PluginException ex)
        {
            _logger.LogError("Plugin '{Plugin}' was rejected: {Message}", manifest.Id, ex.Message);
            await PublishLifecycleAsync(manifest.Id, PluginLifecycleState.Failed, manifest.Version, cancellationToken);
            await plugin.DisposeAsync();
            descriptor.Unload();
            return null;
        }

        var context = new PluginContext
        {
            Manifest = manifest,
            PluginDirectory = descriptor.Directory,
            Logger = _loggerFactory.CreateLogger($"Jarvis.Plugin.{manifest.Id}"),
            Host = _host,
        };

        try
        {
            _logger.LogInformation("Initializing plugin {Plugin} ({Version}).", manifest.Id, manifest.Version);
            await PublishLifecycleAsync(manifest.Id, PluginLifecycleState.Initialized, manifest.Version, cancellationToken);
            await plugin.InitializeAsync(context, cancellationToken);

            await PublishLifecycleAsync(manifest.Id, PluginLifecycleState.Starting, manifest.Version, cancellationToken);
            await plugin.StartAsync(cancellationToken);

            _registry.Add(plugin);
            _plugins[manifest.Id] = new PluginRecord(plugin, descriptor, grantedPermissions);

            await PublishLifecycleAsync(manifest.Id, PluginLifecycleState.Running, manifest.Version, cancellationToken);
            _logger.LogInformation("Plugin {Plugin} is running.", manifest.Id);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin '{Plugin}' failed to load.", manifest.Id);
            await PublishLifecycleAsync(manifest.Id, PluginLifecycleState.Failed, manifest.Version, cancellationToken);
            _permissions.RevokeAll(manifest.Id);
            await plugin.DisposeAsync();
            descriptor.Unload();
            return null;
        }
    }

    private IReadOnlyList<string> GrantPermissions(PluginManifest manifest)
    {
        PermissionOptions policy = _configuration.Permissions;

        // Validate everything first so a rejected plugin never receives partial grants.
        foreach (string permission in manifest.Permissions)
        {
            bool allowed = policy.AllowAll || policy.Allowed.Contains(permission, StringComparer.Ordinal);
            if (!allowed)
            {
                throw new PluginException(manifest.Id,
                    $"Permission '{permission}' is not allowed by the host permission policy.");
            }
        }

        var granted = new List<string>(manifest.Permissions.Count);
        foreach (string permission in manifest.Permissions)
        {
            _permissions.Grant(manifest.Id, permission);
            granted.Add(permission);
        }

        return granted;
    }

    private async Task StopPluginAsync(PluginRecord record, CancellationToken cancellationToken)
    {
        PluginManifest manifest = record.Plugin.Manifest;
        try
        {
            _logger.LogInformation("Stopping plugin {Plugin} ({Version}).", manifest.Id, manifest.Version);
            await record.Plugin.StopAsync(cancellationToken);
            await record.Plugin.DisposeAsync();
            _permissions.RevokeAll(manifest.Id);

            await PublishLifecycleAsync(manifest.Id, PluginLifecycleState.Stopped, manifest.Version, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin '{Plugin}' failed to stop cleanly.", manifest.Id);
        }
        finally
        {
            record.Descriptor.Unload();
        }
    }

    private Task PublishLifecycleAsync(string pluginId, PluginLifecycleState state, string version, CancellationToken cancellationToken)
        => _eventBus.PublishAsync(new PluginLifecycleEvent(pluginId, state, version), cancellationToken);

    private Task PublishCommandEventAsync(string pluginId, string command, bool succeeded, string? error, CancellationToken cancellationToken)
        => _eventBus.PublishAsync(new PluginCommandEvent(pluginId, command, succeeded, error), cancellationToken);

    private static string ResolvePluginDirectory(string configuredDirectory)
    {
        if (Path.IsPathRooted(configuredDirectory))
        {
            return configuredDirectory;
        }

        return Path.Combine(AppContext.BaseDirectory, configuredDirectory);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed record PluginRecord(
        IJarvisPlugin Plugin,
        PluginDescriptor Descriptor,
        IReadOnlyList<string> GrantedPermissions)
    {
        public string Directory => Descriptor.Directory;
    }
}
