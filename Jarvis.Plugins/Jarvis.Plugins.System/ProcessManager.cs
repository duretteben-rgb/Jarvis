using System.Diagnostics;
using System.Globalization;

namespace Jarvis.Plugins.System;

/// <summary>
/// Lists, inspects, starts and stops operating-system processes. All metric reads are guarded
/// against access-denied errors so the manager works without elevated privileges.
/// </summary>
public static class ProcessManager
{
    /// <summary>Lists running processes, optionally filtered by name/pid substring.</summary>
    public static IReadOnlyList<ProcessInfo> List(string? query, int limit = 100)
    {
        Process[] processes = Process.GetProcesses();
        var result = new List<ProcessInfo>(processes.Length);

        foreach (Process process in processes)
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                string name = process.ProcessName;
                int pid = process.Id;
                if (!string.IsNullOrWhiteSpace(query)
                    && !name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && pid.ToString(CultureInfo.InvariantCulture) != query)
                {
                    continue;
                }

                result.Add(ToInfo(process));
            }
            catch
            {
                // Process vanished or access denied; skip it.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result
            .OrderByDescending(process => process.MemoryBytes)
            .ThenBy(process => process.Name)
            .Take(limit)
            .ToList();
    }

    /// <summary>Returns a single process by id or name (first match).</summary>
    public static ProcessInfo? Find(string idOrName)
    {
        if (int.TryParse(idOrName, NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
        {
            try
            {
                using Process? process = Process.GetProcessById(pid);
                return process is null || process.HasExited ? null : ToInfo(process);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        return List(idOrName, limit: 1).FirstOrDefault();
    }

    /// <summary>Starts a process and returns its pid.</summary>
    public static int Start(string fileName, string? arguments = null, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory ?? string.Empty,
            UseShellExecute = false,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        return process.Id;
    }

    /// <summary>Stops a process by pid or name. Returns true when one was terminated.</summary>
    public static bool Stop(string idOrName)
    {
        if (int.TryParse(idOrName, NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                if (process is null || process.HasExited)
                {
                    return false;
                }

                return Terminate(process);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        IReadOnlyList<ProcessInfo> matches = List(idOrName, limit: 50);
        bool any = false;
        foreach (ProcessInfo match in matches)
        {
            try
            {
                using Process process = Process.GetProcessById(match.Pid);
                if (!process.HasExited)
                {
                    any |= Terminate(process);
                }
            }
            catch
            {
                // Ignore a single failing process and continue with the rest.
            }
        }

        return any;
    }

    private static bool Terminate(Process process)
    {
        try
        {
            process.CloseMainWindow();
        }
        catch
        {
            // No interactive window; fall through to kill.
        }

        if (!process.WaitForExit(2500))
        {
            process.Kill(entireProcessTree: true);
            return true;
        }

        return true;
    }

    private static ProcessInfo ToInfo(Process process)
    {
        long memoryBytes = 0;
        double cpuSeconds = 0;
        int threads = 0;
        DateTimeOffset? startTime = null;
        string? path = null;

        try { memoryBytes = process.WorkingSet64; } catch { /* access denied */ }
        try { cpuSeconds = process.TotalProcessorTime.TotalSeconds; } catch { /* access denied */ }
        try { threads = process.Threads.Count; } catch { /* access denied */ }
        try { startTime = process.StartTime; } catch { /* access denied */ }
        try { path = process.MainModule?.FileName; } catch { /* access denied */ }

        return new ProcessInfo(process.Id, process.ProcessName, memoryBytes, cpuSeconds, threads, startTime, path);
    }
}
