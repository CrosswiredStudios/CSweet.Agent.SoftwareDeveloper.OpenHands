using CSweet.Agent.Development;
using CSweet.Agent.OpenHands;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class OpenHandsPolicyRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealWorkerReceivesDenialAndEitherContinuesOrRetainsSourceBeforeStopping(bool repeated)
    {
        var python = Environment.GetEnvironmentVariable("CSWEET_TEST_OPENHANDS_PYTHON")
            ?? throw new InvalidOperationException("The pinned worker test environment is required.");
        var root = Path.Combine(Path.GetTempPath(), "csweet-openhands-policy-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(source, ".csweet"));
        ExecutionCheckpoint? saved = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ExecutionCheckpointRead, ExecutionCheckpointReadResult>(ExecutionCheckpointCapabilities.Read,
                (_, _) => Task.FromResult(new ExecutionCheckpointReadResult(saved)))
            .RegisterCapability<ExecutionCheckpointWrite, ExecutionCheckpoint>(ExecutionCheckpointCapabilities.Write, (input, _) =>
            {
                saved = new(input.SessionKey, input.EngineVersion, input.ExpectedRevision + 1, input.EventCursor,
                    input.EngineArchive, "digest", SourceArchive: input.SourceArchive);
                return Task.FromResult(saved);
            });
        var calls = 0;
        using var client = new ModelBridgeTests.ScriptedClient((messages, _) =>
        {
            calls++;
            if (calls == 3)
                Assert.Contains("workspace.command.policy_denied", messages.SelectMany(x => x.Contents)
                    .OfType<FunctionResultContent>().Last().Result?.ToString());
            var call = calls switch
            {
                1 => new FunctionCallContent("edit", "file_editor", new Dictionary<string, object?>
                    { ["command"] = "create", ["path"] = Path.Combine(source, "partial.txt"), ["file_text"] = "retained source" }),
                _ when repeated || calls == 2 => new FunctionCallContent("deny-" + calls, "terminal",
                    new Dictionary<string, object?> { ["command"] = "cat /proc/cpuinfo" }),
                3 => new FunctionCallContent("outcome", "file_editor", new Dictionary<string, object?>
                    { ["command"] = "create", ["path"] = Path.Combine(source, ".csweet", "outcome.json"), ["file_text"] = "{}" }),
                _ => new FunctionCallContent("finish", "finish", new Dictionary<string, object?> { ["message"] = "Finished permitted work." })
            };
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [call]));
        });
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "src/CSweet.Agent.OpenHands"))) repository = repository.Parent;
        var engine = new OpenHandsExecutionEngine(new(python, Path.Combine(root, "runtime"),
            Path.Combine(repository!.FullName, "src/CSweet.Agent.OpenHands/worker")));
        await using var shell = DevelopmentExecution.CreateShell(source);
        var progress = new WorkspaceProgress(Guid.NewGuid(), (_, _) => Task.CompletedTask, NullLogger.Instance, workspacePath: source);
        var options = DevelopmentExecution.CreateOptions("OpenHands Developer", source, shell, null);
        options.ChatOptions!.Tools = [progress.ShellTool(shell)];
        var session = new DevelopmentEngineSession(engine, runtime.CreateContext(), client, options, OpenHandsDeveloperProfile.Profile);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var checkpoints = 0;
        try
        {
            var key = await session.CreateSessionAsync(timeout.Token);
            Task Run() => DevelopmentExecution.RunImplementationAsync(session, key, "Use the tools.", source, timeout.Token,
                _ => { checkpoints++; return Task.CompletedTask; }, shell: shell);
            if (repeated)
            {
                var error = await Assert.ThrowsAsync<WorkspaceCommandPolicyException>(Run);
                Assert.True(error.RecoveryExhausted);
                Assert.True(DevelopmentFailurePolicy.IsOperational(error));
                Assert.False(File.Exists(Path.Combine(source, ".csweet", "outcome.json")));
            }
            else await Run();
            Assert.Equal(4, calls);
            Assert.True(checkpoints > 0);
            Assert.NotNull(saved);
            Assert.Equal("retained source", await File.ReadAllTextAsync(Path.Combine(source, "partial.txt")));
            Assert.Empty(progress.Validations.Summary(source).Validations);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
