namespace TeamFlow.Agents;

public interface IResearcher
{
    Task<ResearchResult> ResearchAsync(string prompt, CancellationToken ct = default);
}
