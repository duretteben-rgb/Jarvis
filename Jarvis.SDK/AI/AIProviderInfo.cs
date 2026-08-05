namespace Jarvis.SDK.AI;

/// <summary>
/// Snapshot of an AI provider exposed by the JARVIS AI engine.
/// </summary>
public sealed class AIProviderInfo
{
    /// <summary>Stable provider id (e.g. <c>ollama</c>, <c>openai</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Human readable provider name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>True when the provider runs on this machine (works offline).</summary>
    public bool IsLocal { get; init; }

    /// <summary>Model ids currently offered by the provider, when discoverable.</summary>
    public IReadOnlyList<string> Models { get; init; } = Array.Empty<string>();

    /// <summary>True when the provider could be reached and answered a health check.</summary>
    public bool IsAvailable { get; init; }

    /// <summary>Error message when the health check failed, otherwise null.</summary>
    public string? Error { get; init; }
}
