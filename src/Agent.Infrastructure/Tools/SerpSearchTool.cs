using Agent.Core.Tools;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agent.Infrastructure.Tools;

public sealed class SerpSearchTool : ITool
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public string Name => "SerpSearch";
    public string Description =>
        "Search the web via SerpApi (Google) for global and regional results.";

    public SerpSearchTool(HttpClient http, string apiKey)
    {
        _http = http;
        //_apiKey = cfg["SerpApi:ApiKey"]
            //?? throw new ArgumentNullException("SerpApi:ApiKey missing");
        _apiKey = apiKey;
    }

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var query = Uri.EscapeDataString(input);
        // specify Bangladesh domain & gl
        var url = $"https://serpapi.com/search.json?engine=google&q={query}&gl=bd&google_domain=google.com.bd&api_key={_apiKey}&num=5";

        var json = await _http.GetFromJsonAsync<JsonElement>(url, ct);
        // parse first few organic results
        var results = new List<string>();
        if (json.TryGetProperty("organic_results", out var orgs))
        {
            foreach (var r in orgs.EnumerateArray().Take(5))
            {
                if (r.TryGetProperty("title", out var title)
                    && r.TryGetProperty("link", out var link))
                {
                    results.Add($"{title.GetString()} — {link.GetString()}");
                }
            }
        }
        if (results.Count == 0)
        {
            return $"No good results found. See: https://serpapi.com/search?q={query}&gl=bd";
        }
        return "Top results:\n• " + string.Join("\n• ", results);
    }
}
