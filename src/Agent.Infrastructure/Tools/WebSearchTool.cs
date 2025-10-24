using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Agent.Core.Tools;

namespace Agent.Infrastructure.Tools;

public sealed class WebSearchTool : ITool
{
    private readonly HttpClient _http;
    public string Name => "WebSearch";
    public string Description =>
        "Searches the web for information. Use this tool whenever the user asks for recent or factual data.";

    public WebSearchTool(HttpClient http) => _http = http;

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var q = HttpUtility.UrlEncode(input);
        var url = $"https://api.duckduckgo.com/?q={q}&format=json&no_html=1&skip_disambig=1";

        try
        {
            var json = await _http.GetFromJsonAsync<JsonElement>(url, ct);

            var results = new List<string>();

            // Try "AbstractText" (DuckDuckGo instant answer)
            if (json.TryGetProperty("AbstractText", out var abs) && !string.IsNullOrWhiteSpace(abs.GetString()))
            {
                results.Add(abs.GetString()!);
            }

            // Try "RelatedTopics"
            if (json.TryGetProperty("RelatedTopics", out var topics))
            {
                foreach (var t in topics.EnumerateArray())
                {
                    if (t.TryGetProperty("Text", out var txt) && !string.IsNullOrWhiteSpace(txt.GetString()))
                        results.Add(txt.GetString()!);
                }
            }

            // Return combined response or at least the search link
            if (results.Count > 0)
            {
                return $"Top web results for '{input}':\n• " + string.Join("\n• ", results.Take(5));
            }

            return $"I couldn't extract snippets, but here’s a direct search link: https://duckduckgo.com/?q={q}";
        }
        catch (Exception ex)
        {
            return $"WebSearch error: {ex.Message}. Open: https://duckduckgo.com/?q={q}";
        }
    }
}
