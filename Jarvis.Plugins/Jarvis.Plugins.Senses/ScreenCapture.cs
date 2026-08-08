namespace Jarvis.Plugins.Senses;

/// <summary>Outcome of a screen capture attempt.</summary>
internal sealed record CaptureOutcome(string? ImagePath, string Detail)
{
    public bool Captured => ImagePath is not null;
}

/// <summary>
/// Captures the desktop to a PNG file using whatever capture tool is available
/// (scrot, ImageMagick's import, xwd, or ffmpeg x11grab). On headless hosts the capture
/// degrades to a detailed explanation instead of failing.
/// </summary>
internal static class ScreenCapture
{
    /// <summary>
    /// True when a graphical session appears to be available. Windows always has a desktop
    /// session; Linux/macOS expose one through the X11 <c>DISPLAY</c> (or Wayland) variable.
    /// </summary>
    public static bool HasDisplay()
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"));
    }

    /// <summary>Captures the screen into <paramref name="outputDirectory"/>.</summary>
    public static async Task<CaptureOutcome> CaptureAsync(
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (!HasDisplay())
        {
            return new CaptureOutcome(
                null,
                OperatingSystem.IsWindows()
                    ? "Screen capture needs a desktop session, but none could be detected."
                    : "Screen capture needs a graphical session, but DISPLAY is not set (headless environment).");
        }

        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, $"screen-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png");

        (string Executable, string[] Arguments)[] strategies =
        {
            ("scrot", new[] { outputPath }),
            ("import", new[] { "-window", "root", outputPath }),
            ("xwd", new[] { "-root", "-out", outputPath }),
        };

        foreach ((string executable, string[] arguments) in strategies)
        {
            if (Toolbox.FindTool(executable) is null)
            {
                continue;
            }

            (int exitCode, _) = await Toolbox.RunAsync(
                executable,
                arguments,
                cancellationToken,
                timeoutSeconds: 30);

            if (exitCode == 0 && File.Exists(outputPath))
            {
                return new CaptureOutcome(outputPath, $"Captured via {executable}.");
            }
        }

        return new CaptureOutcome(
            null,
            "No screen capture tool (scrot, import, xwd) is available or produced a capture. Install ImageMagick (import) or scrot to enable screen capture.");
    }
}
