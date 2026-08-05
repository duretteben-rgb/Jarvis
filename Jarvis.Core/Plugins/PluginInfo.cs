using Jarvis.SDK.Events.SystemEvents;

namespace Jarvis.Core.Plugins;

/// <summary>
/// Immutable snapshot describing a loaded plugin. Used for management and diagnostics.
/// </summary>
public sealed record PluginInfo(
    string Id,
    string Name,
    string Version,
    PluginLifecycleState State,
    string Directory,
    IReadOnlyList<string> GrantedPermissions,
    IReadOnlyList<string> Commands);
