using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TeamFlow.Agents;

public sealed class ResearchAgentFactory
{
    private readonly ResearchOptions _research;
    private readonly OpenAIOptions _openAI;
    private readonly WebSearchPlugin _plugin;
    private readonly ILoggerFactory _logs;
    private readonly string _instructions;

    public ResearchAgentFactory(
        IOptions<ResearchOptions> research,
        IOptions<OpenAIOptions> openAI,
        WebSearchPlugin plugin,
        ILoggerFactory logs)
    {
        _research = research.Value;
        _openAI = openAI.Value;
        _plugin = plugin;
        _logs = logs;
        _instructions = PromptLoader.Load(_research.PromptsDirectory, "research", _research.PromptVersion);
    }

    public string PromptVersion => _research.PromptVersion;

    /// <summary>
    /// Creates a fresh ResearchAgent plus the budget filter that caps its tool calls.
    /// Caller should invoke the agent once, then discard — kernel and filter are per-task.
    /// </summary>
    public (ChatCompletionAgent Agent, ToolCallBudgetFilter Budget) Create()
    {
        if (string.IsNullOrWhiteSpace(_openAI.ApiKey))
            throw new InvalidOperationException(
                "OpenAI:ApiKey is not configured. Set it via `dotnet user-secrets set OpenAI:ApiKey <key>` in src/Api.");

        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId: _openAI.Model, apiKey: _openAI.ApiKey)
            .Build();

        kernel.Plugins.AddFromObject(_plugin, "web");

        var budget = new ToolCallBudgetFilter(_research.MaxToolCalls, _logs.CreateLogger<ToolCallBudgetFilter>());
        kernel.AutoFunctionInvocationFilters.Add(budget);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            ResponseFormat = typeof(ResearchResult),
            Temperature = 0.2,
        };

        var agent = new ChatCompletionAgent
        {
            Name = "ResearchAgent",
            Instructions = _instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(settings),
        };

        return (agent, budget);
    }
}
