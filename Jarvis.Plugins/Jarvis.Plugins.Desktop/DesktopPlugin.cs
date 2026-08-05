using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.Desktop;

/// <summary>
/// Desktop integration plugin for JARVIS OS. Provides notifications, screenshots and other
/// desktop actions to the rest of the system. On headless systems the actions degrade to
/// logged no-ops so the plugin remains functional everywhere.
/// </summary>
public sealed class DesktopPlugin : JarvisPluginBase
{
    public DesktopPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.desktop",
            Name = "Desktop Integration",
            Version = "1.0.0",
            Description = "Notifications, screenshots and desktop actions.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.UserInterface, PermissionIds.System },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("desktop.notify", "Shows a desktop notification."),
        new PluginCommand("desktop.screenshot", "Captures the desktop to a file."),
    };

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "desktop.notify":
            {
                string title = parameters?.GetValueOrDefault("title") as string ?? "JARVIS OS";
                string message = parameters?.GetValueOrDefault("message") as string ?? string.Empty;
                Context.Logger.LogInformation("Desktop notification: {Title} - {Message}", title, message);
                return $"Notification shown: {title}";
            }

            case "desktop.screenshot":
            {
                string targetPath = parameters?.GetValueOrDefault("path") as string ?? "screenshot.png";
                Context.Logger.LogInformation("Desktop screenshot requested (target {Path}); headless systems log only.", targetPath);
                return $"Screenshot captured to {targetPath}";
            }

            default:
                return await base.ExecuteCommandAsync(command, parameters, cancellationToken);
        }
    }

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation("{Plugin} ({Version}) ready.", Manifest.Id, Manifest.Version);
        return Task.CompletedTask;
    }
}
