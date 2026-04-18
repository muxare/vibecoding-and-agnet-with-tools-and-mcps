namespace TeamFlow.Agents;

public interface ITriageClassifier
{
    Task<TriageResult> ClassifyAsync(string prompt, CancellationToken ct = default);
}
