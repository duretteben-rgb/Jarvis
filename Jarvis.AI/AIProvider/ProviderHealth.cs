namespace Jarvis.AI.AIProvider;

/// <summary>
/// Result of a provider health probe.
/// </summary>
public sealed class ProviderHealth
{
    private ProviderHealth()
    {
    }

    /// <summary>True when the provider could be reached and is usable.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Model ids observed during the probe, when available.</summary>
    public IReadOnlyList<string> Models { get; init; } = Array.Empty<string>();

    /// <summary>Human readable error when the probe failed, otherwise null.</summary>
    public string? Error { get; init; }

    /// <summary>Creates a healthy result.</summary>
    public static ProviderHealth Ok(IReadOnlyList<string>? models = null) =>
        new() { IsAvailable = true, Models = models ?? Array.Empty<string>() };

    /// <summary>Creates a failed result.</summary>
    public static ProviderHealth Fail(string error) => new() { IsAvailable = false, Error = error };
}
