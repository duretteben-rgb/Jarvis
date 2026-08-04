namespace Jarvis.Core.Configuration;

/// <summary>
/// Options bound from the top level <c>Jarvis</c> configuration section.
/// </summary>
public sealed class JarvisOptions
{
    /// <summary>Display name of this JARVIS instance.</summary>
    public string InstanceName { get; set; } = "JARVIS OS";

    /// <summary>Interval, in seconds, between heartbeat events published by the runtime.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 5;
}
