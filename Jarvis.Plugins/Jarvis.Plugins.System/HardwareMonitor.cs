using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Jarvis.Plugins.System;

/// <summary>
/// Cross-platform CPU/memory/disk/uptime monitor. Uses /proc on Linux and P/Invoke on Windows,
/// degrading gracefully to no-op snapshots when the OS is unsupported.
/// </summary>
public static class HardwareMonitor
{
    private static long _lastCpuTotal;
    private static long _lastCpuIdle;

    static HardwareMonitor()
    {
        // Seed the CPU baseline so the first percent reading is meaningful.
        (long total, long idle) = ReadCpuTimes();
        _lastCpuTotal = total;
        _lastCpuIdle = idle;
    }

    /// <summary>Captures a combined hardware snapshot.</summary>
    public static HardwareSnapshot Snapshot()
    {
        (double cpuPercent, long memoryTotal, long memoryAvailable) = ReadMemoryAndCpu();
        IReadOnlyList<DiskInfo> disks = ReadDisks();
        string os = ReadOsVersion();
        string hostName = ReadHostName();
        TimeSpan uptime = ReadUptime();

        return new HardwareSnapshot(cpuPercent, memoryTotal, memoryAvailable, disks, os, hostName, uptime);
    }

    /// <summary>CPU usage percentage over the elapsed interval since the last read.</summary>
    public static double ReadCpuPercent()
    {
        (double cpu, _, _) = ReadMemoryAndCpu();
        return cpu;
    }

    private static (double Cpu, long Total, long Available) ReadMemoryAndCpu()
    {
        if (OperatingSystem.IsLinux())
        {
            return ReadLinux();
        }

        if (OperatingSystem.IsWindows())
        {
            return ReadWindows();
        }

        return (0, 0, 0);
    }

    private static (double Cpu, long Total, long Available) ReadLinux()
    {
        long totalBytes = 0;
        long availableBytes = 0;

        try
        {
            foreach (string line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal) && TryParseKb(line, out long memTotal))
                {
                    totalBytes = memTotal;
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal) && TryParseKb(line, out long available))
                {
                    availableBytes = available;
                }
            }
        }
        catch
        {
            // /proc/meminfo unavailable; metrics default to zero.
        }

        (long total, long idle) = ReadCpuTimes();
        double percent = 0;
        if (total > 0 && _lastCpuTotal > 0)
        {
            long totalDelta = total - _lastCpuTotal;
            long idleDelta = idle - _lastCpuIdle;
            if (totalDelta > 0)
            {
                percent = 100.0 * (1.0 - (double)idleDelta / totalDelta);
            }
        }

        _lastCpuTotal = total;
        _lastCpuIdle = idle;

        return (Math.Clamp(percent, 0, 100), totalBytes, availableBytes);
    }

    private static (double Cpu, long Total, long Available) ReadWindows()
    {
        long totalBytes = 0;
        long availableBytes = 0;
        double percent = 0;

        try
        {
            var status = new MemoryStatusEx();
            if (GlobalMemoryStatusEx(status))
            {
                totalBytes = (long)status.ullTotalPhys;
                availableBytes = (long)status.ullAvailPhys;
            }
        }
        catch
        {
            // Memory read failed; use defaults.
        }

        if (!GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
        {
            return (0, totalBytes, availableBytes);
        }

        long totalTime = idleTime + kernelTime + userTime;
        if (totalTime <= _lastCpuTotal)
        {
            return (0, totalBytes, availableBytes);
        }

        long totalDelta = totalTime - _lastCpuTotal;
        long idleDelta = idleTime - _lastCpuIdle;
        if (totalDelta > 0)
        {
            percent = 100.0 * (1.0 - (double)idleDelta / totalDelta);
        }

        _lastCpuTotal = totalTime;
        _lastCpuIdle = idleTime;

        return (Math.Clamp(percent, 0, 100), totalBytes, availableBytes);
    }

    private static IReadOnlyList<DiskInfo> ReadDisks()
    {
        var disks = new List<DiskInfo>();

        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady)
                    {
                        continue;
                    }

                    disks.Add(new DiskInfo(drive.Name, drive.TotalSize, drive.TotalFreeSpace));
                }
                catch
                {
                    // Drive not ready / access denied; skip it.
                }
            }
        }
        catch
        {
            // No drive enumeration support.
        }

        return disks.OrderBy(disk => disk.Name).ToList();
    }

    private static (long Total, long Idle) ReadCpuTimes()
    {
        if (!OperatingSystem.IsLinux())
        {
            return (0, 0);
        }

        try
        {
            string? first = File.ReadLines("/proc/stat").FirstOrDefault();
            if (first is null || !first.StartsWith("cpu ", StringComparison.Ordinal))
            {
                return (0, 0);
            }

            string[] parts = first.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return (0, 0);
            }

            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
            long irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
            long softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;

            long total = user + nice + system + idle + iowait + irq + softirq;
            return (total, idle + iowait);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static string ReadOsVersion()
        => RuntimeInformation.OSDescription;

    private static string ReadHostName()
    {
        try
        {
            return Environment.MachineName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static TimeSpan ReadUptime()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                string? line = File.ReadLines("/proc/uptime").FirstOrDefault();
                if (line is not null)
                {
                    string seconds = line.Split(' ')[0];
                    if (double.TryParse(seconds, CultureInfo.InvariantCulture, out double value))
                    {
                        return TimeSpan.FromSeconds(value);
                    }
                }
            }
            catch
            {
                // Fall through to Environment.TickCount64.
            }
        }

        return TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    private static bool TryParseKb(string line, out long value)
    {
        value = 0;
        int colon = line.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        string number = line[(colon + 1)..].Trim();
        int space = number.IndexOf(' ');
        if (space > 0)
        {
            number = number[..space];
        }

        if (!long.TryParse(number, out long kb))
        {
            return false;
        }

        value = kb * 1024;
        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(MemoryStatusEx lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out long lpIdleTime,
        out long lpKernelTime,
        out long lpUserTime);
}
