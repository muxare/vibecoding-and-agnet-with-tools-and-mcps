using System.Collections.Concurrent;
using TeamFlow.Core;

namespace TeamFlow.Infrastructure;

public sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly ConcurrentDictionary<Guid, ResearchTask> _store = new();

    public Task<ResearchTask> AddAsync(ResearchTask task, CancellationToken ct = default)
    {
        _store[task.Id] = task;
        return Task.FromResult(task);
    }

    public Task<ResearchTask?> GetAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var task);
        return Task.FromResult(task);
    }

    public Task<ResearchTask?> UpdateAsync(ResearchTask task, CancellationToken ct = default)
    {
        if (!_store.ContainsKey(task.Id)) return Task.FromResult<ResearchTask?>(null);
        _store[task.Id] = task;
        return Task.FromResult<ResearchTask?>(task);
    }
}
