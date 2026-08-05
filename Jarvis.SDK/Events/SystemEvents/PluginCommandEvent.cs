using Jarvis.SDK.Events;

namespace Jarvis.SDK.Events.SystemEvents;

/// <summary>
/// Raised after a plugin command is executed, reporting whether it succeeded.
/// </summary>
public sealed class PluginCommandEvent : JarvisEvent
{
    public PluginCommandEvent(string pluginId, string command, bool succeeded, string? error = null)
        : base("Jarvis.Core.Plugins")
    {
        PluginId = pluginId;
        Command = command;
        Succeeded = succeeded;
        Error = error;
    }

    /// <summary>Id of the plugin that owns the command.</summary>
    public string PluginId { get; }

    /// <summary>Name of the command that was invoked.</summary>
    public string Command { get; }

    /// <summary>True when the command completed without throwing.</summary>
    public bool Succeeded { get; }

    /// <summary>Error message when the command failed, otherwise null.</summary>
    public string? Error { get; }
}
