using Agent.Core.Conversations;
using Agent.Core.LLM;
using Agent.Core.Planning;
using Agent.Core.Tools;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Agent.Application.Agent;

public sealed class AgentExecutor
{
    private readonly IConversationStore _store;
    private readonly IPlanner _planner;
    private readonly IToolRegistry _tools;
    private readonly IChatModel _llm;
    private readonly ILogger<AgentExecutor> _logger;

    public AgentExecutor(
        IConversationStore store,
        IPlanner planner,
        IToolRegistry tools,
        IChatModel llm,
        ILogger<AgentExecutor> logger)
    {
        _store = store;
        _planner = planner;
        _tools = tools;
        _llm = llm;
        _logger = logger;
    }

    public async Task<string> HandleAsync(string sessionId, string userInput, CancellationToken ct = default)
    {
        // Save user message
        await _store.AppendAsync(sessionId, new ChatMessage(AuthorRole.User, userInput), ct);

        // Load conversation history
        var history = await _store.LoadAsync(sessionId, ct);

        // STEP 1: ask planner for structured plan
        var plan = await _planner.CreatePlanAsync(history, _tools, ct);
        _logger.LogInformation("Plan created with {StepCount} steps", plan.Steps.Count);

        string? finalResponse = null;

        try
        {
            if (plan?.Steps is null || plan.Steps.Count == 0)
            {
                finalResponse = "I couldn't determine an action plan.";
                await _store.AppendAsync(sessionId, new ChatMessage(AuthorRole.Assistant, finalResponse), ct);
                return finalResponse;
            }

            var toolOutputs = new List<string>();

            foreach (var step in plan.Steps)
            {
                _logger.LogInformation("Executing tool: {Tool} (input: {Input})", step.Tool, step.Input);

                if (_tools.TryGet(step.Tool, out var tool))
                {
                    var sw = Stopwatch.StartNew();
                    var output = await tool.ExecuteAsync(step.Input, ct);
                    sw.Stop();

                    _logger.LogInformation("[{Tool}] finished in {Elapsed} ms", step.Tool, sw.ElapsedMilliseconds);

                    var block = $"""
                    [Tool: {step.Tool}]
                    {output}
                    [/Tool]
                    """;

                    toolOutputs.Add(block);

                    // persist raw tool result for context
                    await _store.AppendAsync(sessionId, new ChatMessage(AuthorRole.Tool, block, step.Tool), ct);
                }
                else
                {
                    _logger.LogWarning("Unknown tool: {Tool}", step.Tool);
                    toolOutputs.Add($"[Unknown tool: {step.Tool}]");
                }
            }

            if (toolOutputs.Count == 0)
            {
                finalResponse = "No tools were executed.";
            }
            else
            {
                var context = string.Join("\n\n", toolOutputs);

                var systemPrompt = """
                You are an AI assistant that uses external tools such as WebSearch, Calendar, and Drive.
                When a tool result is provided, assume it contains real information from the outside world.

                Never apologize or say you cannot access the internet — that has already been handled by the tools.
                If the WebSearch tool returns a link, summarize or include it helpfully.
                If you get bullet points, summarize them clearly and concisely.
                Always provide a confident, factual answer based on tool data.
                """;

                var messagesForCompletion = new List<ChatMessage>(history)
                {
                    new ChatMessage(AuthorRole.System, systemPrompt),
                    new ChatMessage(AuthorRole.User, context)
                };

                var response = await _llm.CompleteAsync(messagesForCompletion, ct);
                finalResponse = response.Text;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in AgentExecutor");
            finalResponse = "Unexpected error occurred.";
        }

        await _store.AppendAsync(sessionId, new ChatMessage(AuthorRole.Assistant, finalResponse ?? ""), ct);
        return finalResponse ?? "";
    }
}
