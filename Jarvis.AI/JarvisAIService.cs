using System.Runtime.CompilerServices;
using System.Text;
using Jarvis.AI.AIProvider;
using Jarvis.AI.ContextManager;
using Jarvis.AI.Routing;
using Jarvis.SDK.AI;
using Microsoft.Extensions.Logging;

namespace Jarvis.AI;

/// <summary>
/// Default implementation of <see cref="IAIService"/>. Composes the model providers, the
/// intelligent router and the conversation context manager into one API. Requests are routed to
/// the best available model and transparently fall back when a provider fails.
/// </summary>
public sealed class JarvisAIService : IAIService
{
    private readonly IReadOnlyDictionary<string, IAIProvider> _providers;
    private readonly ModelRouter _router;
    private readonly ConversationContextManager _contextManager;
    private readonly ILogger<JarvisAIService> _logger;

    public JarvisAIService(
        IEnumerable<IAIProvider> providers,
        ModelRouter router,
        ConversationContextManager contextManager,
        ILogger<JarvisAIService> logger)
    {
        _providers = providers.ToDictionary(provider => provider.Id, StringComparer.OrdinalIgnoreCase);
        _router = router;
        _contextManager = contextManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AIProviderInfo>> GetProvidersAsync(CancellationToken cancellationToken = default)
        => _router.GetProviderInfosAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        PreparedMessages prepared = PrepareMessages(request);

        IReadOnlyList<RoutedModel> candidates = await _router.RouteCandidatesAsync(request, cancellationToken);
        if (candidates.Count == 0)
        {
            throw BuildNoCandidateException(request);
        }

        Exception? lastError = null;
        foreach (RoutedModel candidate in candidates)
        {
            if (!_providers.TryGetValue(candidate.ProviderId, out IAIProvider? provider))
            {
                continue;
            }

            try
            {
                ChatResponse response = await provider.ChatAsync(
                    prepared.Messages, candidate.ModelName, request.Temperature, request.MaxTokens, cancellationToken);

                if (prepared.SessionId is not null)
                {
                    _contextManager.RecordExchange(prepared.SessionId, prepared.UserMessage, response.Message.Content);
                }

                return new ChatResponse
                {
                    Message = response.Message,
                    Model = candidate.ModelName,
                    Provider = candidate.ProviderId,
                    PromptTokens = response.PromptTokens,
                    CompletionTokens = response.CompletionTokens,
                    FinishReason = response.FinishReason,
                    DurationMs = response.DurationMs,
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
                _router.RecordFailure(candidate.ProviderId);
                _logger.LogWarning("Provider {Provider} failed for model {Model}: {Error}",
                    candidate.ProviderId, candidate.ModelName, exception.Message);
            }
        }

        throw new InvalidOperationException(
            $"All {candidates.Count} candidate model(s) failed. Last error: {lastError?.Message}",
            lastError);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        PreparedMessages prepared = PrepareMessages(request);

        IReadOnlyList<RoutedModel> candidates = await _router.RouteCandidatesAsync(request, cancellationToken);
        if (candidates.Count == 0)
        {
            throw BuildNoCandidateException(request);
        }

        Exception? lastError = null;
        foreach (RoutedModel candidate in candidates)
        {
            if (!_providers.TryGetValue(candidate.ProviderId, out IAIProvider? provider))
            {
                continue;
            }

            var fullText = new StringBuilder();
            IAsyncEnumerator<ChatChunk> enumerator = provider
                .StreamChatAsync(prepared.Messages, candidate.ModelName, request.Temperature, request.MaxTokens, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

            // Open the stream and read the first chunk. If the provider fails before
            // producing anything, fall back to the next candidate.
            bool hasChunk;
            try
            {
                hasChunk = await enumerator.MoveNextAsync();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastError = exception;
                _router.RecordFailure(candidate.ProviderId);
                _logger.LogWarning("Provider {Provider} failed to start streaming ({Error}); trying next candidate.",
                    candidate.ProviderId, exception.Message);
                await enumerator.DisposeAsync();
                continue;
            }

            while (hasChunk)
            {
                fullText.Append(enumerator.Current.Delta);
                yield return enumerator.Current;

                try
                {
                    hasChunk = await enumerator.MoveNextAsync();
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    lastError = exception;
                    _logger.LogWarning("Provider {Provider} stopped streaming early ({Error}).",
                        candidate.ProviderId, exception.Message);
                    break;
                }
            }

            if (prepared.SessionId is not null && fullText.Length > 0)
            {
                _contextManager.RecordExchange(prepared.SessionId, prepared.UserMessage, fullText.ToString());
            }

            await enumerator.DisposeAsync();
            yield break;
        }

        throw new InvalidOperationException(
            $"All {candidates.Count} candidate model(s) failed. Last error: {lastError?.Message}",
            lastError);
    }

    /// <inheritdoc />
    public Task ClearSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _contextManager.Clear(sessionId);
        return Task.CompletedTask;
    }

    private PreparedMessages PrepareMessages(ChatRequest request)
    {
        if (request.Messages.Count == 0)
        {
            throw new ArgumentException("The request must contain at least one message.", nameof(request));
        }

        ChatMessage userMessage = request.Messages[^1];
        if (!string.Equals(userMessage.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The last message must be from the user.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            // Stateless request: use the provided messages as-is, prepending the system prompt.
            var messages = new List<ChatMessage>(request.Messages.Count + 1);
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                messages.Add(ChatMessage.System(request.SystemPrompt));
            }

            messages.AddRange(request.Messages);
            return new PreparedMessages(messages, userMessage, null);
        }

        IReadOnlyList<ChatMessage> contextual = _contextManager.BuildMessages(
            request.SessionId, request.SystemPrompt, userMessage);
        return new PreparedMessages(contextual, userMessage, request.SessionId);
    }

    private static Exception BuildNoCandidateException(ChatRequest request)
        => string.IsNullOrWhiteSpace(request.Model)
            ? new InvalidOperationException("No AI model is currently available. Start a local model (e.g. Ollama) or configure a cloud provider.")
            : new InvalidOperationException($"The requested model '{request.Model}' is unknown or its provider is unavailable.");
}

/// <summary>Messages prepared for the providers plus the session/user message used for context.</summary>
internal sealed record PreparedMessages(
    IReadOnlyList<ChatMessage> Messages,
    ChatMessage UserMessage,
    string? SessionId);
