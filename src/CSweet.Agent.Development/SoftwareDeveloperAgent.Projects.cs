using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.Development;

public abstract partial class DevelopmentAgentBase
{
    private ProjectIntakeService ProjectIntake() =>
        new(new DevelopmentChatClientProvider(Settings, _llmClientFactory, _engine, _profile));

    private Task ResumeProjectIntakeAsync(ProjectIntakeSummary intake, AgentRuntimeContext context,
        CancellationToken ct) =>
        ProjectIntake().ResumeProjectIntakeAsync(intake, context, ct);
}