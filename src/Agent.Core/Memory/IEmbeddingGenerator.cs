namespace Agent.Core.Memory;

public interface IEmbeddingGenerator
{
    int Dimension { get; }
    Task<float[]> GenerateAsync(string text, CancellationToken ct = default);
}
