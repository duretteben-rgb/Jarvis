namespace Jarvis.Memory.Configuration;

/// <summary>
/// Options bound from the <c>Embeddings</c> configuration section.
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// Embedding provider: <c>hash</c> (built-in deterministic fallback, always available) or
    /// <c>ollama</c> (local semantic embeddings via a running Ollama instance).
    /// </summary>
    public string Provider { get; set; } = "hash";

    /// <summary>Ollama model used when <see cref="Provider"/> is <c>ollama</c>.</summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>Base URL of the Ollama instance.</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";
}
