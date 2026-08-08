namespace Jarvis.SDK.AI;

/// <summary>
/// An image attachment on a chat message. Providers that support multimodal input encode the
/// image as a base64 data URI (OpenAI-compatible) or a base64 byte array (Ollama) and pass it
/// to a vision-capable model.
/// </summary>
/// <param name="MimeType">Image content type, e.g. <c>image/png</c> or <c>image/jpeg</c>.</param>
/// <param name="Base64Data">Raw image bytes encoded as base64.</param>
public sealed record ChatImage(string MimeType, string Base64Data)
{
    /// <summary>Builds the <c>data:</c> URI sent to OpenAI-compatible providers.</summary>
    public string ToDataUri() => $"data:{MimeType};base64,{Base64Data}";
}
