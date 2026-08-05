using Jarvis.Core.DependencyInjection;
using Jarvis.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
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
    /// Applies the JARVIS core stack to any host builder: clears the default providers,
    /// configures JARVIS logging and registers <c>AddJarvisCore()</c>. Used by the headless
    /// runtime, the desktop UI and the web API host.
    /// </summary>
    public static void Configure(ILoggingBuilder logging, IServiceCollection services)
    {
        logging.ClearProviders();
        JarvisLogging.Configure(logging);
        services.AddJarvisCore();
    }

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
        Configure(builder.Logging, builder.Services);

        return builder;
    }
}
