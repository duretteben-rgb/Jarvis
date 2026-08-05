using Jarvis.AI.AIProvider;
using Jarvis.AI.Configuration;
using Jarvis.AI.ContextManager;
using Jarvis.AI.Routing;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddHttpClient("openai", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        AIOptions options = configuration.GetSection("AI").Get<AIOptions>() ?? new AIOptions();

        if (options.Ollama.Enabled)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIProvider, OllamaProvider>());
        }

        if (options.OpenAI.Enabled)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIProvider, OpenAIProvider>());
        }

        services.TryAddSingleton<ModelRouter>();
        services.TryAddSingleton<ConversationContextManager>();
        services.TryAddSingleton<IAIService, JarvisAIService>();

        return services;
    }
}
