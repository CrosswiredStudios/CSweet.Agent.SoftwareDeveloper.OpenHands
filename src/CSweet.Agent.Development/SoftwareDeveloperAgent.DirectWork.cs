using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.Development;

public abstract partial class DevelopmentAgentBase
{
    internal const string DirectWorkMarker = DevelopmentStateStore.DirectWorkMarker;

    private Task HandleDirectWorkMessageAsync(
        Guid chatId, CommunicationMessage source, IReadOnlyList<CommunicationMessage> history,
        CommunicationMessageReceivedEvent? received, AgentRuntimeContext context, CancellationToken ct) =>
        new DevelopmentIntakeService(
            Settings, new DevelopmentChatClientProvider(Settings, _llmClientFactory, _engine, _profile))
            .HandleDirectWorkMessageAsync(chatId, source, history, received, context, ct);

    private static bool IsDirectWork(PersonalTodoItem item) =>
        DevelopmentIntakeService.IsDirectWork(item);
}