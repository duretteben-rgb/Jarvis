using Jarvis.SDK.Events;

namespace Jarvis.SDK.Events.SystemEvents;

/// <summary>
/// Raised by the plugin manager whenever a plugin transitions between lifecycle states.
/// </summary>
public sealed class PluginLifecycleEvent : JarvisEvent
{
    public PluginLifecycleEvent(string pluginId, PluginLifecycleState state, string version)
        : base("Jarvis.Core.Plugins")
    {
        PluginId = pluginId;
        State = state;
        Version = version;
    }

    /// <summary>Stable identifier of the plugin.</summary>
    public string PluginId { get; }

    /// <summary>New lifecycle state reached by the plugin.</summary>
    public PluginLifecycleState State { get; }

    /// <summary>Plugin version that transitioned.</summary>
    public string Version { get; }
}
