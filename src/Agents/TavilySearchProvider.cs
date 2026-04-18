using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TeamFlow.Agents;

public sealed class TavilySearchProvider : ISearchProvider
{
    private readonly HttpClient _http;
    private readonly ResearchOptions _options;

    public TavilySearchProvider(HttpClient http, IOptions<ResearchOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.TavilyApiKey))
            throw new InvalidOperationException(
                "Research:TavilyApiKey is not configured. Set it via `dotnet user-secrets set Research:TavilyApiKey <key>` in src/Api.");

        var body = new
        {
            api_key = _options.TavilyApiKey,
            query,
            max_results = Math.Clamp(maxResults, 1, 10),
            search_depth = "basic",
        };

        using var resp = await _http.PostAsJsonAsync("https://api.tavily.com/search", body, ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<TavilyResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Tavily returned empty response.");

        return payload.Results
            .Select(r => new SearchHit(r.Title ?? "", r.Url ?? "", r.Content ?? ""))
            .ToList();
    }

    private sealed class TavilyResponse
    {
        [JsonPropertyName("results")]
        public List<TavilyHit> Results { get; set; } = new();
    }

    private sealed class TavilyHit
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
