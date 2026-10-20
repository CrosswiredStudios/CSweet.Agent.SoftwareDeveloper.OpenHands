using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.Development;

public abstract partial class DevelopmentAgentBase
{
    internal Task<PersonalWorkPlan> PrepareDevelopmentPlanAsync(
        PersonalTodoItem item, AgentRuntimeContext context, CancellationToken cancellationToken) =>
        new PersonalDevelopmentService(
            Settings,
            new DevelopmentChatClientProvider(Settings, _llmClientFactory, _engine, _profile))
            .PrepareDevelopmentPlanAsync(item, context, cancellationToken);
}
