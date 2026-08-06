using System.Diagnostics;

namespace Jarvis.Plugins.System;

/// <summary>
/// Launches and stops applications by friendly name. Resolves a target against PATH and the
/// user's application directories before falling back to shell execution on desktop OSes.
/// </summary>
public static class AppLauncher
{
    /// <summary>Launches an application by name or path. Returns the process id.</summary>
    public static int Launch(string nameOrPath, string? arguments = null, string? workingDirectory = null)
    {
        string? target = ResolveTarget(nameOrPath);
        if (string.IsNullOrEmpty(target))
        {
            throw new FileNotFoundException($"Application not found: {nameOrPath}");
        }

        return ProcessManager.Start(target, arguments, workingDirectory);
    }

    /// <summary>Stops all processes whose name matches the given application name.</summary>
    public static bool Stop(string name)
    {
        return ProcessManager.Stop(name);
    }

    /// <summary>Returns true when an application matching the name is currently running.</summary>
    public static bool IsRunning(string name)
    {
        return ProcessManager.List(name, limit: 1).Count > 0;
    }

    private static string? ResolveTarget(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            return null;
        }

        // Absolute or relative path to an executable.
        if (File.Exists(nameOrPath))
        {
            return Path.GetFullPath(nameOrPath);
        }

        // Resolve against PATH (Unix-style exec lookup, works on Windows too).
        string? pathMatch = ResolveFromPath(nameOrPath);
        if (pathMatch is not null)
        {
            return pathMatch;
        }

        // On Windows, resolve Start Menu shortcuts / installed app commands.
        if (OperatingSystem.IsWindows())
        {
            string? windowsMatch = ResolveWindows(nameOrPath);
            if (windowsMatch is not null)
            {
                return windowsMatch;
            }
        }

        // Last resort: rely on the OS to resolve it (e.g. .app bundles on macOS,
        // registered associations on Windows). Headless hosts have no shell UI, so this
        // may legitimately fail; the caller surfaces the error.
        return nameOrPath;
    }

    private static string? ResolveFromPath(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string[] extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", ".com" }
            : new[] { string.Empty };

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory, name + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? ResolveWindows(string name)
    {
        string[] searchRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu"),
        };

        string[] extensions = { ".lnk", ".url", ".exe", ".cmd", ".bat" };

        foreach (string root in searchRoots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (string extension in extensions)
            {
                string pattern = "*" + extension;
                try
                {
                    IEnumerable<string> matches = Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                        .Where(file => Path.GetFileNameWithoutExtension(file).Contains(name, StringComparison.OrdinalIgnoreCase))
                        .Take(1);

                    foreach (string match in matches)
                    {
                        return match;
                    }
                }
                catch
                {
                    // Inaccessible Start Menu sub-tree; continue searching others.
                }
            }
        }

        return null;
    }
}
