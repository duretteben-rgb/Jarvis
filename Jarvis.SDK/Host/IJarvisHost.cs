using Jarvis.SDK.Configuration;
using Jarvis.SDK.Events;
using Jarvis.SDK.Plugins;

namespace Jarvis.SDK.Host;

/// <summary>
/// Public API surface of a running JARVIS OS instance. It is the only gateway through which
/// plugins and modules access shared system capabilities.
/// </summary>
public interface IJarvisHost
{
    /// <summary>Version of the JARVIS platform.</summary>
    string ApplicationVersion { get; }

    /// <summary>Configured display name of this instance.</summary>
    string InstanceName { get; }

    /// <summary>Read-only access to the configuration store.</summary>
    IJarvisConfiguration Configuration { get; }

    /// <summary>System-wide event bus used for decoupled communication.</summary>
    IEventBus EventBus { get; }

    /// <summary>Service locator exposing registered host services.</summary>
    IServiceProvider Services { get; }

    /// <summary>Snapshot of all plugins currently loaded and managed by the host.</summary>
    IReadOnlyList<IJarvisPlugin> Plugins { get; }
}
