using Agent.Core.LLM;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Infrastructure.LLM;

public sealed class OpenAICompatibleChatModel : IChatModel
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly double _temperature;
    private readonly double _topP;

    public OpenAICompatibleChatModel(HttpClient http, string model, int maxTokens, double temperature, double topP)
    {
        _http = http;
        _model = model;
        _maxTokens = maxTokens;
        _temperature = temperature;
        _topP = topP;

        _json = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public string Name => _model;

    public async Task<IChatResponse> CompleteAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        var req = new ChatCompletionsRequest
        {
            Model = _model,
            MaxTokens = _maxTokens,
            Temperature = _temperature,
            TopP = _topP,
            Stream = false,
            Messages = history.Select(Map).ToList()
        };

        using var res = await _http.PostAsJsonAsync("/v1/chat/completions", req, _json, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_json, ct)
                   ?? throw new InvalidOperationException("Null response");

        var text = body.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

        var usage = body.Usage is null ? null : new Usage(
            body.Usage.PromptTokens,
            body.Usage.CompletionTokens
        );

        return new Resp(text, usage, new Dictionary<string, object>
        {
            ["provider"] = "openai-compatible",
            ["finishReason"] = body.Choices?.FirstOrDefault()?.FinishReason ?? ""
        });
    }

    public async Task<IChatStream> StreamAsync(IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
    {
        var req = new ChatCompletionsRequest
        {
            Model = _model,
            MaxTokens = _maxTokens,
            Temperature = _temperature,
            TopP = _topP,
            Stream = true,
            Messages = history.Select(Map).ToList()
        };

        var httpReq = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(req, _json), Encoding.UTF8, "application/json")
        };
        var res = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();

        var stream = await res.Content.ReadAsStreamAsync(ct);
        return new SseStream(stream);
    }

    private static OaiMessage Map(ChatMessage m) => new()
    {
        Role = m.Role switch
        {
            AuthorRole.System => "system",
            AuthorRole.User => "user",
            AuthorRole.Assistant => "assistant",
            AuthorRole.Tool => "tool",
            _ => "user"
        },
        Content = m.Content
    };

    // DTOs
    private sealed class ChatCompletionsRequest
    {
        public string Model { get; set; } = default!;
        public List<OaiMessage> Messages { get; set; } = new();
        public bool Stream { get; set; }
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public double? TopP { get; set; }
    }

    private sealed class OaiMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
    }

    private sealed class ChatCompletionsResponse
    {
        public string? Id { get; set; }
        public string? Object { get; set; }
        public List<Choice>? Choices { get; set; }
        public UsageDto? Usage { get; set; }
    }

    private sealed class Choice
    {
        public int Index { get; set; }
        public OaiMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private sealed class UsageDto
    {
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }
        public int? TotalTokens { get; set; }
    }

    private sealed record Usage(int? PromptTokens, int? CompletionTokens) : ITokenUsage;

    private sealed record Resp(string Text, ITokenUsage? Usage, IReadOnlyDictionary<string, object>? Raw) : IChatResponse;

    // Naive SSE reader for "data: {...}" lines
    private sealed class SseStream : IChatStream
    {
        private readonly Stream _stream;
        public SseStream(Stream stream) => _stream = stream;

        public IAsyncEnumerable<string> Tokens => Read();

        public async IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken ct = default)
        {
            await foreach (var token in Read(ct).WithCancellation(ct))
            {
                yield return token;
            }
        }

        // Replace the Read method in SseStream with the following implementation to fix CS1626

        public async IAsyncEnumerable<string> Read([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            using var reader = new StreamReader(_stream, Encoding.UTF8, false, leaveOpen: false);
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line is null) break;
                if (!line.StartsWith("data:")) continue;

                var json = line.AsSpan(5).Trim().ToString();
                if (json == "[DONE]") yield break;

                string? token = null;
                try
                {
                    var doc = JsonDocument.Parse(json);
                    var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var content))
                        token = content.GetString();
                }
                catch
                {
                    // ignore malformed chunks from some servers
                }

                if (token != null)
                    yield return token;
            }
        }

        public async ValueTask DisposeAsync() => await _stream.DisposeAsync();
    }
}
