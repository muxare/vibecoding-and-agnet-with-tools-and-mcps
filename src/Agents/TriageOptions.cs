namespace TeamFlow.Agents;

public sealed class TriageOptions
{
    public string PromptVersion { get; set; } = "v4";
    public string PromptsDirectory { get; set; } = "prompts";
}

public sealed class OpenAIOptions
{
    public string Model { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";
}
