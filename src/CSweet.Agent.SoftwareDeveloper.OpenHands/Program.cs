using CSweet.Agent.SDK;
using CSweet.Agent.Development;
using CSweet.Agent.SoftwareDeveloper.OpenHands;
using Microsoft.Extensions.Hosting;

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    var manifest = await AgentManifestLoader.LoadAsync("csweet-plugin.json", CancellationToken.None);
    var agent = new OpenHandsDeveloperAgent();
    var result = await new AgentTestRuntime().ExecuteCapabilityAsync(agent, AgentConfigurationCapabilities.Describe, new { });
    var valid = result.Succeeded && manifest.Id == agent.AgentId && manifest.Version == agent.Version &&
        manifest.Capabilities.Contains(DevelopmentCapabilities.Primary);
    Console.WriteLine(valid ? "OpenHands Developer manifest and configuration are valid." : "OpenHands Developer self-test failed.");
    Environment.ExitCode = valid ? 0 : 1;
    return;
}

var builder = Host.CreateApplicationBuilder(args);
var installed = await AgentManifestLoader.LoadAsync("csweet-plugin.json", CancellationToken.None);
if (installed.Id != OpenHandsDeveloperProfile.AgentId || installed.Version != OpenHandsDeveloperProfile.Version)
    throw new InvalidOperationException("Agent implementation and manifest identity differ.");
builder.AddCSweetAgent<OpenHandsDeveloperAgent>();
await builder.Build().RunAsync();
