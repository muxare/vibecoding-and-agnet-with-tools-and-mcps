using System.Text.Json.Serialization;

namespace TeamFlow.Agents;

public sealed class Finding
{
    [JsonPropertyName("claim")]
    public string Claim { get; set; } = "";

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public sealed class ResearchResult
{
    [JsonPropertyName("answer")]
    public string Answer { get; set; } = "";

    [JsonPropertyName("findings")]
    public List<Finding> Findings { get; set; } = new();
}
