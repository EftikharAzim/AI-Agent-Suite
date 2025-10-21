using Agent.Core.LLM;
using Agent.Core.Tools;
using Agent.Core.Planning;
using Agent.Core.Conversations;

namespace Agent.Application.Agent;

public sealed class AgentExecutor
{
    private readonly IChatModel _model;
    private readonly IPlanner _planner;
    private readonly IToolRegistry _tools;
    private readonly IConversationStore _store;

    public AgentExecutor(IChatModel model, IPlanner planner, IToolRegistry tools, IConversationStore store)
    {
        _model = model;
        _planner = planner;
        _tools = tools;
        _store = store;
    }

    public async Task<string> HandleAsync(string sessionId, string userInput, bool stream = false, CancellationToken ct = default)
    {
        await _store.AppendAsync(sessionId, new ChatMessage(AuthorRole.User, userInput), ct);

        var history = await _store.LoadAsync(sessionId, ct);

        // First pass (planner can be SK later)
        var plan = await _planner.CreatePlanAsync(history, _tools, ct);

        var outputs = new List<string>();
        foreach (var step in plan.Steps)
        {
            if (!_tools.TryGet(step.Tool, out var tool))
                outputs.Add($"[Planner chose unknown tool: {step.Tool}]");
            else
                outputs.Add(await tool.ExecuteAsync(step.Input, ct));
        }

        // Assistant summarizes results
        var assistantPrompt =
        $"""
        You are an assistant. Summarize tool results into a helpful answer.

        Plan Rationale: {plan.NaturalLanguageRationale}

        Tool Results:
        {string.Join("\n---\n", outputs)}
        """;

        var convo = new List<ChatMessage>(history)
        {
            new(AuthorRole.System, "You are a precise, helpful engineer."),
            new(AuthorRole.Assistant, "Planning complete. Summarizing results…"),
            new(AuthorRole.User, assistantPrompt)
        };

        var completion = await _model.CompleteAsync(convo, ct);
        await _store.AppendAsync(sessionId, new ChatMessage(AuthorRole.Assistant, completion.Text), ct);
        return completion.Text;
    }
}
