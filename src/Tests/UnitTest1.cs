using TeamFlow.Core;
using TeamFlow.Infrastructure;

namespace TeamFlow.Tests;

public class InMemoryTaskRepositoryTests
{
    [Fact]
    public async Task Add_then_Get_round_trips_the_task()
    {
        var repo = new InMemoryTaskRepository();
        var task = new ResearchTask(Guid.NewGuid(), "what is the current price of gold", DateTimeOffset.UtcNow);

        await repo.AddAsync(task);
        var fetched = await repo.GetAsync(task.Id);

        Assert.Equal(task, fetched);
    }

    [Fact]
    public async Task Get_returns_null_for_unknown_id()
    {
        var repo = new InMemoryTaskRepository();
        var fetched = await repo.GetAsync(Guid.NewGuid());
        Assert.Null(fetched);
    }
}
