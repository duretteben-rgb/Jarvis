using System.Text.Json;

namespace Jarvis.Plugins.Developer;

/// <summary>Metadata stored as <c>.jarvis.json</c> inside every Studio project root.</summary>
public sealed record ProjectMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Language { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Root { get; init; } = string.Empty;
    public Dictionary<string, string> Commands { get; init; } = new();

    public const string FileName = ".jarvis.json";

    public static ProjectMetadata? TryLoad(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ProjectMetadata>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static void Save(ProjectMetadata metadata)
    {
        string path = Path.Combine(metadata.Root, FileName);
        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}

/// <summary>A discovered Studio project with a resolved command for a given action.</summary>
public sealed record StudioProject(
    string Name,
    string Language,
    string Type,
    string Root,
    string? Command);

/// <summary>The outcome of a build, test or run execution.</summary>
public sealed record ProcessOutput(int ExitCode, string Output, string Command)
{
    /// <inheritdoc />
    public override string ToString()
        => $"exit {ExitCode}\n{Output}".Trim();
}
