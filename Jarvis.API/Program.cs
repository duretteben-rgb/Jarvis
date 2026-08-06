using Jarvis.AI.DependencyInjection;
using Jarvis.API.Endpoints;
using Jarvis.Core.Hosting;
using Jarvis.Memory.DependencyInjection;
using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Jarvis.API;

/// <summary>
/// Entry point of the JARVIS web host. Boots the JARVIS core stack (event bus, plugins, memory,
/// AI), enables CORS for the JARVIS HUB (Electron and browser preview) and serves the Hub
/// renderer together with the <c>/api/*</c> endpoints.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseContentRoot(AppContext.BaseDirectory);
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

        JarvisHostFactory.Configure(builder.Logging, builder.Services);
        builder.Services.AddJarvisMemory(builder.Configuration);
        builder.Services.AddJarvisAI(builder.Configuration);
        builder.Services.AddHostedService<Jarvis.Core.Hosting.HeartbeatService>();

        builder.Services.AddCors(static (CorsOptions options) =>
        {
            // The Hub runs as an Electron renderer (file:// origin) or in a browser preview on
            // any port, so the API must accept cross-origin calls.
            options.AddDefaultPolicy(static policy => policy
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin());
        });

        WebApplication app = builder.Build();

        app.UseCors();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapJarvisApi();

        await app.StartAsync();
        return await WaitForShutdownAsync(app);
    }

    private static async Task<int> WaitForShutdownAsync(WebApplication app)
    {
        await app.WaitForShutdownAsync();
        return 0;
    }
}
