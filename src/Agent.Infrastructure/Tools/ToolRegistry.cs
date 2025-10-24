using Agent.Core.Tools;
using System.Collections.Concurrent;

namespace Agent.Infrastructure.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    // NEW: load all tools when the registry is constructed
    public ToolRegistry(IEnumerable<ITool> tools)
    {
        foreach (var t in tools) Register(t);
    }

    public IEnumerable<ITool> All => _tools.Values;

    public void Register(ITool tool)
    {
        // main key
        _tools[tool.Name] = tool;

        // simple aliases (you can extend this)
        void alias(string a)
        {
            if (!string.IsNullOrWhiteSpace(a))
                _tools.TryAdd(a, tool);
        }

        // Common search aliases
        if (tool.Name.Equals("WebSearch", StringComparison.OrdinalIgnoreCase))
        {
            alias("SerpSearch");
            alias("WebSearch");
            alias("Google");
            alias("Search");
            alias("Internet search");
        }
        // Add more alias sets for future tools: Calendar, Drive, PdfQA, etc.
    }

    public bool TryGet(string name, out ITool tool)
    {
        // Defensive normalization
        name = (name ?? "").Trim();
        return _tools.TryGetValue(name, out tool!);
    }
}
