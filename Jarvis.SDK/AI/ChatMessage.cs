namespace Jarvis.SDK.AI;

/// <summary>
/// A single message exchanged with an AI model.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>Creator of the message.</summary>
    public required string Role { get; init; }

    /// <summary>Text content of the message.</summary>
    public required string Content { get; init; }

    /// <summary>Optional image attachment for multimodal (vision) models.</summary>
    public ChatImage? Image { get; init; }

    /// <summary>Creates a user message.</summary>
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };

    /// <summary>Creates a user message with an image attachment.</summary>
    public static ChatMessage UserWithImage(string content, ChatImage image)
        => new() { Role = "user", Content = content, Image = image };

    /// <summary>Creates an assistant message.</summary>
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };

    /// <summary>Creates a system message.</summary>
    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
}
