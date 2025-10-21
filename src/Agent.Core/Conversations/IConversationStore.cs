using Agent.Core.LLM;

namespace Agent.Core.Conversations;

public interface IConversationStore
{
    Task AppendAsync(string sessionId, ChatMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> LoadAsync(string sessionId, CancellationToken ct = default);
}
