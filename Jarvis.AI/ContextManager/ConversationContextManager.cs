using System.Collections.Concurrent;
using Jarvis.AI.Configuration;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.ContextManager;

/// <summary>
/// Tracks per-session conversation history for the AI engine. History is held in memory,
/// trimmed to a token budget so requests stay within model limits, and evicted LRU-style when
/// too many sessions accumulate.
/// </summary>
public sealed class ConversationContextManager
{
    private readonly ConcurrentDictionary<string, ConversationContext> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ContextOptions _options;

    public ConversationContextManager(IOptions<AIOptions> options)
    {
        _options = options.Value.Context;
    }

    /// <summary>
    /// Builds the message list sent to the model: an optional system prompt, the tracked
    /// history (trimmed to the token budget) and the new user message.
    /// </summary>
    public IReadOnlyList<ChatMessage> BuildMessages(
        string sessionId,
        string? systemPrompt,
        ChatMessage userMessage)
    {
        ConversationContext context = GetOrCreate(sessionId, systemPrompt);
        context.Touch();

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(ChatMessage.System(systemPrompt));
        }

        int budget = _options.MaxTokens;
        // Always keep the newest user message.
        messages.Add(userMessage);
        int usedTokens = EstimateTokens(userMessage.Content);

        // Walk history newest-first, keeping messages while the budget allows.
        var kept = new List<ChatMessage>(context.History.Count);
        for (int i = context.History.Count - 1; i >= 0; i--)
        {
            ChatMessage message = context.History[i];
            int tokens = EstimateTokens(message.Content);
            if (usedTokens + tokens > budget)
            {
                break;
            }

            usedTokens += tokens;
            kept.Add(message);
        }

        kept.Reverse();
        messages.InsertRange(messages.Count - 1, kept);
        return messages;
    }

    /// <summary>Records an exchange so it becomes part of the session history.</summary>
    public void RecordExchange(string sessionId, ChatMessage userMessage, string assistantContent)
    {
        ConversationContext context = GetOrCreate(sessionId, null);
        context.Touch();
        context.History.Add(userMessage);
        context.History.Add(ChatMessage.Assistant(assistantContent));
    }

    /// <summary>Removes all history for a session.</summary>
    public void Clear(string sessionId) => _sessions.TryRemove(sessionId, out _);

    private ConversationContext GetOrCreate(string sessionId, string? systemPrompt)
    {
        ConversationContext context = _sessions.GetOrAdd(sessionId, _ => new ConversationContext { SystemPrompt = systemPrompt });
        EvictIfNeeded();
        return context;
    }

    private void EvictIfNeeded()
    {
        if (_sessions.Count <= _options.MaxSessions)
        {
            return;
        }

        string? oldestId = _sessions
            .OrderBy(pair => pair.Value.LastAccess)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (oldestId is not null)
        {
            _sessions.TryRemove(oldestId, out _);
        }
    }

    /// <summary>Rough token estimate: one token per four characters, at least one.</summary>
    internal static int EstimateTokens(string text)
        => string.IsNullOrEmpty(text) ? 1 : Math.Max(1, (int)Math.Ceiling(text.Length / 4.0d));
}

/// <summary>Mutable per-session state guarded by the context manager.</summary>
internal sealed class ConversationContext
{
    public string? SystemPrompt { get; init; }
    public List<ChatMessage> History { get; } = new();
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastAccess { get; private set; } = DateTimeOffset.UtcNow;

    public void Touch() => LastAccess = DateTimeOffset.UtcNow;
}
