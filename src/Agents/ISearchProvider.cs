namespace TeamFlow.Agents;

public interface ISearchProvider
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int maxResults, CancellationToken ct = default);
}

public sealed record SearchHit(string Title, string Url, string Snippet);
