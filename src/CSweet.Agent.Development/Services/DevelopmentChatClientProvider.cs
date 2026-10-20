using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.Development;

internal sealed class DevelopmentChatClientProvider(
    AgentSettings settings,
    IAgentLlmClientFactory? llmClientFactory, IDevelopmentExecutionEngine engine, DevelopmentProfile profile)
{
    internal DevelopmentProfile Profile => profile;
    internal DevelopmentEngineSession CreateEngine(AgentRuntimeContext context, IChatClient client, DevelopmentEngineOptions options, string? sessionKey = null)
    {
        options.SessionKey = sessionKey;
        return new(engine, context, client, options, profile);
    }
    private readonly AgentSettings _settings = settings;
    private readonly IAgentLlmClientFactory? _llmClientFactory = llmClientFactory;
    internal async Task<IChatClient> CreateAsync(AgentRuntimeContext context, CancellationToken ct)
    {
        var provider = _settings.GetGuid("llmProviderId") ?? throw new OperationalDevelopmentException("Configure the developer's LLM provider.");
        var model = _settings.GetString("llmModel");
        if (string.IsNullOrWhiteSpace(model)) throw new OperationalDevelopmentException("Configure the developer's coding model.");
        var selection = new AgentLlmSelection(provider, model);
        try
        {
            return _llmClientFactory is null
                ? context.CreateChatClient(selection)
                : await _llmClientFactory.CreateChatClientAsync(selection, ct);
        }
        catch (InvalidOperationException error)
        {
            throw new OperationalDevelopmentException("The configured coding model is unavailable.", error);
        }
    }

}
