using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Jarvis.Runtime.ProcessManager;

/// <summary>
/// Default implementation of <see cref="IProcessManager"/>.
/// </summary>
public sealed class ProcessManager : IProcessManager, IAsyncDisposable
{
    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<ProcessManager> _logger;
    private readonly ConcurrentDictionary<int, ManagedProcess> _processes = new();

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ManagedProcess> Processes => _processes.Values.ToArray();

    /// <inheritdoc />
    public Task<ManagedProcess> StartAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                _logger.LogInformation("[{Pid}] {Line}", process.Id, args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                _logger.LogError("[{Pid}] {Line}", process.Id, args.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{startInfo.FileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var managed = new ManagedProcess(process.Id, process.ProcessName, startInfo.FileName, process);
        _processes[process.Id] = managed;

        _logger.LogInformation("Started process {Pid} ({Name}).", process.Id, process.ProcessName);
        return Task.FromResult(managed);
    }

    /// <inheritdoc />
    public async Task<bool> StopAsync(int processId, CancellationToken cancellationToken = default)
    {
        if (!_processes.TryRemove(processId, out ManagedProcess? managed))
        {
            _logger.LogDebug("Process {Pid} is not tracked.", processId);
            return false;
        }

        Process process = managed.Process;
        if (!process.HasExited)
        {
            if (!process.CloseMainWindow())
            {
                process.Kill(entireProcessTree: true);
            }
            else if (!process.WaitForExit((int)GracefulStopTimeout.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
            }
        }

        await process.WaitForExitAsync(cancellationToken);
        _logger.LogInformation("Stopped process {Pid} ({Name}).", process.Id, process.ProcessName);
        return true;
    }

    /// <summary>Stops every tracked process when the runtime shuts down.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (ManagedProcess managed in _processes.Values)
        {
            await StopAsync(managed.Id);
        }

        _processes.Clear();
    }
}
