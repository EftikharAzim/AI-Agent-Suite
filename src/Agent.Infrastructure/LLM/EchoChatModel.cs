using Agent.Core.LLM;

namespace Agent.Infrastructure.LLM;

public sealed class EchoChatModel : IChatModel
{
    public string Name => "echo";
    public Task<IChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        var last = history.LastOrDefault()?.Content ?? "(empty)";
        return Task.FromResult<IChatResponse>(new Resp($"[echo] {last}", null, null));
    }

    public Task<IChatStream> StreamAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        var text = $"[echo-stream] {history.LastOrDefault()?.Content ?? "(empty)"}";
        return Task.FromResult<IChatStream>(new EchoStream(text));
    }

    private sealed record Resp(string Text, ITokenUsage? Usage, IReadOnlyDictionary<string, object>? Raw) : IChatResponse;
    private sealed class EchoStream : IChatStream
    {
        private readonly string _text;
        public EchoStream(string text) => _text = text;
        public IAsyncEnumerable<string> Tokens => Stream();

        // Explicit implementation for IAsyncEnumerable<string>
        IAsyncEnumerator<string> IAsyncEnumerable<string>.GetAsyncEnumerator(CancellationToken cancellationToken)
            => GetAsyncEnumerator(cancellationToken);

        public async IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var ch in _text) { yield return ch.ToString(); await Task.Delay(5, cancellationToken); }
        }
        public IAsyncEnumerator<string> GetAsyncEnumerator() => GetAsyncEnumerator(default);
        public IAsyncEnumerable<string> Stream() => this;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
