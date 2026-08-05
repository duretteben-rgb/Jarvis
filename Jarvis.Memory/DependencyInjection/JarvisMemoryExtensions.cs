using Jarvis.Memory.Configuration;
using Jarvis.Memory.Database;
using Jarvis.Memory.Embedding;
using Jarvis.Memory.Repository;
using Jarvis.SDK.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MemoryDb = Jarvis.Memory.Database.MemoryDatabase;

namespace Jarvis.Memory.DependencyInjection;

/// <summary>
/// Registers the JARVIS memory system into a service collection.
/// </summary>
public static class JarvisMemoryExtensions
{
    /// <summary>
    /// Adds the memory stack: SQLite database, entry/preference repository, local embedding
    /// service and <see cref="IMemoryService"/>. Bound from the <c>Memory</c> and
    /// <c>Embeddings</c> configuration sections.
    /// </summary>
    public static IServiceCollection AddJarvisMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MemoryOptions>()
            .Bind(configuration.GetSection("Memory"));
        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection("Embeddings"));

        services.TryAddSingleton<MemoryDb>();
        services.TryAddSingleton<MemoryRepository>();
        services.TryAddSingleton<HashEmbeddingService>();
        services.TryAddSingleton<IMemoryService, MemoryManager>();

        return services;
    }
}
