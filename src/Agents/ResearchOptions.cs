namespace TeamFlow.Agents;

public sealed class ResearchOptions
{
    public string PromptVersion { get; set; } = "v1";
    public string PromptsDirectory { get; set; } = "prompts";

    /// <summary>Tavily search API key (set via user-secrets).</summary>
    public string TavilyApiKey { get; set; } = "";

    /// <summary>Max search results returned to the model per search call.</summary>
    public int MaxSearchResults { get; set; } = 5;

    /// <summary>Max chars returned by a single fetch — prevents blowing the context window.</summary>
    public int MaxFetchChars { get; set; } = 8000;

    /// <summary>Tool-call budget per task. Starting guess; tune after seeing real traces.</summary>
    public int MaxToolCalls { get; set; } = 6;
}
