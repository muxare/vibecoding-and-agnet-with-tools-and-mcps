using Serilog;
using Serilog.Context;
using TeamFlow.Api;
using TeamFlow.Core;
using TeamFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapGet("/", () => "TeamFlow API — Phase 0");

app.MapPost("/tasks", async (CreateTaskRequest req, ITaskRepository repo, ILogger<Program> log) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest(new { error = "prompt is required" });

    var task = new ResearchTask(Guid.NewGuid(), req.Prompt, DateTimeOffset.UtcNow);
    using (LogContext.PushProperty("taskId", task.Id))
    {
        await repo.AddAsync(task);
        log.LogInformation("Task created with prompt length {PromptLength}", task.Prompt.Length);
    }
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapGet("/tasks/{id:guid}", async (Guid id, ITaskRepository repo, ILogger<Program> log) =>
{
    using (LogContext.PushProperty("taskId", id))
    {
        var task = await repo.GetAsync(id);
        if (task is null)
        {
            log.LogInformation("Task not found");
            return Results.NotFound();
        }
        return Results.Ok(task);
    }
});

app.Run();

namespace TeamFlow.Api
{
    public record CreateTaskRequest(string Prompt);
}
