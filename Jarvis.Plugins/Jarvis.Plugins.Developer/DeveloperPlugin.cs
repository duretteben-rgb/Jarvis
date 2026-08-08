using Jarvis.SDK.AI;
using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Jarvis.Plugins.Developer;

/// <summary>
/// JARVIS STUDIO — a developer agent plugin. It scaffolds projects from built-in templates,
/// writes source files, generates code through the AI engine, and drives the local toolchain
/// (dotnet, node, python) to build, test and run the projects it manages.
/// </summary>
public sealed class DeveloperPlugin : JarvisPluginBase
{
    public DeveloperPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.developer",
            Name = "JARVIS STUDIO",
            Version = "1.0.0",
            Description = "Developer agent: scaffold, generate, build, test and run projects.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.Processes, PermissionIds.Files, PermissionIds.AI },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("developer.project.create", "Scaffolds a project from a built-in template."),
        new PluginCommand("developer.project.list", "Lists the Studio projects in the workspace."),
        new PluginCommand("developer.project.info", "Shows a project's metadata and source tree."),
        new PluginCommand("developer.file.write", "Writes a source file inside a project."),
        new PluginCommand("developer.file.read", "Reads a source file inside a project."),
        new PluginCommand("developer.generate", "Asks the AI engine to write code into a project file."),
        new PluginCommand("developer.build", "Builds a project with its language toolchain."),
        new PluginCommand("developer.test", "Runs a project's self-contained tests."),
        new PluginCommand("developer.run", "Runs the project and captures its output."),
    };

    private string WorkspaceRoot => Context.Host.Configuration.GetValue("Jarvis:Studio:Root")
        ?? Path.Combine(AppContext.BaseDirectory, "projects");

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        switch (command)
        {
            case "developer.project.create":
                return await CreateProjectAsync(parameters, cancellationToken);

            case "developer.project.list":
                return ListProjects();

            case "developer.project.info":
                return ShowProject(Required(parameters, "name"));

            case "developer.file.write":
                return WriteFile(parameters);

            case "developer.file.read":
                return ReadFile(Required(parameters, "path"));

            case "developer.generate":
                return await GenerateAsync(parameters, cancellationToken);

            case "developer.build":
            case "developer.test":
            case "developer.run":
                return await RunProjectAsync(parameters, command, cancellationToken);

            default:
                return await base.ExecuteCommandAsync(command, parameters, cancellationToken);
        }
    }

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation("{Plugin} ({Version}) ready; workspace at {Workspace}.",
            Manifest.Id, Manifest.Version, WorkspaceRoot);
        return Task.CompletedTask;
    }

    private Task<object?> CreateProjectAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        string name = Required(parameters, "name");
        string template = GetString(parameters, "template") ?? GetString(parameters, "language") ?? "dotnet-console";

        if (!ProjectTemplates.IsKnown(template))
        {
            throw new PluginException(Manifest.Id, $"Unknown template '{template}'. Available: {string.Join(", ", ProjectTemplates.Templates.Keys)}");
        }

        Directory.CreateDirectory(WorkspaceRoot);
        string root = Path.Combine(WorkspaceRoot, SanitizeName(name));

        if (Directory.Exists(root))
        {
            throw new PluginException(Manifest.Id, $"A project named '{name}' already exists at {root}.");
        }

        Directory.CreateDirectory(root);
        ProjectTemplates.Scaffold(template, name, root);

        ProjectMetadata.Save(new ProjectMetadata
        {
            Name = name,
            Language = template,
            Type = template,
            Root = root,
            Commands = new Dictionary<string, string>
            {
                ["build"] = ProjectTemplates.DefaultCommand(template, "build"),
                ["test"] = ProjectTemplates.DefaultCommand(template, "test"),
                ["run"] = ProjectTemplates.DefaultCommand(template, "run"),
            },
        });

        Context.Logger.LogInformation("Created project {Name} ({Template}) at {Root}.", name, template, root);
        return Task.FromResult<object?>($"Created project '{name}' ({template}) at {root}.");
    }

    private object ListProjects()
    {
        IReadOnlyList<StudioProject> projects = DiscoverProjects();
        if (projects.Count == 0)
        {
            return "No projects yet. Create one with developer.project.create.";
        }

        return projects.Select(project => $"{project.Name} [{project.Language}] — {project.Root}");
    }

    private object ShowProject(string name)
    {
        StudioProject project = ResolveProject(name);
        var files = new List<string>();
        CollectTree(project.Root, files, depth: 0, maxDepth: 4);

        var lines = new List<string>
        {
            $"Project : {project.Name}",
            $"Type    : {project.Language}",
            $"Root    : {project.Root}",
            string.Empty,
        };

        lines.AddRange(files);
        return string.Join('\n', lines);
    }

    private object WriteFile(IReadOnlyDictionary<string, object?>? parameters)
    {
        string path = Required(parameters, "path");
        string content = GetString(parameters, "content") ?? string.Empty;
        bool append = GetBool(parameters, "append") ?? false;

        string fullPath = EnsureInsideWorkspace(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (append)
        {
            File.AppendAllText(fullPath, content);
        }
        else
        {
            File.WriteAllText(fullPath, content);
        }

        return $"Wrote {(append ? "(appended) " : string.Empty)}{fullPath}.";
    }

    private object ReadFile(string path)
    {
        string fullPath = EnsureInsideWorkspace(path);
        if (!File.Exists(fullPath))
        {
            throw new PluginException(Manifest.Id, $"File not found: {fullPath}");
        }

        return File.ReadAllText(fullPath);
    }

    private async Task<object?> GenerateAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        string path = Required(parameters, "path");
        string prompt = Required(parameters, "prompt");
        string? model = GetString(parameters, "model");

        string fullPath = EnsureInsideWorkspace(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string languageHint = Path.GetExtension(fullPath) switch
        {
            ".cs" => "C#",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".py" => "Python",
            ".json" => "JSON",
            ".md" => "Markdown",
            _ => "the file's language",
        };

        var ai = Context.Host.Services.GetService(typeof(IAIService)) as IAIService;
        if (ai is null)
        {
            throw new PluginException(Manifest.Id, "The AI engine is not available in this host.");
        }

        var request = new ChatRequest
        {
            Model = model,
            TaskKind = TaskKind.Coding,
            PreferLocal = false,
            MaxTokens = 4096,
            Messages = new[]
            {
                ChatMessage.System(
                    "You are JARVIS STUDIO, a senior software engineer agent. " +
                    $"Write clean, complete, production-quality {languageHint} code that satisfies the " +
                    "user's request. Return ONLY the code in a single fenced code block; no prose."),
                ChatMessage.User(prompt),
            },
        };

        ChatResponse response = await ai.ChatAsync(request, cancellationToken);
        string code = StripCodeFence(response.Message.Content);
        File.WriteAllText(fullPath, code);

        Context.Logger.LogInformation("Generated {Path} via model {Model} ({Provider}).", fullPath, response.Model, response.Provider);
        return $"Generated {fullPath} using {response.Model} ({response.Provider}).\n\n{code}";
    }

    private async Task<object?> RunProjectAsync(
        IReadOnlyDictionary<string, object?>? parameters,
        string command,
        CancellationToken cancellationToken)
    {
        string name = Required(parameters, "name");
        StudioProject project = ResolveProject(name);
        string action = command switch
        {
            "developer.build" => "build",
            "developer.test" => "test",
            _ => "run",
        };

        string? commandLine = ProjectMetadata.TryLoad(project.Root)
            ?.Commands.GetValueOrDefault(action);
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            throw new PluginException(Manifest.Id, $"Project '{name}' has no {action} command configured.");
        }

        int timeoutSeconds = GetInt(parameters, "timeoutMs") is int timeoutMs
            ? Math.Clamp(timeoutMs / 1000, 5, 300)
            : 120;

        Context.Logger.LogInformation("Studio {Action} on {Name}: {Command}", action, name, commandLine);
        ProcessOutput result = await CommandRunner.RunAsync(
            commandLine,
            project.Root,
            TimeSpan.FromSeconds(timeoutSeconds),
            cancellationToken);

        bool succeeded = result.ExitCode == 0;
        return $"[{action} of '{name}' — {(succeeded ? "OK" : "FAILED")} (exit {result.ExitCode})]\n{result.Output}";
    }

    private IReadOnlyList<StudioProject> DiscoverProjects()
    {
        if (!Directory.Exists(WorkspaceRoot))
        {
            return Array.Empty<StudioProject>();
        }

        var projects = new List<StudioProject>();
        foreach (string directory in Directory.EnumerateDirectories(WorkspaceRoot))
        {
            ProjectMetadata? metadata = ProjectMetadata.TryLoad(directory);
            if (metadata is null)
            {
                continue;
            }

            projects.Add(new StudioProject(
                metadata.Name,
                metadata.Language,
                metadata.Type,
                metadata.Root,
                metadata.Commands.GetValueOrDefault("build")));
        }

        return projects
            .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private StudioProject ResolveProject(string name)
    {
        StudioProject? match = DiscoverProjects().FirstOrDefault(project =>
            project.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || project.Root.EndsWith(name, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new PluginException(Manifest.Id, $"Unknown project '{name}'.");
    }

    private string EnsureInsideWorkspace(string path)
    {
        string fullPath = Path.GetFullPath(path, WorkspaceRoot);
        string root = Path.GetFullPath(WorkspaceRoot);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginException(Manifest.Id, $"Path '{path}' is outside the Studio workspace ({root}).");
        }

        return fullPath;
    }

    private static void CollectTree(string directory, List<string> lines, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            return;
        }

        string indent = new string(' ', depth * 2);
        foreach (string entry in Directory.EnumerateDirectories(directory)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{indent}[{Path.GetFileName(entry)}/]");
            CollectTree(entry, lines, depth + 1, maxDepth);
        }

        foreach (string entry in Directory.EnumerateFiles(directory)
                     .Where(path => !path.EndsWith(ProjectMetadata.FileName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{indent}{Path.GetFileName(entry)}");
        }
    }

    private static string StripCodeFence(string content)
    {
        string trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = trimmed.IndexOf('\n');
            int end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLineBreak >= 0 && end > firstLineBreak)
            {
                trimmed = trimmed[(firstLineBreak + 1)..end].TrimEnd();
            }
        }

        return trimmed;
    }

    private static string SanitizeName(string name)
    {
        string safe = new string(name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        return safe.Length == 0 || !char.IsLetter(safe[0]) ? "App" + safe : safe;
    }

    private string Required(IReadOnlyDictionary<string, object?>? parameters, string key)
        => GetString(parameters, key)
            ?? throw new PluginException(Manifest.Id, $"Parameter '{key}' is required.");

    private static string? GetString(IReadOnlyDictionary<string, object?>? parameters, string key)
        => parameters?.TryGetValue(key, out object? value) == true ? value as string : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out object? value) != true || value is null)
        {
            return null;
        }

        return value is int integer
            ? integer
            : int.TryParse(value.ToString(), out int parsed) ? parsed : null;
    }

    private static bool? GetBool(IReadOnlyDictionary<string, object?>? parameters, string key)
    {
        if (parameters?.TryGetValue(key, out object? value) != true || value is null)
        {
            return null;
        }

        return value is bool boolean
            ? boolean
            : bool.TryParse(value.ToString(), out bool parsed) ? parsed : null;
    }
}
