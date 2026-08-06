namespace Jarvis.Plugins.System;

/// <summary>A single running process snapshot.</summary>
public sealed record ProcessInfo(int Pid, string Name, long MemoryBytes, double CpuSeconds, int Threads, DateTimeOffset? StartTime, string? Path)
{
    /// <inheritdoc />
    public override string ToString()
        => $"{Name} (pid {Pid}) | {(double)MemoryBytes / (1024 * 1024):F1} MB | cpu {CpuSeconds:F1}s | {Threads} threads | started {StartTime:HH:mm:ss}";
}

/// <summary>A file system entry produced by list/search operations.</summary>
public sealed record FileSystemEntry(string Path, bool IsDirectory, long Size, DateTimeOffset LastModified)
{
    /// <inheritdoc />
    public override string ToString()
        => IsDirectory ? $"[dir ] {Path}" : $"[file] {Path}  ({(double)Size / 1024:F1} KB, {LastModified:yyyy-MM-dd HH:mm})";
}

/// <summary>A disk volume snapshot.</summary>
public sealed record DiskInfo(string Name, long TotalBytes, long FreeBytes)
{
    /// <inheritdoc />
    public override string ToString()
        => $"{Name}  {((double)(TotalBytes - FreeBytes) / (1024 * 1024 * 1024)):F1}/{((double)TotalBytes / (1024 * 1024 * 1024)):F1} GB used  ({PercentUsed:F0}%)";

    private double PercentUsed => TotalBytes == 0 ? 0 : 100.0 * (TotalBytes - FreeBytes) / TotalBytes;
}

/// <summary>Combined hardware snapshot (CPU, memory, disks, OS, uptime).</summary>
public sealed record HardwareSnapshot(
    double CpuPercent,
    long MemoryTotalBytes,
    long MemoryAvailableBytes,
    IReadOnlyList<DiskInfo> Disks,
    string OperatingSystem,
    string HostName,
    TimeSpan Uptime)
{
    /// <inheritdoc />
    public override string ToString()
    {
        double memoryUsedPercent = MemoryTotalBytes == 0
            ? 0
            : 100.0 * (MemoryTotalBytes - MemoryAvailableBytes) / MemoryTotalBytes;

        return string.Join('\n',
            $"CPU        {CpuPercent:F1}%",
            $"Memory     {((double)(MemoryTotalBytes - MemoryAvailableBytes) / (1024 * 1024 * 1024)):F1}/{((double)MemoryTotalBytes / (1024 * 1024 * 1024)):F1} GB ({memoryUsedPercent:F0}%)",
            string.Join('\n', Disks.Select(disk => $"Disk       {disk}")),
            $"Uptime     {FormatUptime(Uptime)}",
            $"Host       {HostName}",
            $"OS         {OperatingSystem}");
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        return $"{(int)uptime.TotalMinutes}m";
    }
}
