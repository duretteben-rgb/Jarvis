using Jarvis.SDK.Memory;

namespace Jarvis.Memory.Repository;

/// <summary>
/// A memory entry together with its stored embedding vector, as loaded for vector search.
/// </summary>
public readonly record struct StoredEntry(MemoryEntry Entry, float[] Embedding);
