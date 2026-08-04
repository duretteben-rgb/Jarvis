using Jarvis.Core.DependencyInjection;
using Jarvis.Core.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Hosting;

/// <summary>
/// Builds a fully wired JARVIS host: configuration, logging, dependency injection, core
/// services, event bus and automatic plugin loading. Both the headless runtime and the
/// desktop UI start from this factory.
/// </summary>
public static class JarvisHostFactory
{
    /// <summary>
    /// Creates a <see cref="Microsoft.Extensions.Hosting.HostApplicationBuilder"/> pre-configured
    /// with the JARVIS core. Callers may register additional services, hosted services and
    /// options before building.
    /// </summary>
    /// <param name="args">Command line arguments merged into the configuration stack.</param>
    public static HostApplicationBuilder CreateJarvisHostBuilder(string[]? args = null)
    {
        var settings = new HostApplicationBuilderSettings
        {
            Args = args ?? [],
            // Anchor configuration to the executable directory so appsettings.json is always
            // found no matter where the process is launched from.
            ContentRootPath = AppContext.BaseDirectory,
        };

        HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(settings);

        builder.Logging.ClearProviders();
        JarvisLogging.Configure(builder.Logging);

        builder.Services.AddJarvisCore();

        return builder;
    }
}
