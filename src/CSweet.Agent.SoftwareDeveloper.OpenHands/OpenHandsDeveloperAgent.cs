using CSweet.Agent.Development;
using CSweet.Agent.OpenHands;
using CSweet.Agent.SDK;
using Microsoft.Extensions.Logging;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands;

public sealed class OpenHandsDeveloperAgent : DevelopmentAgentBase
{
    public OpenHandsDeveloperAgent() : base(OpenHandsDeveloperProfile.Profile, new OpenHandsExecutionEngine()) { }
    public OpenHandsDeveloperAgent(IAgentLlmClientFactory factory, IDevelopmentExecutionEngine? engine = null, ILogger? logger = null)
        : base(OpenHandsDeveloperProfile.Profile, engine ?? new OpenHandsExecutionEngine(), logger, factory) { }
}
