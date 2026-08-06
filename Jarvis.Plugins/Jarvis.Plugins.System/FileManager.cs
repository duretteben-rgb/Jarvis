using System.Text;

namespace Jarvis.Plugins.System;

/// <summary>
/// Safe file-system operations for JARVIS: list, read, write, copy, move, inspect and search.
/// Reads are bounded and searches are depth/result limited so the manager stays responsive.
/// </summary>
public static class FileManager
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Lists the entries of a directory, optionally filtered by a glob pattern.</summary>
    public static IReadOnlyList<FileSystemEntry> List(string path, string? pattern = null)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
        }

        var entries = new List<FileSystemEntry>();

        foreach (string directory in Directory.EnumerateDirectories(fullPath))
        {
            if (Matches(directory, pattern))
            {
                entries.Add(ToEntry(directory, isDirectory: true));
            }
        }

        foreach (string file in Directory.EnumerateFiles(fullPath))
        {
            if (Matches(file, pattern))
            {
                entries.Add(ToEntry(file, isDirectory: false));
            }
        }

        return entries
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.Path)
            .ToList();
    }

    /// <summary>Reads a text file, returning the first <c>maxBytes</c> bytes plus a truncation flag.</summary>
    public static (string Content, bool Truncated) Read(string path, int maxBytes = 64 * 1024)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}");
        }

        using FileStream stream = File.OpenRead(fullPath);
        int bytesToRead = Math.Min(maxBytes, (int)Math.Min(stream.Length, int.MaxValue));
        byte[] buffer = new byte[bytesToRead];
        int read = stream.Read(buffer, 0, bytesToRead);
        bool truncated = stream.Length > bytesToRead;
        return (Encoding.UTF8.GetString(buffer, 0, read), truncated);
    }

    /// <summary>Writes a text file, creating parent directories as needed.</summary>
    public static void Write(string path, string content, bool append = false)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (append)
        {
            File.AppendAllText(fullPath, content, Utf8NoBom);
        }
        else
        {
            File.WriteAllText(fullPath, content, Utf8NoBom);
        }
    }

    /// <summary>Copies a file or directory tree.</summary>
    public static void Copy(string source, string destination)
    {
        string sourcePath = Path.GetFullPath(source);
        string destinationPath = Path.GetFullPath(destination);

        if (Directory.Exists(sourcePath))
        {
            CopyDirectory(sourcePath, destinationPath);
            return;
        }

        if (File.Exists(sourcePath))
        {
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            return;
        }

        throw new FileNotFoundException($"Source not found: {sourcePath}");
    }

    /// <summary>Moves a file or directory.</summary>
    public static void Move(string source, string destination)
    {
        string sourcePath = Path.GetFullPath(source);
        string destinationPath = Path.GetFullPath(destination);

        string? destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
            return;
        }

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath, overwrite: true);
            return;
        }

        throw new FileNotFoundException($"Source not found: {sourcePath}");
    }

    /// <summary>Returns file/directory metadata.</summary>
    public static FileSystemEntry Info(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return ToEntry(fullPath, isDirectory: true);
        }

        if (File.Exists(fullPath))
        {
            return ToEntry(fullPath, isDirectory: false);
        }

        throw new FileNotFoundException($"Path not found: {fullPath}");
    }

    /// <summary>
    /// Recursively searches a directory for names matching a glob pattern, bounded by depth and
    /// result count. Inaccessible sub-directories are skipped.
    /// </summary>
    public static IReadOnlyList<FileSystemEntry> Search(string root, string pattern = "*", int maxResults = 50, int maxDepth = 6)
    {
        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Directory not found: {fullRoot}");
        }

        var results = new List<FileSystemEntry>();
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((fullRoot, 0));

        while (pending.Count > 0 && results.Count < maxResults)
        {
            (string current, int depth) = pending.Pop();
            IEnumerable<string> directories;
            IEnumerable<string> files;

            try
            {
                directories = Directory.EnumerateDirectories(current);
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (string file in files)
            {
                if (results.Count >= maxResults)
                {
                    break;
                }

                if (Matches(file, pattern))
                {
                    results.Add(ToEntry(file, isDirectory: false));
                }
            }

            if (depth < maxDepth)
            {
                foreach (string directory in directories)
                {
                    if (Matches(directory, pattern))
                    {
                        results.Add(ToEntry(directory, isDirectory: true));
                    }

                    pending.Push((directory, depth + 1));
                }
            }
        }

        return results.OrderBy(entry => entry.Path).ToList();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            string? targetDirectory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(file, target, overwrite: true);
        }
    }

    private static bool Matches(string fullPath, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern == "*")
        {
            return true;
        }

        return MatchesName(Path.GetFileName(fullPath), pattern);
    }

    private static bool MatchesName(string name, string pattern)
        => pattern.Contains('*') || pattern.Contains('?')
            ? MatchGlob(name, pattern)
            : name.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private static bool MatchGlob(string name, string pattern)
    {
        // Iterative glob matching supporting '*' and '?'.
        int nameIndex = 0;
        int patternIndex = 0;
        int starIndex = -1;
        int starNameIndex = 0;

        while (nameIndex < name.Length)
        {
            if (patternIndex < pattern.Length
                && (pattern[patternIndex] == '?' || pattern[patternIndex] == name[nameIndex]))
            {
                nameIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex;
                starNameIndex = nameIndex;
                patternIndex++;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                nameIndex = ++starNameIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static FileSystemEntry ToEntry(string fullPath, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                var info = new DirectoryInfo(fullPath);
                return new FileSystemEntry(fullPath, true, 0, info.LastWriteTimeUtc);
            }

            var fileInfo = new FileInfo(fullPath);
            return new FileSystemEntry(fullPath, false, fileInfo.Length, fileInfo.LastWriteTimeUtc);
        }
        catch
        {
            return new FileSystemEntry(fullPath, isDirectory, 0, DateTimeOffset.MinValue);
        }
    }
}
