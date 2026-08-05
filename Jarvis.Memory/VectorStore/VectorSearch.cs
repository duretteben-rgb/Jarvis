using Jarvis.Memory.Repository;
using Jarvis.SDK.Memory;

namespace Jarvis.Memory.VectorStore;

/// <summary>
/// Ranks stored memory entries by cosine similarity against a query embedding.
///
/// This is an in-memory, exact nearest-neighbour search over the loaded entries. It is
/// intentionally simple: the dataset is local and small (a personal assistant's memory), so
/// brute force is both fast enough and avoids the complexity of an ANN index.
/// </summary>
public static class VectorSearch
{
    /// <summary>
    /// Returns entries with a stored embedding, ranked by cosine similarity (descending) to the
    /// query embedding, filtered to the given count.
    /// </summary>
    public static IReadOnlyList<MemorySearchResult> Rank(
        IReadOnlyList<StoredEntry> entries,
        float[] queryEmbedding,
        int limit,
        double minimumScore = 0.0d)
    {
        if (entries.Count == 0 || queryEmbedding.Length == 0)
        {
            return Array.Empty<MemorySearchResult>();
        }

        var scored = new List<(MemoryEntry Entry, double Score)>(entries.Count);
        foreach (StoredEntry stored in entries)
        {
            if (stored.Embedding.Length == 0)
            {
                continue;
            }

            double score = CosineSimilarity(queryEmbedding, stored.Embedding);
            if (score >= minimumScore)
            {
                scored.Add((stored.Entry, score));
            }
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        int take = Math.Min(limit, scored.Count);
        var results = new List<MemorySearchResult>(take);
        for (int i = 0; i < take; i++)
        {
            results.Add(new MemorySearchResult(scored[i].Entry, scored[i].Score));
        }

        return results;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        int length = Math.Min(a.Length, b.Length);
        double dot = 0d;
        for (int i = 0; i < length; i++)
        {
            dot += (double)a[i] * b[i];
        }

        // Both vectors are L2-normalized by the embedding service, so the dot product already
        // is the cosine. Guard against tiny magnitudes introduced by storage round-tripping.
        double magnitudeA = 0d;
        double magnitudeB = 0d;
        for (int i = 0; i < length; i++)
        {
            magnitudeA += (double)a[i] * a[i];
            magnitudeB += (double)b[i] * b[i];
        }

        double denominator = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        return denominator < 1e-12d ? 0d : Math.Clamp(dot / denominator, 0.0d, 1.0d);
    }
}
