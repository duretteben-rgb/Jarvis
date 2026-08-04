using Jarvis.Core.Configuration;
using Jarvis.Core.EventBus;
using Jarvis.Core.Host;
using Jarvis.Core.Hosting;
using Jarvis.Core.Plugins;
using Jarvis.Core.ServiceManager;
using Jarvis.SDK.Configuration;
using Jarvis.SDK.Events;
using Jarvis.SDK.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jarvis.Core.DependencyInjection;

/// <summary>
/// Registers the JARVIS core services into a service collection.
/// </summary>
public static class JarvisServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core JARVIS stack: configuration, event bus, service manager, plugin system
    /// and the public host API. Also registers <see cref="PluginHostedService"/> so plugins
    /// are loaded and stopped with the host.
    /// </summary>
    public static IServiceCollection AddJarvisCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<JarvisConfiguration>();
        services.TryAddSingleton<IJarvisConfiguration>(sp => sp.GetRequiredService<JarvisConfiguration>());

        services.TryAddSingleton<IEventBus, global::Jarvis.Core.EventBus.EventBus>();

        services.TryAddSingleton<PluginRegistry>();
        services.TryAddSingleton<PluginLoader>();
        services.TryAddSingleton<IPluginManager, PluginManager>();

        services.TryAddSingleton<IServiceManager, global::Jarvis.Core.ServiceManager.ServiceManager>();

        services.TryAddSingleton<IJarvisHost, JarvisHost>();

        services.AddHostedService<PluginHostedService>();

        return services;
    }
}
