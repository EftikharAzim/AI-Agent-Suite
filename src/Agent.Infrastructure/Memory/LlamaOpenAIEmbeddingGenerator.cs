using System.Net.Http.Json;
using System.Text.Json;
using Agent.Core.Memory;

namespace Agent.Infrastructure.Memory;

/// <summary>
/// Calls an OpenAI-compatible /v1/embeddings endpoint (e.g., llama.cpp / LocalAI).
/// </summary>
public sealed class LlamaOpenAIEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _dim;

    public LlamaOpenAIEmbeddingGenerator(HttpClient http, string model, int dimension)
    {
        _http = http;
        _model = model;
        _dim = dimension;
    }

    public int Dimension => _dim;

    public async Task<float[]> GenerateAsync(string text, CancellationToken ct = default)
    {
        var req = new
        {
            model = _model,
            input = text
        };

        using var res = await _http.PostAsJsonAsync("/v1/embeddings", req, cancellationToken: ct);
        res.EnsureSuccessStatusCode();

        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        // Expecting: { "data": [ { "embedding": [ ... floats ... ] } ] }
        var data = doc.GetProperty("data")[0].GetProperty("embedding");
        var arr = new float[data.GetArrayLength()];
        for (int i = 0; i < arr.Length; i++) arr[i] = (float)data[i].GetDouble();
        return arr;
    }
}
