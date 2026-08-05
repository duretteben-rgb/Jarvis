namespace Jarvis.AI.Configuration;

/// <summary>
/// A routable model entry. Models are the units the router chooses between; each one points at
/// a provider and that provider's model id.
/// </summary>
public sealed class ModelDefinition
{
    /// <summary>Stable id used to reference this model (e.g. <c>groq-llama</c>, <c>local-mistral</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Provider id that serves this model (<c>ollama</c> or <c>openai</c>).</summary>
    public required string Provider { get; init; }

    /// <summary>Model id understood by the provider.</summary>
    public required string Model { get; init; }

    /// <summary>Display name shown in the UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Used as the fallback when no model matches the task kind.</summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Capabilities used to match task kinds: <c>fast</c>, <c>powerful</c>, <c>reasoning</c>,
    /// <c>coding</c>, <c>summarize</c>, <c>offline</c>.
    /// </summary>
    public List<string> Tags { get; init; } = new();
}
