namespace Jarvis.SDK.Plugins;

/// <summary>
/// Raised when a plugin cannot be loaded, initialized, started or stopped by the host.
/// </summary>
public sealed class PluginException : Exception
{
    public PluginException(string pluginId, string message)
        : base(message)
    {
        PluginId = pluginId;
    }

    public PluginException(string pluginId, string message, Exception innerException)
        : base(message, innerException)
    {
        PluginId = pluginId;
    }

    /// <summary>Identifier of the plugin that caused the failure.</summary>
    public string PluginId { get; }
}
