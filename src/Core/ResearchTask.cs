namespace TeamFlow.Core;

public record ResearchTask(
    Guid Id,
    string Prompt,
    DateTimeOffset CreatedAt,
    string? Kind = null);
