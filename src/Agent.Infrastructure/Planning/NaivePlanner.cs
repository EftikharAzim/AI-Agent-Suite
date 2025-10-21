using Agent.Core.LLM;
using Agent.Core.Planning;
using Agent.Core.Tools;

namespace Agent.Infrastructure.Planning;

public sealed class NaivePlanner : IPlanner
{
    public Task<Plan> CreatePlanAsync(IReadOnlyList<ChatMessage> history, IToolRegistry tools, CancellationToken ct = default)
    {
        // M1: baseline — no real planning; just one step "answer" with no tool call.
        var steps = new List<PlanStep>();
        var rationale = "Baseline planner: no tools used; answer directly via the model.";
        return Task.FromResult(new Plan(steps, rationale));
    }
}
