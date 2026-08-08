namespace Jarvis.Plugins.Senses;

/// <summary>
/// Local text-to-speech backed by espeak-ng (or espeak). When neither is installed the caller
/// receives null plus an explanation so the HUB can fall back to browser speech synthesis.
/// </summary>
internal static class VoiceSynthesis
{
    /// <summary>True when a local speech synthesizer can be found on the PATH.</summary>
    public static bool IsAvailable() => Toolbox.FindTool("espeak-ng") is not null || Toolbox.FindTool("espeak") is not null;

    /// <summary>
    /// Renders <paramref name="text"/> to a WAV file under <paramref name="outputDirectory"/>.
    /// Returns the file path, or null (with <paramref name="detail"/> set) when no synthesizer
    /// is available or the synthesis failed.
    /// </summary>
    public static async Task<string?> SynthesizeToWavAsync(
        string text,
        string? voice,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        string? espeak = Toolbox.FindTool("espeak-ng") ?? Toolbox.FindTool("espeak");
        if (espeak is null)
        {
            return null;
        }

        Directory.CreateDirectory(outputDirectory);
        string safeName = Sanitize(text);
        string outputPath = Path.Combine(outputDirectory, $"{safeName}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.wav");

        var arguments = new List<string> { "-w", outputPath };
        if (!string.IsNullOrWhiteSpace(voice))
        {
            arguments.Add("-v");
            arguments.Add(voice);
        }

        arguments.Add(text);

        (int exitCode, string output) = await Toolbox.RunAsync(espeak, arguments, cancellationToken, timeoutSeconds: 30);
        if (exitCode != 0 || !File.Exists(outputPath))
        {
            return null;
        }

        return outputPath;
    }

    private static string Sanitize(string text)
    {
        string safe = new string(text
            .Where(char.IsLetterOrDigit)
            .Take(24)
            .ToArray());
        return safe.Length == 0 ? "speech" : safe;
    }
}
