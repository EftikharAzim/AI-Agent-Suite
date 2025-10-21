using Agent.Core.Memory;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Agent.Infrastructure.Memory;

public sealed class QdrantMemoryStore : IMemoryStore
{
    private readonly QdrantClient _client;
    private readonly IEmbeddingGenerator _embedder;
    private readonly string _collection;

    public QdrantMemoryStore(QdrantClient client, IEmbeddingGenerator embedder, string collection = "agent_memory")
    {
        _client = client;
        _embedder = embedder;
        _collection = collection;
    }

    public async Task UpsertAsync(MemoryRecord record, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);

        var vector = (record.Embedding is { Length: > 0 })
        ? record.Embedding
        : await _embedder.GenerateAsync(record.Text, ct);

        await _client.UpsertAsync(
            _collection,
            points: new[]
            {
                new PointStruct
                {
                    Id = new PointId {Uuid = record.Id },
                    Vectors = vector,
                    Payload = { ["text"] = record.Text, ["metadata"] = record.Metadata ?? "" }
                }
            },
            cancellationToken: ct
        );
    }

    public async Task<IReadOnlyList<MemoryRecord>> SearchAsync(string queryText, int limit = 5, double minScore = 0.6, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(ct);
        var vector = await _embedder.GenerateAsync(queryText, ct);

        var result = await _client.SearchAsync(
            _collection,
            vector: vector,
            limit: (ulong)limit,
            scoreThreshold: (float)minScore,
            cancellationToken: ct
        );

        return result.Select(hit =>
            new MemoryRecord(
                hit.Id.Uuid ?? hit.Id.Num.ToString(),
                hit.Payload.TryGetValue("text", out var t) ? t.StringValue ?? "" : "",
                hit.Vectors?.Vector?.Data?.ToArray() ?? Array.Empty<float>(),
                hit.Payload.TryGetValue("metadata", out var md) ? md.StringValue ?? "" : "")
        ).ToList();
    }

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        var collections = await _client.ListCollectionsAsync(ct);
        if (!collections.Contains(_collection))
        {
            await _client.CreateCollectionAsync(
                _collection,
                new VectorParams { Size = (uint)_embedder.Dimension, Distance = Distance.Cosine },
                cancellationToken: ct
            );
        }
    }
}
