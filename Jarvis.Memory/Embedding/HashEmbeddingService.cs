using Jarvis.Memory.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.Memory.Embedding;

/// <summary>
/// Computes embeddings for memory content so entries can be found by semantic search.
///
/// The default provider is a deterministic, dependency-free local implementation: character
/// n-grams of the text are hashed into a fixed-size bag-of-vectors representation. This is not
/// as expressive as a trained model but is stable across restarts and requires no external
/// service, which is appropriate for a local personal assistant.
/// </summary>
public sealed class HashEmbeddingService
{
    private const int Dimensions = 256;
    private const int NGramSize = 3;

    private readonly ILogger<HashEmbeddingService> _logger;

    public HashEmbeddingService(IOptions<EmbeddingOptions> options, ILogger<HashEmbeddingService> logger)
    {
        _logger = logger;
        if (!string.Equals(options.Value.Provider, "hash", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Embedding provider '{Provider}' is not available in this build; falling back to the built-in 'hash' provider.",
                options.Value.Provider);
        }
    }

    /// <summary>Computes a fixed-size, L2-normalized embedding for arbitrary text.</summary>
    public float[] Compute(string text)
    {
        float[] vector = new float[Dimensions];

        ReadOnlySpan<char> span = text.AsSpan();
        if (span.IsEmpty)
        {
            return vector;
        }

        // Character n-grams capture local structure (prefixes, suffixes, common substrings)
        // without needing a vocabulary or external model.
        Span<char> buffer = stackalloc char[NGramSize];
        for (int i = 0; i < span.Length; i++)
        {
            int end = Math.Min(i + NGramSize, span.Length);
            Span<char> gram = buffer[..(end - i)];
            for (int j = i; j < end; j++)
            {
                gram[j - i] = span[j];
            }

            uint hash = StableHash(gram);
            int index = (int)(hash % Dimensions);
            // Alternate sign by hash bit to give both positive and negative dimensions.
            vector[index] += (hash & 1) == 0 ? 1f : -1f;
        }

        NormalizeInPlace(vector);
        return vector;
    }

    /// <summary>Number of dimensions every embedding produced by this service has.</summary>
    public int GetDimensions() => Dimensions;

    private static uint StableHash(ReadOnlySpan<char> gram)
    {
        // FNV-1a over the UTF-16 code units; stable across runs and platforms.
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        uint hash = offsetBasis;
        foreach (char c in gram)
        {
            hash ^= c;
            hash *= prime;
        }

        return hash;
    }

    private static void NormalizeInPlace(float[] vector)
    {
        double sum = 0d;
        for (int i = 0; i < vector.Length; i++)
        {
            sum += (double)vector[i] * vector[i];
        }

        double magnitude = Math.Sqrt(sum);
        if (magnitude < 1e-12d)
        {
            return;
        }

        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
    }
}
