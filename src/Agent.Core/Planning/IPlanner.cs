using Agent.Core.Tools;
using Agent.Core.LLM;

namespace Agent.Core.Planning;

public record PlanStep(string Tool, string Input);
public record Plan(IReadOnlyList<PlanStep> Steps, string NaturalLanguageRationale);

public interface IPlanner
{
    Task<Plan> CreatePlanAsync(
        IReadOnlyList<ChatMessage> history,
        IToolRegistry tools,
        CancellationToken ct = default);
}
