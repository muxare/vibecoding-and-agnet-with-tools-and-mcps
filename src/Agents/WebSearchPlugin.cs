using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace TeamFlow.Agents;

/// <summary>
/// Two tools the ResearchAgent can call: <c>search</c> and <c>fetch</c>.
///
/// The descriptions below are prompts the model reads. Treat them as such.
/// </summary>
public sealed class WebSearchPlugin
{
    private readonly ISearchProvider _search;
    private readonly HttpClient _http;
    private readonly ResearchOptions _options;
    private readonly ILogger<WebSearchPlugin> _log;

    public WebSearchPlugin(
        ISearchProvider search,
        HttpClient http,
        IOptions<ResearchOptions> options,
        ILogger<WebSearchPlugin> log)
    {
        _search = search;
        _http = http;
        _options = options.Value;
        _log = log;
    }

    [KernelFunction("search")]
    [Description(
        "Search the public web and return a ranked list of {title, url, snippet} hits. " +
        "Use this when you need up-to-date or external information to answer the task. " +
        "Prefer specific, high-signal queries over broad ones. " +
        "Call fetch(url) afterward only for the one or two hits that look most promising — " +
        "do not fetch every result.")]
    public async Task<string> SearchAsync(
        [Description("The search query. Keep it focused: 5–12 words, no quotes unless an exact phrase is required.")]
        string query,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var hits = await _search.SearchAsync(query, _options.MaxSearchResults, ct);
        sw.Stop();

        var sb = new StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            sb.AppendLine($"[{i + 1}] {h.Title}");
            sb.AppendLine(h.Url);
            sb.AppendLine(h.Snippet);
            sb.AppendLine();
        }
        var result = sb.ToString();

        _log.LogInformation(
            "Tool call {ToolName} query={Query} resultCount={ResultCount} resultSize={ResultSize} durationMs={DurationMs}",
            "search", query, hits.Count, result.Length, sw.ElapsedMilliseconds);

        return result;
    }

    [KernelFunction("fetch")]
    [Description(
        "Fetch the contents of a URL (typically one you got back from search) and return its text. " +
        "Output is truncated to a few thousand characters — do not assume you see the whole page. " +
        "Only call this when you need detail a search snippet does not provide.")]
    public async Task<string> FetchAsync(
        [Description("The absolute URL to fetch. Must start with http:// or https://.")]
        string url,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        string text;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("TeamFlow-Research/0.2");
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            text = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.LogWarning(ex,
                "Tool call {ToolName} url={Url} failed after durationMs={DurationMs}",
                "fetch", url, sw.ElapsedMilliseconds);
            return $"ERROR fetching {url}: {ex.Message}";
        }

        if (text.Length > _options.MaxFetchChars)
            text = text[.._options.MaxFetchChars] + "\n…[truncated]";
        sw.Stop();

        _log.LogInformation(
            "Tool call {ToolName} url={Url} resultSize={ResultSize} durationMs={DurationMs}",
            "fetch", url, text.Length, sw.ElapsedMilliseconds);

        return text;
    }
}
