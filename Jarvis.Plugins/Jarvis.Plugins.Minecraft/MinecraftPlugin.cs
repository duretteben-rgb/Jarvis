using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.Minecraft;

/// <summary>
/// Minecraft server management plugin for JARVIS OS. Exposes commands to launch, stop and
/// inspect the status of a Minecraft server. The server lifecycle is simulated for now;
/// wiring a real server process is a drop-in replacement inside <see cref="ExecuteCommandAsync"/>.
/// </summary>
public sealed class MinecraftPlugin : JarvisPluginBase
{
    private readonly object _gate = new();
    private bool _running;
    private DateTimeOffset _startedAt;

    public MinecraftPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.minecraft",
            Name = "Minecraft",
            Version = "1.0.0",
            Description = "Launch, stop and monitor Minecraft servers.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.Processes, PermissionIds.Files },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("minecraft.launch", "Starts the Minecraft server."),
        new PluginCommand("minecraft.stop", "Stops the Minecraft server."),
        new PluginCommand("minecraft.status", "Reports whether the Minecraft server is running."),
    };

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!Context.Host.Permissions.IsGranted(Manifest.Id, PermissionIds.Processes))
        {
            throw new PluginException(Manifest.Id, "Missing 'processes' permission.");
        }

        switch (command)
        {
            case "minecraft.launch":
            {
                lock (_gate)
                {
                    if (_running)
                    {
                        return "Minecraft server is already running.";
                    }

                    _running = true;
                    _startedAt = DateTimeOffset.UtcNow;
                }

                Context.Logger.LogInformation("Minecraft server starting.");
                return "Minecraft server started.";
            }

            case "minecraft.stop":
            {
                lock (_gate)
                {
                    if (!_running)
                    {
                        return "Minecraft server is not running.";
                    }

                    _running = false;
                }

                Context.Logger.LogInformation("Minecraft server stopping.");
                return "Minecraft server stopped.";
            }

            case "minecraft.status":
            {
                bool running;
                TimeSpan uptime;
                lock (_gate)
                {
                    running = _running;
                    uptime = _running ? DateTimeOffset.UtcNow - _startedAt : TimeSpan.Zero;
                }

                return running
                    ? $"Minecraft server is running (uptime {uptime.TotalMinutes:F1} min)."
                    : "Minecraft server is stopped.";
            }

            default:
                return await base.ExecuteCommandAsync(command, parameters, cancellationToken);
        }
    }

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation("{Plugin} ({Version}) ready; {Count} commands available.",
            Manifest.Id, Manifest.Version, Commands.Count);
        return Task.CompletedTask;
    }
}
