using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TeamFlow.Agents;

public sealed class TriageAgentFactory
{
    private readonly TriageOptions _triage;
    private readonly OpenAIOptions _openAI;
    private readonly string _instructions;

    public TriageAgentFactory(IOptions<TriageOptions> triage, IOptions<OpenAIOptions> openAI)
    {
        _triage = triage.Value;
        _openAI = openAI.Value;
        _instructions = PromptLoader.Load(_triage.PromptsDirectory, "triage", _triage.PromptVersion);
    }

    public string PromptVersion => _triage.PromptVersion;

    public ChatCompletionAgent Create()
    {
        if (string.IsNullOrWhiteSpace(_openAI.ApiKey))
            throw new InvalidOperationException(
                "OpenAI:ApiKey is not configured. Set it via user-secrets: " +
                "`dotnet user-secrets set OpenAI:ApiKey <key>` in src/Api.");

        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: _openAI.Model, apiKey: _openAI.ApiKey)
            .Build();

        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = typeof(TriageResult),
            Temperature = 0,
        };

        return new ChatCompletionAgent
        {
            Name = "TriageAgent",
            Instructions = _instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(settings),
        };
    }
}
