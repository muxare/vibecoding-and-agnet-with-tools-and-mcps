using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace TeamFlow.Agents;

public sealed class TriageClassifier : ITriageClassifier
{
    private readonly TriageAgentFactory _factory;
    private readonly ILogger<TriageClassifier> _log;

    public TriageClassifier(TriageAgentFactory factory, ILogger<TriageClassifier> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task<TriageResult> ClassifyAsync(string prompt, CancellationToken ct = default)
    {
        var agent = _factory.Create();

        string? content = null;
        await foreach (var item in agent.InvokeAsync(prompt, cancellationToken: ct))
        {
            content = item.Message.Content;
        }

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("TriageAgent returned empty content.");

        _log.LogInformation("Triage raw response using prompt {PromptVersion}: {Content}",
            _factory.PromptVersion, content);

        var result = JsonSerializer.Deserialize<TriageResult>(content!)
            ?? throw new InvalidOperationException("TriageAgent returned non-JSON content: " + content);

        if (result.Kind is not ("Simple" or "Complex"))
            throw new InvalidOperationException($"TriageAgent returned unexpected kind: '{result.Kind}'.");

        return result;
    }
}
