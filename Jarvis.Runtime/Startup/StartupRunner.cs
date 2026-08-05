using Jarvis.Core.Hosting;
using Jarvis.Memory.DependencyInjection;
using Jarvis.Runtime.ProcessManager;
using Jarvis.SDK.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Runtime.Startup;

/// <summary>
/// Boots the JARVIS runtime: builds the host, wires runtime-specific services, starts the
/// system and blocks until the host is told to shut down.
/// </summary>
public static class StartupRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        HostApplicationBuilder builder = JarvisHostFactory.CreateJarvisHostBuilder(args);

        // Runtime services: process management, heartbeats and the JARVIS memory system.
        builder.Services.AddSingleton<IProcessManager, global::Jarvis.Runtime.ProcessManager.ProcessManager>();
        builder.Services.AddHostedService<Jarvis.Core.Hosting.HeartbeatService>();
        builder.Services.AddJarvisMemory(builder.Configuration);

        using IHost host = builder.Build();
        IJarvisHost jarvisHost = host.Services.GetRequiredService<IJarvisHost>();
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Jarvis.Runtime");

        logger.LogInformation("Starting {Instance} (v{Version})...",
            jarvisHost.InstanceName, jarvisHost.ApplicationVersion);

        await host.StartAsync();

        logger.LogInformation("JARVIS OS runtime is running. Press Ctrl+C to stop.");

        // Blocks until Ctrl+C / SIGTERM / StopApplication is raised by the host lifetime.
        await host.WaitForShutdownAsync();

        logger.LogInformation("JARVIS OS runtime stopped.");
        return 0;
    }
}
