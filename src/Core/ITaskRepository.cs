namespace TeamFlow.Core;

public interface ITaskRepository
{
    Task<ResearchTask> AddAsync(ResearchTask task, CancellationToken ct = default);
    Task<ResearchTask?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ResearchTask?> UpdateAsync(ResearchTask task, CancellationToken ct = default);
}
