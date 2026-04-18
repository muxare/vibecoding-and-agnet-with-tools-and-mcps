using System.Text.Json.Serialization;

namespace TeamFlow.Agents;

public sealed class TriageResult
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";
}
