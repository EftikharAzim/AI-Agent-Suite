namespace Agent.Core.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(string input, CancellationToken ct = default);
}

public interface IToolRegistry
{
    void Register(ITool tool);
    bool TryGet(string name, out ITool tool);
    IEnumerable<ITool> All { get; }
}
