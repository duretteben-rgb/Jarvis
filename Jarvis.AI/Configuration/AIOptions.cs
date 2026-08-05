namespace Jarvis.AI.Configuration;

/// <summary>
/// Options bound from the <c>AI</c> configuration section: provider endpoints, the list of
/// routable models and the routing policy.
/// </summary>
public sealed class AIOptions
{
    /// <summary>Whether the AI engine is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Local Ollama provider settings.</summary>
    public OllamaOptions Ollama { get; set; } = new();

    /// <summary>OpenAI-compatible cloud provider settings (works with Groq, OpenAI, ...).</summary>
    public OpenAICompatibleOptions OpenAI { get; set; } = new();

    /// <summary>Models available to the router. Each entry points at a provider + model id.</summary>
    public List<ModelDefinition> Models { get; set; } = new();

    /// <summary>Routing policy.</summary>
    public RoutingOptions Routing { get; set; } = new();

    /// <summary>Conversation context management settings.</summary>
    public ContextOptions Context { get; set; } = new();
}

/// <summary>Conversation context management settings.</summary>
public sealed class ContextOptions
{
    /// <summary>Maximum token budget (estimated) kept per conversation session.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Maximum number of concurrent sessions before the oldest is evicted.</summary>
    public int MaxSessions { get; set; } = 100;
}

/// <summary>Settings for the local Ollama provider.</summary>
public sealed class OllamaOptions
{
    /// <summary>Whether the provider is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Base URL of the Ollama instance (runs offline on this machine).</summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Default model used when no routed model is configured.</summary>
    public string Model { get; set; } = "llama3.2";
}

/// <summary>Settings for the OpenAI-compatible cloud provider.</summary>
public sealed class OpenAICompatibleOptions
{
    /// <summary>Whether the provider is enabled. Off by default until an API key is provided.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Display name shown in the UI.</summary>
    public string DisplayName { get; set; } = "OpenAI compatible (Groq / OpenAI)";

    /// <summary>Base URL of an OpenAI-compatible chat completions API.</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    /// <summary>
    /// API key. Leave empty to fall back to the <c>JARVIS_OPENAI_API_KEY</c> environment
    /// variable. Keys are never required for local providers.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Default model used when no routed model is configured.</summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";
}

/// <summary>Routing policy used by the model router.</summary>
public sealed class RoutingOptions
{
    /// <summary>
    /// When true, available local models are preferred over cloud models so requests work
    /// offline. The router still falls back to the cloud when no local model is available.
    /// </summary>
    public bool PreferLocal { get; set; } = true;

    /// <summary>How long a health-check result stays valid, in seconds.</summary>
    public int AvailabilityTtlSeconds { get; set; } = 15;

    /// <summary>Consecutive failures after which a provider enters a cooldown.</summary>
    public int ConsecutiveFailuresBeforeCooldown { get; set; } = 3;

    /// <summary>Cooldown duration in seconds after too many consecutive failures.</summary>
    public int CooldownSeconds { get; set; } = 30;
}
