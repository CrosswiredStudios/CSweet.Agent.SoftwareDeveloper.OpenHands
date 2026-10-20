using System.Text.Json;
using CSweet.Agent.Development;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Agents.AI.Tools.Shell;
namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;
public sealed class WorkspacePolicyRecoveryTests
{
    [Fact]
    public async Task Consecutive_policy_denials_are_bounded_and_allowed_work_resets_the_count()
    {
        var inner = new PolicySpyShell();
        await using var shell = new BoundedWorkspaceShell(inner, TimeSpan.FromSeconds(1),
            policy: DevelopmentExecution.CreateShellPolicy(), workingDirectory: Path.GetTempPath());
        var stopped = 0;
        shell.StopOnFailure = () => stopped++;
        for (var cycle = 0; cycle < 2; cycle++)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var denial = await Assert.ThrowsAsync<WorkspaceCommandPolicyException>(() => shell.RunAsync("cat /proc/cpuinfo"));
                Assert.False(denial.RecoveryExhausted);
            }
            if (cycle == 0) await shell.RunAsync("npm test");
        }
        var exhausted = await Assert.ThrowsAsync<WorkspaceCommandPolicyException>(() => shell.RunAsync("cat /sys/class/drm"));
        Assert.True(exhausted.RecoveryExhausted);
        Assert.True(DevelopmentFailurePolicy.IsOperational(exhausted));
        Assert.Same(exhausted, shell.Failure);
        Assert.Equal(1, stopped);
        await Assert.ThrowsAsync<WorkspaceCommandPolicyException>(() => shell.RunAsync("npm test"));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Policy_denial_is_actionable_tool_data_without_execution_or_validation_receipts()
    {
        var root = Path.Combine(Path.GetTempPath(), "csweet-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var reports = new List<JsonElement>();
            var progress = new WorkspaceProgress(Guid.NewGuid(), (value, _) =>
            { reports.Add(JsonSerializer.SerializeToElement(value)); return Task.CompletedTask; }, NullLogger.Instance, workspacePath: root);
            await using var shell = DevelopmentExecution.CreateShell(root);
            var preparations = 0;
            shell.BeforeCommand = (_, _) => { preparations++; return Task.CompletedTask; };
            const string command = "cat /sys/class/dmi/id/product_name # private-command-marker";
            var response = await progress.ShellTool(shell).InvokeAsync(new AIFunctionArguments(
                new Dictionary<string, object?> { ["command"] = command }));
            var text = Assert.IsType<JsonElement>(response).GetString();
            Assert.Contains("workspace.command.policy_denied", text);
            Assert.Contains("No process was started", text);
            Assert.Contains("Do not retry", text);
            Assert.Contains("ticket revision", text);
            Assert.DoesNotContain("private-command-marker", text);
            Assert.Equal(0, preparations);
            Assert.Null(shell.Failure);
            Assert.Empty(progress.Validations.Summary(root).Validations);
            Assert.Equal("denied", reports[^1].GetProperty("phase").GetString());
            Assert.Equal(JsonValueKind.Null, reports[^1].GetProperty("exitCode").ValueKind);
            Assert.DoesNotContain("private-command-marker", JsonSerializer.Serialize(reports));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class PolicySpyShell : ShellExecutor
    {
        public int Calls;
        public override Task<ShellResult> RunAsync(string command, CancellationToken token = default)
        { Calls++; return Task.FromResult(new ShellResult("", "", 0, TimeSpan.Zero, false)); }
        public override AIFunction AsAIFunction(string name = "run_shell", string? description = null, bool requireApproval = true) => throw new NotSupportedException();
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

}
