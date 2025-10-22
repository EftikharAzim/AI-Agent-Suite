using Agent.Core.LLM;
using Agent.Core.Planning;
using Agent.Core.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;
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
        You are a planning assistant. Produce a SINGLE JSON object ONLY, no commentary.
        Output strictly one object that matches this schema:
        {
          ""rationale"": ""string"",
          ""steps"": [ { ""tool"": ""ToolName"", ""input"": ""string"" } ]
        }
        Do not write any explanations or extra text before or after the JSON.
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

        PlanDto planDto;

        try
        {
            var json = ExtractJson(text);
            Log.Information($"Extracted json = {json}");
            planDto = JsonSerializer.Deserialize<PlanDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
            }) ?? new PlanDto { Rationale = "Empty plan", Steps = [] };
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[Planner] JSON parse error: {ex.Message}\nRaw:\n{text}\n");
            planDto = new PlanDto { Rationale = "Invalid JSON", Steps = [] };
        }

        // 6) Map
        var steps = (planDto.Steps ?? []).Select(s => new PlanStep(s.Tool ?? "", s.Input ?? "")).ToList();
        var rationale = planDto.Rationale ?? "No rationale.";
        return new Plan(steps, rationale);
    }

    private static string ExtractJson(string text)
    {
        // Collect all complete JSON objects in the string
        var jsonBlocks = new List<string>();
        var depth = 0;
        var start = -1;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    jsonBlocks.Add(text[start..(i + 1)]);
                    start = -1;
                }
            }
        }

        if (jsonBlocks.Count == 0)
            return "{\"rationale\":\"No valid JSON found.\",\"steps\":[]}";

        // If multiple JSONs exist, take the LAST one — it's usually the "final" plan
        return jsonBlocks.Last();
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
