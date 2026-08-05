using System.Text.Json.Serialization;

namespace Jarvis.API.Contracts;

/// <summary>
/// Body of <c>POST /api/ai/chat</c> and <c>POST /api/ai/chat/stream</c>.
/// </summary>
public sealed class ChatApiRequest
{
    /// <summary>Optional explicit model id (see <c>GET /api/ai/models</c>).</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Task kind used by the router: simple, complex, reasoning, coding, summarization.</summary>
    [JsonPropertyName("taskKind")]
    public string? TaskKind { get; set; }

    /// <summary>Conversation session id; server keeps history under it.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    /// <summary>Optional system prompt.</summary>
    [JsonPropertyName("systemPrompt")]
    public string? SystemPrompt { get; set; }

    /// <summary>Optional sampling temperature.</summary>
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    /// <summary>Optional maximum tokens to generate.</summary>
    [JsonPropertyName("maxTokens")]
    public int? MaxTokens { get; set; }

    /// <summary>Prefer a local (offline) model when available.</summary>
    [JsonPropertyName("preferLocal")]
    public bool? PreferLocal { get; set; }

    /// <summary>Explicit message list. When omitted, <see cref="Prompt"/> is used.</summary>
    [JsonPropertyName("messages")]
    public List<ChatMessageDto>? Messages { get; set; }

    /// <summary>Shortcut for a single user prompt.</summary>
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }
}

/// <summary>A chat message in the API payload.</summary>
public sealed class ChatMessageDto
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
