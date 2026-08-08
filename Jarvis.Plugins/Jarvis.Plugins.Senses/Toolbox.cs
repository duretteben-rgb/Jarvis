using System.Diagnostics;
using System.Text;

namespace Jarvis.Plugins.Senses;

/// <summary>
/// Small helpers for locating external tools on the PATH and running short-lived commands.
/// Every tool is optional: callers degrade gracefully when a tool is missing.
/// </summary>
internal static class Toolbox
{
    /// <summary>Returns the absolute path of a tool found on the PATH, or null.</summary>
    public static string? FindTool(string tool)
    {
        string? pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (string directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, tool);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe"))
            {
                return candidate + ".exe";
            }
        }

        return null;
    }

    /// <summary>Runs a command with arguments and a timeout, capturing combined output.</summary>
    public static async Task<(int ExitCode, string Output)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        int timeoutSeconds = 60,
        string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => Append(eventArgs.Data, output);
        process.ErrorDataReceived += (_, eventArgs) => Append(eventArgs.Data, output);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return (process.ExitCode, output.ToString().Trim());
    }

    private static void Append(string? line, StringBuilder builder)
    {
        if (line is null)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(line);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch
        {
            // Best effort; the process tree may already be gone.
        }
    }
}
