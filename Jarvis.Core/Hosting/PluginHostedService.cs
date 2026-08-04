using Jarvis.Core.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Hosting;

/// <summary>
/// Hosted service that starts plugins when the host starts and gracefully stops and unloads
/// them when the host shuts down.
/// </summary>
public sealed class PluginHostedService : IHostedService
{
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<PluginHostedService> _logger;

    public PluginHostedService(IPluginManager pluginManager, ILogger<PluginHostedService> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _pluginManager.LoadAndStartPluginsAsync(cancellationToken);
        _logger.LogInformation("{Count} plugin(s) active.", _pluginManager.Plugins.Count);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _pluginManager.StopAndUnloadPluginsAsync(cancellationToken);
    }
}
