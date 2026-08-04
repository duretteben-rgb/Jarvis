using System.Diagnostics;

namespace Jarvis.Runtime.ProcessManager;

/// <summary>
/// Starts, monitors and terminates child processes on behalf of JARVIS (plugin runtimes,
/// helpers, external tools, ...).
/// </summary>
public interface IProcessManager
{
    /// <summary>All child processes currently tracked by this manager.</summary>
    IReadOnlyList<ManagedProcess> Processes { get; }

    /// <summary>
    /// Starts a process with stdout/stderr captured and forwarded to the logger.
    /// </summary>
    Task<ManagedProcess> StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops a tracked process, then force-kills it if it does not exit in time.
    /// </summary>
    Task<bool> StopAsync(int processId, CancellationToken cancellationToken = default);
}
