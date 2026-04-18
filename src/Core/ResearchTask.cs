namespace TeamFlow.Core;

public record ResearchTask(
    Guid Id,
    string Prompt,
    DateTimeOffset CreatedAt,
    string? Kind = null,
    string? Answer = null,
    IReadOnlyList<TaskFinding>? Findings = null);

public sealed record TaskFinding(string Claim, string SourceUrl, double Confidence);
