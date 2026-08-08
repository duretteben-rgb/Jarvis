namespace Jarvis.Plugins.Senses;

/// <summary>Outcome of a text-to-speech request.</summary>
public sealed record SpeechResult(bool Synthesized, string Text, string? AudioPath, string? Detail)
{
    /// <inheritdoc />
    public override string ToString()
    {
        if (Synthesized)
        {
            string location = string.IsNullOrWhiteSpace(AudioPath) ? string.Empty : $" -> {AudioPath}";
            return $"Spoken ({Text.Length} chars){location}";
        }

        return $"Speech synthesis unavailable: {Detail}";
    }
}

/// <summary>Outcome of a speech-to-text request.</summary>
public sealed record TranscriptionResult(bool Transcribed, string? Text, string? Detail)
{
    /// <inheritdoc />
    public override string ToString()
        => Transcribed ? $"Transcript: {Text}" : $"Transcription unavailable: {Detail}";
}

/// <summary>Outcome of an image analysis request.</summary>
public sealed record AnalysisResult(string Description, string Model, string Provider, string ImageSource)
{
    /// <inheritdoc />
    public override string ToString()
        => $"Analysis ({Model} via {Provider}) of {ImageSource}:\n{Description}";
}

/// <summary>Outcome of a screen capture request.</summary>
public sealed record CaptureResult(bool Captured, string? ImagePath, string Detail)
{
    /// <inheritdoc />
    public override string ToString()
    {
        if (Captured)
        {
            string location = string.IsNullOrWhiteSpace(ImagePath) ? string.Empty : $" -> {ImagePath}";
            return $"Screen captured{location}";
        }

        return $"Screen capture unavailable: {Detail}";
    }
}
