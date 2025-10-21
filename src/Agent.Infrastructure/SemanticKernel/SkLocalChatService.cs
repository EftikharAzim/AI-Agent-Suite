using Agent.Core.LLM;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;

namespace Agent.Infrastructure.SemanticKernel;

/// <summary>
/// Minimal IChatCompletionService that delegates to your existing IChatModel.
/// Keeps SK decoupled from HTTP details; you already solved that in OpenAICompatibleChatModel.
/// </summary>
public sealed class SkLocalChatService : IChatCompletionService
{
    private readonly IChatModel _model;

    public SkLocalChatService(IChatModel model) => _model = model;

    public string ModelId => _model.Name;

    public IReadOnlyDictionary<string, object?> Attributes { get; } =
        new Dictionary<string, object?>();

    public async Task<ChatMessageContent> GetChatMessageContentAsync(
        ChatHistory chat,
        PromptExecutionSettings? settings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var history = Map(chat);
        var resp = await _model.CompleteAsync(history, cancellationToken);
        return new ChatMessageContent(Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant, resp.Text, ModelId);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chat,
        PromptExecutionSettings? settings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = Map(chat);
        await foreach (var token in (await _model.StreamAsync(history, cancellationToken)).Tokens.WithCancellation(cancellationToken))
        {
            yield return new StreamingChatMessageContent(Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant, token, ModelId);
        }
    }

    private static List<Agent.Core.LLM.ChatMessage> Map(Microsoft.SemanticKernel.ChatCompletion.ChatHistory chat)
    {
        var list = new List<Agent.Core.LLM.ChatMessage>();
        foreach (var m in chat)
        {
            Agent.Core.LLM.AuthorRole role;
            if (m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)
                role = Agent.Core.LLM.AuthorRole.System;
            else if (m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User)
                role = Agent.Core.LLM.AuthorRole.User;
            else if (m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant)
                role = Agent.Core.LLM.AuthorRole.Assistant;
            else
                role = Agent.Core.LLM.AuthorRole.User;

            list.Add(new Agent.Core.LLM.ChatMessage(role, m.Content ?? string.Empty));
        }
        return list;
    }
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var message = await GetChatMessageContentAsync(chatHistory, executionSettings, kernel, cancellationToken);
        return new List<ChatMessageContent> { message };
    }
}
