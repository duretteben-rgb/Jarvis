using System.Diagnostics;

namespace Jarvis.Runtime.ProcessManager;

/// <summary>
/// Descriptor of a child process launched and tracked by the <see cref="IProcessManager"/>.
/// </summary>
public sealed class ManagedProcess
{
    internal ManagedProcess(int id, string name, string fileName, Process process)
    {
        Id = id;
        Name = name;
        FileName = fileName;
        Process = process;
        StartedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Operating system process id.</summary>
    public int Id { get; }

    /// <summary>Process name.</summary>
    public string Name { get; }

    /// <summary>Executable that was launched.</summary>
    public string FileName { get; }

    /// <summary>UTC time the process was started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Whether the process has exited.</summary>
    public bool HasExited => Process.HasExited;

    /// <summary>Exit code when the process has exited, otherwise null.</summary>
    public int? ExitCode => Process.HasExited ? Process.ExitCode : null;

    internal Process Process { get; }
}
