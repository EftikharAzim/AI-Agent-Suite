using Agent.Core.Conversations;
using Agent.Core.LLM;
using System.Collections.Concurrent;

namespace Agent.Infrastructure.Conversations;

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _db = new();

    public Task AppendAsync(string sessionId, ChatMessage message, CancellationToken ct = default)
    {
        var list = _db.GetOrAdd(sessionId, _ => new List<ChatMessage>());
        lock (list) list.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatMessage>> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        _db.TryGetValue(sessionId, out var list);
        return Task.FromResult<IReadOnlyList<ChatMessage>>(list ?? new List<ChatMessage>());
    }
}
