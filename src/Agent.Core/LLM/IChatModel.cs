namespace Agent.Core.LLM;

public enum AuthorRole { System, User, Assistant, Tool }

public record ChatMessage(AuthorRole Role, string Content, string? ToolName = null);

public interface ITokenUsage
{
    int? PromptTokens { get; }
    int? CompletionTokens { get; }
    int? TotalTokens => (PromptTokens ?? 0) + (CompletionTokens ?? 0);
}

public interface IChatResponse
{
    string Text { get; }
    ITokenUsage? Usage { get; }
    IReadOnlyDictionary<string, object>? Raw { get; } // provider diagnostics
}

public interface IChatStream : IAsyncEnumerable<string>, IAsyncDisposable
{
    IAsyncEnumerable<string> Tokens { get; } // alias for clarity
}

public interface IChatModel
{
    string Name { get; }
    Task<IChatResponse> CompleteAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);

    Task<IChatStream> StreamAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);
}
