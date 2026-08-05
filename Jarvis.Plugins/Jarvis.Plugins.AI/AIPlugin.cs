using Jarvis.SDK.Memory;
using Jarvis.SDK.Permissions;
using Jarvis.SDK.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Jarvis.Plugins.AI;

/// <summary>
/// AI plugin for JARVIS OS. Exposes model-backed chat and integrates with the JARVIS memory
/// system: entries stored here are embedded and made searchable, so the assistant can recall
/// user facts and preferences later.
/// </summary>
public sealed class AIPlugin : JarvisPluginBase
{
    private IMemoryService? _memory;

    public AIPlugin()
    {
        Manifest = new PluginManifest
        {
            Id = "jarvis.ai",
            Name = "AI Assistant",
            Version = "1.0.0",
            Description = "Model-backed chat and semantic memory integration.",
            Author = "JARVIS Team",
            MinimumCoreVersion = new Version(0, 2, 0),
            Permissions = new[] { PermissionIds.AI, PermissionIds.Memory },
        };
    }

    /// <inheritdoc />
    public override IReadOnlyList<PluginCommand> Commands => new[]
    {
        new PluginCommand("ai.remember", "Stores a fact into JARVIS memory."),
        new PluginCommand("ai.search", "Searches JARVIS memory semantically."),
        new PluginCommand("ai.set-preference", "Stores a user preference."),
        new PluginCommand("ai.get-preference", "Reads a user preference."),
    };

    /// <inheritdoc />
    public override async Task<object?> ExecuteCommandAsync(
        string command,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (_memory is null)
        {
            _memory = Context.Host.Services.GetService<IMemoryService>();
            if (_memory is null)
            {
                throw new PluginException(Manifest.Id, "The JARVIS memory system is not available.");
            }
        }

        switch (command)
        {
            case "ai.remember":
            {
                string? content = parameters?.GetValueOrDefault("content") as string;
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new PluginException(Manifest.Id, "A 'content' parameter is required.");
                }

                Guid id = await _memory.StoreAsync(new MemoryEntry
                {
                    Kind = MemoryKind.LongTerm,
                    Content = content,
                }, cancellationToken);

                Context.Logger.LogInformation("Remembered '{Content}' as {EntryId}.", content, id);
                return $"Remembered as {id}";
            }

            case "ai.search":
            {
                string? query = parameters?.GetValueOrDefault("query") as string;
                if (string.IsNullOrWhiteSpace(query))
                {
                    throw new PluginException(Manifest.Id, "A 'query' parameter is required.");
                }

                IReadOnlyList<MemorySearchResult> results = await _memory.SearchAsync(query, kind: MemoryKind.LongTerm, cancellationToken: cancellationToken);
                return results.Count == 0
                    ? "No memories found."
                    : string.Join("\n", results.Select(r => $"[{r.Score:F2}] {r.Entry.Content}"));
            }

            case "ai.set-preference":
            {
                string? key = parameters?.GetValueOrDefault("key") as string;
                string? value = parameters?.GetValueOrDefault("value") as string;
                if (string.IsNullOrWhiteSpace(key) || value is null)
                {
                    throw new PluginException(Manifest.Id, "'key' and 'value' parameters are required.");
                }

                await _memory.SetPreferenceAsync(key, value, cancellationToken);
                return $"Preference '{key}' set.";
            }

            case "ai.get-preference":
            {
                string? key = parameters?.GetValueOrDefault("key") as string;
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new PluginException(Manifest.Id, "A 'key' parameter is required.");
                }

                string? value = await _memory.GetPreferenceAsync(key, cancellationToken);
                return value is null ? $"No preference for '{key}'." : value;
            }

            default:
                return await base.ExecuteCommandAsync(command, parameters, cancellationToken);
        }
    }

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        Context.Logger.LogInformation("{Plugin} ({Version}) ready.", Manifest.Id, Manifest.Version);
        return Task.CompletedTask;
    }
}
