namespace Jarvis.SDK.AI;

/// <summary>
/// Category of the task a chat request is solving. The model router uses this to pick the most
/// appropriate model (fast for simple tasks, more capable for reasoning, ...).
/// </summary>
public enum TaskKind
{
    /// <summary>Short questions, light conversation.</summary>
    Simple,

    /// <summary>Multi-step questions that benefit from a strong model.</summary>
    Complex,

    /// <summary>Step-by-step reasoning, analysis, planning.</summary>
    Reasoning,

    /// <summary>Code generation and code-related help.</summary>
    Coding,

    /// <summary>Condensing or summarizing existing content.</summary>
    Summarization,
}
