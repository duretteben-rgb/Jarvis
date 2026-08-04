using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Runtime.BackgroundServices;

/// <summary>
/// Base class for long-running background services in the JARVIS runtime. Adds structured
/// error handling and graceful shutdown semantics on top of <see cref="BackgroundService"/>.
/// </summary>
public abstract class JarvisBackgroundService : BackgroundService
{
    protected JarvisBackgroundService(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>Logger for the concrete service.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExecuteCoreAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Logger.LogDebug("{Service} cancelled during shutdown.", GetType().Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Service} terminated unexpectedly.", GetType().Name);
        }
    }

    /// <summary>Core loop of the background service. Must observe <paramref name="stoppingToken"/>.</summary>
    protected abstract Task ExecuteCoreAsync(CancellationToken stoppingToken);
}
