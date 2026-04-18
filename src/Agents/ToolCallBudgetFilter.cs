using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace TeamFlow.Agents;

/// <summary>
/// Enforces a per-task tool-call budget by terminating auto function calling
/// once <see cref="Max"/> invocations have been observed. One instance per
/// research invocation — it is not safe to share across tasks.
/// </summary>
public sealed class ToolCallBudgetFilter : IAutoFunctionInvocationFilter
{
    private readonly ILogger _log;
    public int Max { get; }
    public int Count { get; private set; }

    public ToolCallBudgetFilter(int max, ILogger log)
    {
        Max = max;
        _log = log;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        Count++;
        await next(context);

        if (Count >= Max)
        {
            _log.LogWarning(
                "Tool-call budget reached: {Count}/{Max}. Terminating auto function calling.",
                Count, Max);
            context.Terminate = true;
        }
    }
}
