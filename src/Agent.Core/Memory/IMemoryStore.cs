namespace Agent.Core.Memory;

public record MemoryRecord(string Id, string Text, float[] Embedding, string? Metadata = null);

public interface IMemoryStore
{
    Task UpsertAsync(MemoryRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryRecord>> SearchAsync(string queryText, int limit = 5, double minScore = 0.6, CancellationToken ct = default);
}
