using Avalonia;
using Jarvis.Core.Hosting;
using Jarvis.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jarvis.UI;

/// <summary>
/// Entry point of the JARVIS OS desktop interface.
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Build the JARVIS host (configuration, logging, DI, event bus, plugins).
        HostApplicationBuilder builder = JarvisHostFactory.CreateJarvisHostBuilder(args);
        builder.Services.AddHostedService<HeartbeatService>();
        builder.Services.AddSingleton<MainViewModel>();

        using IHost host = builder.Build();
        host.StartAsync().GetAwaiter().GetResult();

        App.ServiceProvider = host.Services;

        // Run the Avalonia desktop shell.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        host.StopAsync().GetAwaiter().GetResult();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
