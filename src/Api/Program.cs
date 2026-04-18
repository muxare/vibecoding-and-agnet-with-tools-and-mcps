using Serilog;
using Serilog.Context;
using TeamFlow.Agents;
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

builder.Services.Configure<TriageOptions>(builder.Configuration.GetSection("Triage"));
builder.Services.Configure<ResearchOptions>(builder.Configuration.GetSection("Research"));
builder.Services.Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.AddSingleton<TriageAgentFactory>();
builder.Services.AddSingleton<ITriageClassifier, TriageClassifier>();

builder.Services.AddHttpClient<ISearchProvider, TavilySearchProvider>();
builder.Services.AddHttpClient<WebSearchPlugin>();
builder.Services.AddSingleton<ResearchAgentFactory>();
builder.Services.AddSingleton<IResearcher, Researcher>();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapGet("/", () => "TeamFlow API — Phase 1");

app.MapPost("/tasks", async (
    CreateTaskRequest req,
    ITaskRepository repo,
    ITriageClassifier triage,
    IResearcher researcher,
    ILogger<Program> log,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Prompt))
        return Results.BadRequest(new { error = "prompt is required" });

    var task = new ResearchTask(Guid.NewGuid(), req.Prompt, DateTimeOffset.UtcNow);
    using (LogContext.PushProperty("taskId", task.Id))
    {
        await repo.AddAsync(task, ct);
        log.LogInformation("Task created with prompt length {PromptLength}", task.Prompt.Length);

        try
        {
            var result = await triage.ClassifyAsync(task.Prompt, ct);
            task = task with { Kind = result.Kind };
            await repo.UpdateAsync(task, ct);
            log.LogInformation("Task classified as {Kind}: {Reasoning}", result.Kind, result.Reasoning);

            // Phase 2: intentionally ugly C# glue — "if Simple, call ResearchAgent".
            // Phase 4 will replace this with a handoff tool call. Do not elegant-ify.
            if (result.Kind == "Simple")
            {
                var research = await researcher.ResearchAsync(task.Prompt, ct);
                task = task with
                {
                    Answer = research.Answer,
                    Findings = research.Findings
                        .Select(f => new TaskFinding(f.Claim, f.SourceUrl, f.Confidence))
                        .ToList(),
                };
                await repo.UpdateAsync(task, ct);
                log.LogInformation(
                    "Research complete: findings={FindingCount} answerLength={AnswerLength}",
                    research.Findings.Count, research.Answer.Length);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Triage or research failed; task stored with whatever completed before the error");
        }
    }
    return Results.Created($"/tasks/{task.Id}", task);
});

app.MapGet("/tasks/{id:guid}", async (Guid id, ITaskRepository repo, ILogger<Program> log, CancellationToken ct) =>
{
    using (LogContext.PushProperty("taskId", id))
    {
        var task = await repo.GetAsync(id, ct);
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
