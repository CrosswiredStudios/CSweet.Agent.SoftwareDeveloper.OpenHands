using CSweet.Agent.SDK;
using CSweet.Agent.Development.Phases.Onboarding;

namespace CSweet.Agent.Development;

public abstract partial class DevelopmentAgentBase
{
    protected override Task OnOnboardedAsync(
        AgentOnboardedEvent onboarded,
        AgentEventEnvelope message,
        AgentRuntimeContext context,
        CancellationToken cancellationToken) =>
        OnboardingService.HandleAsync(message, context, cancellationToken);
}
