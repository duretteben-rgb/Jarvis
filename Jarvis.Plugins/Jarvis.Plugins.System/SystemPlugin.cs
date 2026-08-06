using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.System;

/// <summary>
/// PC control plugin for JARVIS OS. Exposes processes, files, hardware metrics and application
/// launching to the host and the HUB. All operations are permission-gated and degrade gracefully
/// on restricted or headless hosts.
/// </summary>
public sealed class SystemPlugin : JarvisPluginBase
{
    public SystemPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.system",
            Name = "System Control",
            Version = "1.0.0",
            Description = "Process, file, hardware and application control.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.Processes, PermissionIds.Files, PermissionIds.System },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("system.process.list", "Lists running processes, optionally filtered by name."),
        new PluginCommand("system.process.info", "Returns details about a single process by pid or name."),
        new PluginCommand("system.process.start", "Starts a process by executable path or command."),
        new PluginCommand("system.process.kill", "Stops a process by pid or name."),
        new PluginCommand("system.file.list", "Lists a directory, optionally filtered by a glob pattern."),
        new PluginCommand("system.file.read", "Reads a text file, optionally truncated to a byte limit."),
        new PluginCommand("system.file.write", "Writes (or appends) a text file."),
        new PluginCommand("system.file.copy", "Copies a file or directory tree."),
        new PluginCommand("system.file.move", "Moves a file or directory."),
        new PluginCommand("system.file.search", "Recursively searches a directory for matching names."),
        new PluginCommand("system.app.launch", "Launches an application by name or path."),
        new PluginCommand("system.app.stop", "Stops all processes matching an application name."),
        new PluginCommand("system.app.running", "Reports whether an application is currently running."),
        new PluginCommand("system.hardware.metrics", "Returns CPU, memory, disk and uptime metrics."),
    };

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "system.process.list":
            {
                string query = GetString(parameters, "name") ?? GetString(parameters, "query") ?? string.Empty;
                int limit = GetInt(parameters, "limit") ?? 100;
                return Join(ProcessManager.List(query, Math.Clamp(limit, 1, 500)));
            }

            case "system.process.info":
            {
                string idOrName = Required(parameters, "id");
                return ProcessManager.Find(idOrName)?.ToString()
                    ?? $"No process found for '{idOrName}'.";
            }

            case "system.process.start":
            {
                string path = Required(parameters, "path");
                string? arguments = GetString(parameters, "arguments");
                int pid = ProcessManager.Start(path, arguments);
                return $"Started {path} with pid {pid}.";
            }

            case "system.process.kill":
            {
                string idOrName = Required(parameters, "id");
                bool stopped = ProcessManager.Stop(idOrName);
                return stopped ? $"Stopped process '{idOrName}'." : $"No running process found for '{idOrName}'.";
            }

            case "system.file.list":
            {
                string path = Required(parameters, "path");
                string? pattern = GetString(parameters, "pattern");
                return Join(FileManager.List(path, pattern));
            }

            case "system.file.read":
            {
                string path = Required(parameters, "path");
                int maxBytes = GetInt(parameters, "maxBytes") ?? 64 * 1024;
                (string content, bool truncated) = FileManager.Read(path, Math.Clamp(maxBytes, 1024, 10 * 1024 * 1024));
                return truncated ? content + "\n... [truncated]" : content;
            }

            case "system.file.write":
            {
                string path = Required(parameters, "path");
                string content = GetString(parameters, "content") ?? string.Empty;
                bool append = GetBool(parameters, "append") ?? false;
                FileManager.Write(path, content, append);
                return $"Wrote {(append ? "(appended) " : string.Empty)}{path}.";
            }

            case "system.file.copy":
            {
                string source = Required(parameters, "source");
                string destination = Required(parameters, "destination");
                FileManager.Copy(source, destination);
                return $"Copied {source} to {destination}.";
            }

            case "system.file.move":
            {
                string source = Required(parameters, "source");
                string destination = Required(parameters, "destination");
                FileManager.Move(source, destination);
                return $"Moved {source} to {destination}.";
            }

            case "system.file.search":
            {
                string root = Required(parameters, "root");
                string pattern = GetString(parameters, "pattern") ?? "*";
                int maxResults = GetInt(parameters, "maxResults") ?? 50;
                return Join(FileManager.Search(root, pattern, Math.Clamp(maxResults, 1, 500)));
            }

            case "system.app.launch":
            {
                string name = Required(parameters, "name");
                string? arguments = GetString(parameters, "arguments");
                int pid = AppLauncher.Launch(name, arguments);
                return $"Launched {name} with pid {pid}.";
            }

            case "system.app.stop":
            {
                string name = Required(parameters, "name");
                bool stopped = AppLauncher.Stop(name);
                return stopped ? $"Stopped application '{name}'." : $"Application '{name}' was not running.";
            }

            case "system.app.running":
            {
                string name = Required(parameters, "name");
                return AppLauncher.IsRunning(name) ? $"'{name}' is running." : $"'{name}' is not running.";
            }

            case "system.hardware.metrics":
            {
                return HardwareMonitor.Snapshot().ToString();
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

    private static string Join(IEnumerable<object?> items)
    {
        var lines = items.Select(item => item?.ToString() ?? string.Empty).ToList();
        return lines.Count == 0 ? "(no results)" : string.Join('\n', lines);
    }

    private string Required(IReadOnlyDictionary<string, object?>? parameters, string key)
        => GetString(parameters, key)
            ?? throw new PluginException(Manifest.Id, $"Parameter '{key}' is required.");

    private static string? GetString(IReadOnlyDictionary<string, object?>? parameters, string key)
        => parameters?.TryGetValue(key, out object? value) == true ? value as string : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out object? value) != true || value is null)
        {
            return null;
        }

        return value is int integer
            ? integer
            : int.TryParse(value.ToString(), out int parsed) ? parsed : null;
    }

    private static bool? GetBool(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out object? value) != true || value is null)
        {
            return null;
        }

        return value is bool boolean
            ? boolean
            : bool.TryParse(value.ToString(), out bool parsed) ? parsed : null;
    }
}
