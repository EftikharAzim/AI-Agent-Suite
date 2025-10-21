using System.Security.Cryptography;
using System.Text;
using Agent.Core.Memory;

namespace Agent.Infrastructure.Memory;

public sealed class SimpleHashEmbeddingGenerator : IEmbeddingGenerator
{
    public int Dimension { get; }

    public SimpleHashEmbeddingGenerator(int dimension = 384) => Dimension = dimension;

    public Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        var vec = new float[Dimension];
        for (int i = 0; i < vec.Length; i++)
            vec[i] = hash[i % hash.Length] / 255f;
        return Task.FromResult(vec);
    }
}
