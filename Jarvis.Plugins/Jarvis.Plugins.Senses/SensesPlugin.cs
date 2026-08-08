using Jarvis.SDK.AI;
using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.Senses;

/// <summary>
/// Voice and vision plugin for JARVIS OS. Speaks text aloud, transcribes audio, analyzes
/// images with an AI vision model and captures the screen. Every capability degrades
/// gracefully when the required local tool or display is unavailable, so the plugin remains
/// functional on headless or minimal hosts.
/// </summary>
public sealed class SensesPlugin : JarvisPluginBase
{
    public SensesPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.senses",
            Name = "Senses (Voice & Vision)",
            Version = "1.0.0",
            Description = "Voice synthesis, transcription and computer vision.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.AI, PermissionIds.Files, PermissionIds.Network },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("voice.speak", "Speaks text aloud (local TTS); returns the text when no synthesizer is installed."),
        new PluginCommand("voice.transcribe", "Transcribes an audio file to text (requires the whisper CLI)."),
        new PluginCommand("vision.analyze", "Describes or analyzes an image with an AI vision model."),
        new PluginCommand("vision.screen", "Captures the screen and optionally analyzes it (headless-degraded)."),
    };

    private string MediaRoot => Context.Host.Configuration.GetValue("Jarvis:Senses:Media")
        ?? Path.Combine(AppContext.BaseDirectory, "media");

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation(
            "{Plugin} ({Version}) ready; TTS {Tts}, screen capture {Capture}.",
            Manifest.Id,
            Manifest.Version,
            VoiceSynthesis.IsAvailable() ? "available" : "unavailable (browser fallback)",
            ScreenCapture.HasDisplay() ? "available" : "unavailable (headless)");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "voice.speak":
                return await SpeakAsync(parameters, cancellationToken);

            case "voice.transcribe":
                return await TranscribeAsync(parameters, cancellationToken);

            case "vision.analyze":
                return await AnalyzeAsync(parameters, cancellationToken);

            case "vision.screen":
                return await ScreenAsync(parameters, cancellationToken);

            default:
                return await base.ExecuteCommandAsync(command, parameters, cancellationToken);
        }
    }

    private async Task<object?> SpeakAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        string text = Required(parameters, "text");
        string? voice = GetString(parameters, "voice");

        string? audioPath = await VoiceSynthesis.SynthesizeToWavAsync(text, voice, MediaRoot, cancellationToken);
        if (audioPath is null)
        {
            string detail = VoiceSynthesis.IsAvailable()
                ? "The speech synthesizer failed to produce audio."
                : "Local speech synthesis requires espeak-ng or espeak; neither is installed. The HUB falls back to browser speech.";
            Context.Logger.LogInformation("voice.speak degraded: {Detail}", detail);
            return new SpeechResult(false, text, null, detail);
        }

        Context.Logger.LogInformation("Spoke {Chars} chars to {Path}.", text.Length, audioPath);
        return new SpeechResult(true, text, audioPath, "Rendered with espeak.");
    }

    private async Task<object?> TranscribeAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        string file = Required(parameters, "file");
        string fullPath = Path.GetFullPath(file);
        if (!File.Exists(fullPath))
        {
            throw new PluginException(Manifest.Id, $"Audio file not found: {fullPath}");
        }

        string? whisper = Toolbox.FindTool("whisper");
        if (whisper is null)
        {
            throw new PluginException(
                Manifest.Id,
                "Transcription requires the OpenAI 'whisper' CLI on the PATH; it is not installed in this environment. Install whisper (pip install openai-whisper) to enable speech-to-text.");
        }

        Directory.CreateDirectory(MediaRoot);
        var arguments = new List<string> { fullPath, "--output_format", "txt", "--output_dir", MediaRoot };
        string? model = GetString(parameters, "model");
        if (!string.IsNullOrWhiteSpace(model))
        {
            arguments.Add("--model");
            arguments.Add(model);
        }

        (int exitCode, string output) = await Toolbox.RunAsync(whisper, arguments, cancellationToken, timeoutSeconds: 300);
        if (exitCode != 0)
        {
            throw new PluginException(Manifest.Id, $"Whisper failed: {output}");
        }

        string transcriptPath = Path.Combine(MediaRoot, Path.GetFileNameWithoutExtension(fullPath) + ".txt");
        string transcript = File.Exists(transcriptPath) ? File.ReadAllText(transcriptPath).Trim() : output;
        return new TranscriptionResult(true, transcript, $"Transcribed {Path.GetFileName(fullPath)}.");
    }

    private async Task<object?> AnalyzeAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        string imageSource = Required(parameters, "image");
        string prompt = GetString(parameters, "prompt") ?? "Describe this image in detail.";
        string? model = GetString(parameters, "model");

        (byte[] bytes, string mimeType) = await LoadImageAsync(imageSource, cancellationToken);
        var ai = Context.Host.Services.GetService(typeof(IAIService)) as IAIService;
        if (ai is null)
        {
            throw new PluginException(Manifest.Id, "The AI engine is not available in this host.");
        }

        var request = new ChatRequest
        {
            Model = model,
            TaskKind = TaskKind.Complex,
            PreferLocal = false,
            MaxTokens = 1024,
            Messages = new[]
            {
                ChatMessage.System("You are JARVIS's vision. Answer using only the supplied image."),
                ChatMessage.UserWithImage(prompt, new ChatImage(mimeType, Convert.ToBase64String(bytes))),
            },
        };

        ChatResponse response = await ai.ChatAsync(request, cancellationToken);
        Context.Logger.LogInformation(
            "Analyzed image ({Bytes} bytes, {Mime}) via {Model} ({Provider}).",
            bytes.Length,
            mimeType,
            response.Model,
            response.Provider);
        return new AnalysisResult(response.Message.Content, response.Model, response.Provider, imageSource);
    }

    private async Task<object?> ScreenAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        CaptureOutcome outcome = await ScreenCapture.CaptureAsync(MediaRoot, cancellationToken);
        if (outcome.ImagePath is null)
        {
            Context.Logger.LogInformation("vision.screen degraded: {Detail}", outcome.Detail);
            return new CaptureResult(false, null, outcome.Detail);
        }

        string? prompt = GetString(parameters, "prompt");
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            var analyzeParameters = new Dictionary<string, object?>
            {
                ["image"] = outcome.ImagePath,
                ["prompt"] = prompt,
            };
            object? analysis = await AnalyzeAsync(analyzeParameters, cancellationToken);
            return new { captured = true, image = outcome.ImagePath, capture = outcome.Detail, analysis };
        }

        return new CaptureResult(true, outcome.ImagePath, outcome.Detail);
    }

    private async Task<(byte[] Bytes, string MimeType)> LoadImageAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            int comma = source.IndexOf(',');
            if (comma < 0)
            {
                throw new PluginException(Manifest.Id, "Malformed data URI for image.");
            }

            string header = source[5..comma];
            string mimeType = header.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
            return (Convert.FromBase64String(source[(comma + 1)..]), mimeType);
        }

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            byte[] bytes = await client.GetByteArrayAsync(source, cancellationToken);
            return (bytes, MimeForPath(source));
        }

        string fullPath = Path.GetFullPath(source);
        if (!File.Exists(fullPath))
        {
            throw new PluginException(Manifest.Id, $"Image file not found: {fullPath}");
        }

        return (await File.ReadAllBytesAsync(fullPath, cancellationToken), MimeForPath(fullPath));
    }

    private static string MimeForPath(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" => "image/tiff",
            _ => "image/png",
        };

    private string Required(IReadOnlyDictionary<string, object?>? parameters, string key)
        => GetString(parameters, key)
            ?? throw new PluginException(Manifest.Id, $"Parameter '{key}' is required.");

    private static string? GetString(IReadOnlyDictionary<string, object?>? parameters, string key)
        => parameters?.TryGetValue(key, out object? value) == true ? value as string : null;
}
