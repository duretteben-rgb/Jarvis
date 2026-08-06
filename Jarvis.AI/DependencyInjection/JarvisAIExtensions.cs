using Jarvis.AI.AIProvider;
using Jarvis.AI.Configuration;
using Jarvis.AI.ContextManager;
using Jarvis.AI.Routing;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.DependencyInjection;

/// <summary>
/// Registers the JARVIS AI engine into a service collection: model providers (local Ollama,
/// OpenAI-compatible cloud), the intelligent model router, the conversation context manager and
/// <see cref="IAIService"/>.
/// </summary>
public static class JarvisAIExtensions
{
    /// <summary>
    /// Adds the AI stack bound from the <c>AI</c> configuration section. Only enabled providers
    /// are registered, and requests fall back between them automatically.
    /// </summary>
    public static IServiceCollection AddJarvisAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AIOptions>()
            .Bind(configuration.GetSection("AI"));

        services.AddHttpClient("ollama", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        AIOptions options = configuration.GetSection("AI").Get<AIOptions>() ?? new AIOptions();

        if (options.Ollama.Enabled)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIProvider, OllamaProvider>());
        }

        // One provider instance per enabled OpenAI-compatible entry. The legacy AI:OpenAI
        // section is treated as an implicit first entry so existing configurations keep working.
        var openAiCompatEntries = new List<OpenAICompatibleOptions>();
        if (options.OpenAI.Enabled)
        {
            openAiCompatEntries.Add(options.OpenAI);
        }

        openAiCompatEntries.AddRange(options.OpenAICompat.Where(entry => entry.Enabled));

        foreach (OpenAICompatibleOptions entry in openAiCompatEntries)
        {
            OpenAICompatibleOptions captured = entry;
            string clientName = $"openai-{captured.Id}";
            services.AddHttpClient(clientName, client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5);
            });

            services.AddSingleton<IAIProvider>(
                serviceProvider => new OpenAIProvider(
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    captured,
                    serviceProvider.GetRequiredService<ILogger<OpenAIProvider>>()));
        }

        services.TryAddSingleton<ModelRouter>();
        services.TryAddSingleton<ConversationContextManager>();
        services.TryAddSingleton<IAIService, JarvisAIService>();

        return services;
    }
}
