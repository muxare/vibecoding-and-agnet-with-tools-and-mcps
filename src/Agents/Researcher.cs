using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TeamFlow.Agents;

public sealed class Researcher : IResearcher
{
    private readonly ResearchAgentFactory _factory;
    private readonly ILogger<Researcher> _log;

    public Researcher(ResearchAgentFactory factory, ILogger<Researcher> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task<ResearchResult> ResearchAsync(string prompt, CancellationToken ct = default)
    {
        var (agent, budget) = _factory.Create();

        string? content = null;
        await foreach (var item in agent.InvokeAsync(prompt, cancellationToken: ct))
        {
            content = item.Message.Content;
        }

        _log.LogInformation(
            "Research agent finished using prompt {PromptVersion} with toolCalls={ToolCalls}/{MaxToolCalls}",
            _factory.PromptVersion, budget.Count, budget.Max);

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("ResearchAgent returned empty content.");

        var result = JsonSerializer.Deserialize<ResearchResult>(content!)
            ?? throw new InvalidOperationException("ResearchAgent returned non-JSON content: " + content);

        return result;
    }
}
