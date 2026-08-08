using System.Diagnostics;
using System.Text;

namespace Jarvis.Plugins.Developer;

/// <summary>
/// Runs external toolchain commands (dotnet, node, npm, python) with a timeout and captures
/// stdout + stderr. Executables are resolved against PATH, <c>DOTNET_ROOT</c> and well-known
/// install locations so the plugin works on hosts where those tools are not on PATH.
/// </summary>
public static class CommandRunner
{
    private const int MaxOutputChars = 32 * 1024;

    /// <summary>Runs a command line, returning its exit code and (truncated) combined output.</summary>
    public static async Task<ProcessOutput> RunAsync(
        string commandLine,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        (string executable, string arguments) = SplitCommand(commandLine);
        string resolved = ResolveExecutable(executable)
            ?? throw new FileNotFoundException($"Executable not found: {executable}");

        var startInfo = new ProcessStartInfo
        {
            FileName = resolved,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach ((string key, string? value) in GetToolchainEnvironment())
        {
            startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        bool truncated = false;
        process.OutputDataReceived += (_, eventArgs) => Append(eventArgs.Data, output, ref truncated);
        process.ErrorDataReceived += (_, eventArgs) => Append(eventArgs.Data, output, ref truncated);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process already exited.
            }

            await process.WaitForExitAsync();
            return new ProcessOutput(-1, $"{output}\n[tool command timed out after {timeout.TotalSeconds:0}s]".Trim(), commandLine);
        }

        return new ProcessOutput(process.ExitCode, output.ToString().Trim(), commandLine);
    }

    private static void Append(string? line, StringBuilder builder, ref bool truncated)
    {
        if (line is null)
        {
            return;
        }

        if (builder.Length >= MaxOutputChars)
        {
            if (!truncated)
            {
                truncated = true;
                builder.Append("\n[output truncated]");
            }

            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        int remaining = MaxOutputChars - builder.Length;
        builder.Append(line, 0, Math.Min(line.Length, Math.Max(0, remaining)));
    }

    private static (string Executable, string Arguments) SplitCommand(string commandLine)
    {
        string trimmed = commandLine.Trim();
        if (trimmed.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        int end = trimmed[0] == '"'
            ? trimmed.IndexOf('"', 1) + 1
            : trimmed.IndexOf(' ');

        if (end <= 0)
        {
            return (trimmed, string.Empty);
        }

        return (trimmed[..end].Trim('"'), trimmed[end..].Trim());
    }

    private static string? ResolveExecutable(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        if (executable.Contains(Path.DirectorySeparatorChar) && File.Exists(executable))
        {
            return Path.GetFullPath(executable);
        }

        // dotnet special case: prefer DOTNET_ROOT or the well-known SDK location.
        if (string.Equals(executable, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrWhiteSpace(dotnetRoot))
            {
                string candidate = Path.Combine(dotnetRoot, "dotnet");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string wellKnown = "/usr/share/dotnet/dotnet";
            if (File.Exists(wellKnown))
            {
                return wellKnown;
            }
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            string[] extensions = OperatingSystem.IsWindows()
                ? new[] { ".exe", ".cmd", ".bat", ".com" }
                : new[] { string.Empty };

            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (string extension in extensions)
                {
                    string candidate = Path.Combine(directory, executable + extension);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> GetToolchainEnvironment()
    {
        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
        };

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            environment["DOTNET_ROOT"] = dotnetRoot;
        }

        return environment;
    }
}
