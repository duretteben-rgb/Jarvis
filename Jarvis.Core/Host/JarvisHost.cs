using Jarvis.Core.Plugins;
using Jarvis.SDK;
using Jarvis.SDK.Configuration;
using Jarvis.SDK.Events;
using Jarvis.SDK.Host;
using Jarvis.SDK.Plugins;

namespace Jarvis.Core.Host;

/// <summary>
/// Public implementation of <see cref="IJarvisHost"/>. Exposes shared system capabilities
/// (configuration, event bus, service locator and the plugin list) to plugins and modules.
/// </summary>
public sealed class JarvisHost : IJarvisHost
{
    private readonly IJarvisConfiguration _configuration;
    private readonly IEventBus _eventBus;
    private readonly IServiceProvider _services;
    private readonly PluginRegistry _registry;

    public JarvisHost(
        IJarvisConfiguration configuration,
        IEventBus eventBus,
        IServiceProvider services,
        PluginRegistry registry)
    {
        _configuration = configuration;
        _eventBus = eventBus;
        _services = services;
        _registry = registry;
    }

    /// <inheritdoc />
    public string ApplicationVersion => JarvisVersions.Platform;

    /// <inheritdoc />
    public string InstanceName => _configuration.GetValue("Jarvis:InstanceName") ?? "JARVIS OS";

    /// <inheritdoc />
    public IJarvisConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public IEventBus EventBus => _eventBus;

    /// <inheritdoc />
    public IServiceProvider Services => _services;

    /// <inheritdoc />
    public IReadOnlyList<IJarvisPlugin> Plugins => _registry.Snapshot();
}
