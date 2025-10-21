using Agent.Core.LLM;
using Agent.Core.Planning;
using Agent.Core.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Infrastructure.Planning;

public sealed class SkPlanner : IPlanner
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chat;

    public SkPlanner(Kernel kernel)
    {
        _kernel = kernel;
        _chat = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<Plan> CreatePlanAsync(
        IReadOnlyList<ChatMessage> history,
        IToolRegistry tools,
        CancellationToken ct = default)
    {
        // 1) Build a tool inventory string for the prompt
        var toolList = string.Join("\n", tools.All.Select(t => $"- {t.Name}: {t.Description}"));

        // 2) Build SK ChatHistory from our history
        var chat = new ChatHistory();
        chat.AddSystemMessage(@"
            You are a planning assistant. Given a conversation and a list of available tools,
            produce a minimal, ordered plan (JSON) to solve the user's latest request.

            Rules:
            - Use only tools from the provided list.
            - If no tool is needed, return an empty array for steps.
            - Be precise and concise.
            - Output valid JSON ONLY. No commentary outside JSON.

            JSON schema:
            {
              ""rationale"": ""string"",
              ""steps"": [
                { ""tool"": ""ToolName"", ""input"": ""string"" }
              ]
            }
        ");

        // Fix: Map Agent.Core.LLM.AuthorRole to Microsoft.SemanticKernel.ChatCompletion.AuthorRole
        foreach (var m in history)
        {
            var skRole = m.Role switch
            {
                Agent.Core.LLM.AuthorRole.System => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System,
                Agent.Core.LLM.AuthorRole.User => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User,
                Agent.Core.LLM.AuthorRole.Assistant => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant,
                Agent.Core.LLM.AuthorRole.Tool => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant, // tool results are treated as assistant context
                _ => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User
            };
            chat.AddMessage(skRole, m.Content);
        }

        // 3) Add tool inventory and the instruction to output JSON
        chat.AddUserMessage($@"
            Available tools:
            {toolList}

            Return only JSON as specified.");

        // 4) Call SK chat completion (delegates to your local model via our adapter)
        var response = await _chat.GetChatMessageContentAsync(chat, kernel: _kernel, cancellationToken: ct);
        var text = response.Content?.Trim() ?? "{}";

        // 5) Parse robustly
        var json = ExtractJson(text); // in case model returns stray text
        var planDto = JsonSerializer.Deserialize<PlanDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new PlanDto { Rationale = "Empty plan", Steps = [] };

        // 6) Map
        var steps = (planDto.Steps ?? []).Select(s => new PlanStep(s.Tool ?? "", s.Input ?? "")).ToList();
        var rationale = planDto.Rationale ?? "No rationale.";
        return new Plan(steps, rationale);
    }

    private static string ExtractJson(string s)
    {
        // Be forgiving: find first '{' and last '}'.
        var start = s.IndexOf('{');
        var end = s.LastIndexOf('}');
        if (start >= 0 && end > start) return s[start..(end + 1)];
        return "{}";
    }

    private sealed class PlanDto
    {
        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }

        [JsonPropertyName("steps")]
        public List<StepDto>? Steps { get; set; }
    }

    private sealed class StepDto
    {
        [JsonPropertyName("tool")]
        public string? Tool { get; set; }

        [JsonPropertyName("input")]
        public string? Input { get; set; }
    }
}
