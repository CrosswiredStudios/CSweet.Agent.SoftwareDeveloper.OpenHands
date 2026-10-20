using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;
using CSweet.Agent.Development;

internal static class CalendarHarness
{
    internal static async Task<DevelopmentEngineOptions> ConfigureAsync(AgentRuntimeContext context,
        DevelopmentEngineOptions options, CancellationToken token)
    {
        options.ChatOptions = await context.Platform.Calendar.WithToolsAsync(options.ChatOptions ?? new ChatOptions(), token);
        return options;
    }
}
