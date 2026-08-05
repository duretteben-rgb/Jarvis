using System.Collections.Concurrent;
using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.Automation;

/// <summary>
/// Automation plugin for JARVIS OS. Maintains a registry of automation rules (name -> trigger
/// description) that other plugins or the user can define and run. Rules are stored in memory
/// for now; persisting them is a drop-in change inside <see cref="ExecuteCommandAsync"/>.
/// </summary>
public sealed class AutomationPlugin : JarvisPluginBase
{
    private readonly ConcurrentDictionary<string, string> _rules = new(StringComparer.OrdinalIgnoreCase);

    public AutomationPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.automation",
            Name = "Automation",
            Version = "1.0.0",
            Description = "Define and run scheduled or event-driven automations.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.Automation },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("automation.list", "Lists the registered automations."),
        new PluginCommand("automation.add", "Registers an automation by name and trigger."),
        new PluginCommand("automation.run", "Triggers an automation by name."),
    };

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "automation.list":
            {
                if (_rules.IsEmpty)
                {
                    return "No automations registered.";
                }

                return string.Join("\n", _rules
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => $"{pair.Key}: {pair.Value}"));
            }

            case "automation.add":
            {
                string? name = parameters?.GetValueOrDefault("name") as string;
                string? trigger = parameters?.GetValueOrDefault("trigger") as string ?? "manual";
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new PluginException(Manifest.Id, "An automation 'name' parameter is required.");
                }

                _rules[name] = trigger;
                Context.Logger.LogInformation("Automation '{Name}' registered (trigger: {Trigger}).", name, trigger);
                return $"Automation '{name}' registered.";
            }

            case "automation.run":
            {
                string? name = parameters?.GetValueOrDefault("name") as string;
                if (string.IsNullOrWhiteSpace(name) || !_rules.TryGetValue(name, out string? trigger))
                {
                    throw new PluginException(Manifest.Id, $"Unknown automation '{name}'.");
                }

                Context.Logger.LogInformation("Automation '{Name}' fired (trigger: {Trigger}).", name, trigger);
                return $"Automation '{name}' fired.";
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
