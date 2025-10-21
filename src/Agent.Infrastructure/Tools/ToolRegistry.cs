using Agent.Core.Tools;
using System.Collections.Concurrent;

namespace Agent.Infrastructure.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<ITool> All => _tools.Values;

    public void Register(ITool tool) => _tools[tool.Name] = tool;
    public bool TryGet(string name, out ITool tool) => _tools.TryGetValue(name, out tool!);
}
