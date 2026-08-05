using System.Collections.Concurrent;
using Jarvis.AI.AIProvider;
using Jarvis.AI.Configuration;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.Routing;

/// <summary>A model selected by the router, together with the provider that serves it.</summary>
public sealed record RoutedModel(string DefinitionId, string ProviderId, string ModelName, string DisplayName);

/// <summary>
/// Decides which provider and model serve each request. The router keeps a short-lived health
/// cache per provider, prefers local models when asked to (so requests can be answered offline)
/// and matches models to the task kind through capability tags.
/// </summary>
public sealed class ModelRouter
{
    private readonly AIOptions _options;
    private readonly IReadOnlyDictionary<string, IAIProvider> _providers;
    private readonly ConcurrentDictionary<string, ProviderState> _health = new();
    private readonly ILogger<ModelRouter> _logger;

    public ModelRouter(
        IEnumerable<IAIProvider> providers,
        IOptions<AIOptions> options,
        ILogger<ModelRouter> logger)
    {
        _options = options.Value;
        _providers = providers.ToDictionary(provider => provider.Id, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>Returns provider snapshots with their current (cached) health.</summary>
    public async Task<IReadOnlyList<AIProviderInfo>> GetProviderInfosAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<AIProviderInfo>(_providers.Count);
        foreach (IAIProvider provider in _providers.Values)
        {
            ProviderHealth health = await GetHealthAsync(provider, cancellationToken);
            results.Add(new AIProviderInfo
            {
                Id = provider.Id,
                DisplayName = provider.DisplayName,
                IsLocal = provider.IsLocal,
                Models = health.Models,
                IsAvailable = health.IsAvailable,
                Error = health.Error,
            });
        }

        return results;
    }

    /// <summary>
    /// Returns the ordered list of candidate models for a request, best first. Unavailable
    /// providers are skipped. Empty when nothing can serve the request.
    /// </summary>
    public async Task<IReadOnlyList<RoutedModel>> RouteCandidatesAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ModelDefinition> definitions = GetDefinitions();

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            ModelDefinition? forced = definitions.FirstOrDefault(
                definition => string.Equals(definition.Id, request.Model, StringComparison.OrdinalIgnoreCase));
            if (forced is null)
            {
                return Array.Empty<RoutedModel>();
            }

            IAIProvider? provider = _providers.GetValueOrDefault(forced.Provider);
            if (provider is null)
            {
                return Array.Empty<RoutedModel>();
            }

            ProviderHealth health = await GetHealthAsync(provider, cancellationToken);
            if (!health.IsAvailable)
            {
                return Array.Empty<RoutedModel>();
            }

            return new[]
            {
                new RoutedModel(forced.Id, provider.Id, forced.Model, DisplayName(forced)),
            };
        }

        string[] matchingTags = MatchingTags(request.TaskKind);
        var scored = new List<(ModelDefinition Definition, IAIProvider Provider, int Score)>(definitions.Count);
        foreach (ModelDefinition definition in definitions)
        {
            if (!_providers.TryGetValue(definition.Provider, out IAIProvider? provider))
            {
                continue;
            }

            int score = 0;
            if (definition.Tags.Any(tag => matchingTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            {
                score += 2;
            }

            if (definition.IsDefault)
            {
                score += 1;
            }

            scored.Add((definition, provider, score));
        }

        var available = new List<(ModelDefinition Definition, IAIProvider Provider, int Score)>();
        foreach ((ModelDefinition definition, IAIProvider provider, int score) in scored)
        {
            ProviderHealth health = await GetHealthAsync(provider, cancellationToken);
            if (health.IsAvailable)
            {
                available.Add((definition, provider, score));
            }
        }

        IEnumerable<(ModelDefinition Definition, IAIProvider Provider, int Score)> ordered = available
            .OrderByDescending(entry => entry.Provider.IsLocal == _options.Routing.PreferLocal)
            .ThenByDescending(entry => entry.Score);

        return ordered
            .Select(entry => new RoutedModel(
                entry.Definition.Id,
                entry.Provider.Id,
                entry.Definition.Model,
                DisplayName(entry.Definition)))
            .ToList();
    }

    /// <summary>Records a failed provider call so it can enter a cooldown.</summary>
    public void RecordFailure(string providerId)
    {
        if (!_providers.TryGetValue(providerId, out IAIProvider? provider))
        {
            return;
        }

        GetOrCreateState(providerId).Fail(_options.Routing, providerId, _logger);
    }

    private IReadOnlyList<ModelDefinition> GetDefinitions()
    {
        if (_options.Models.Count > 0)
        {
            return _options.Models;
        }

        // No explicit model list: synthesize one entry per enabled provider.
        var synthesized = new List<ModelDefinition>();
        foreach (IAIProvider provider in _providers.Values)
        {
            if (provider.Id == "ollama")
            {
                synthesized.Add(new ModelDefinition
                {
                    Id = "local-default",
                    Provider = provider.Id,
                    Model = _options.Ollama.Model,
                    DisplayName = $"{_options.Ollama.Model} (local)",
                    IsDefault = true,
                    Tags = new List<string> { "fast", "offline" },
                });
            }
            else
            {
                synthesized.Add(new ModelDefinition
                {
                    Id = "cloud-default",
                    Provider = provider.Id,
                    Model = _options.OpenAI.Model,
                    DisplayName = _options.OpenAI.Model,
                    IsDefault = true,
                    Tags = new List<string> { "powerful" },
                });
            }
        }

        return synthesized;
    }

    private async Task<ProviderHealth> GetHealthAsync(IAIProvider provider, CancellationToken cancellationToken)
    {
        ProviderState state = GetOrCreateState(provider.Id);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock (state)
        {
            if (state.CooldownUntil > now)
            {
                return state.Health;
            }

            if (now - state.LastCheck < TimeSpan.FromSeconds(_options.Routing.AvailabilityTtlSeconds))
            {
                return state.Health;
            }
        }

        // Probe outside the lock so a slow provider does not block others; then publish.
        ProviderHealth health;
        try
        {
            health = await provider.CheckHealthAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            health = ProviderHealth.Fail(exception.Message);
        }

        lock (state)
        {
            state.LastCheck = now;
            state.Health = health;
            if (health.IsAvailable)
            {
                state.Failures = 0;
                state.CooldownUntil = DateTimeOffset.MinValue;
            }
            else
            {
                state.Failures++;
                _logger.LogDebug("Provider {Provider} health probe failed ({Failures} consecutive).",
                    provider.Id, state.Failures);
            }
        }

        return health;
    }

    private ProviderState GetOrCreateState(string providerId) => _health.GetOrAdd(providerId, _ => new ProviderState());

    private static string DisplayName(ModelDefinition definition)
        => string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName;

    private static string[] MatchingTags(TaskKind taskKind) => taskKind switch
    {
        TaskKind.Simple => new[] { "fast" },
        TaskKind.Complex => new[] { "powerful", "complex" },
        TaskKind.Reasoning => new[] { "reasoning" },
        TaskKind.Coding => new[] { "coding" },
        TaskKind.Summarization => new[] { "summarize", "fast" },
        _ => Array.Empty<string>(),
    };
}

/// <summary>
/// Tracks cached health and cooldown state for a single provider.
/// </summary>
internal sealed class ProviderState
{
    public ProviderHealth Health { get; set; } = ProviderHealth.Fail("Not checked yet");
    public DateTimeOffset LastCheck { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset CooldownUntil { get; set; } = DateTimeOffset.MinValue;
    public int Failures { get; set; }

    public void Fail(RoutingOptions routing, string providerId, ILogger logger)
    {
        Health = ProviderHealth.Fail("Recent request failed.");
        Failures++;
        if (Failures >= routing.ConsecutiveFailuresBeforeCooldown)
        {
            CooldownUntil = DateTimeOffset.UtcNow.AddSeconds(routing.CooldownSeconds);
            logger.LogWarning(
                "Provider {Provider} entered a {Seconds}s cooldown after {Count} consecutive failures.",
                providerId, routing.CooldownSeconds, Failures);
            Failures = 0;
        }
    }
}
