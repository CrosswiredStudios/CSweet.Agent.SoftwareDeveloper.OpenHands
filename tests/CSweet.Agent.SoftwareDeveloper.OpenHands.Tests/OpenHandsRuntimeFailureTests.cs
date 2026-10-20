using CSweet.Agent.Development;
using CSweet.Agent.OpenHands;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class OpenHandsRuntimeFailureTests
{
    [Fact]
    public void MissingExecutableRoutesToOperationalRecovery()
    {
        var error = Assert.Throws<OperationalDevelopmentException>(() =>
            OpenHandsExecutionEngine.Start("csweet-missing-" + Guid.NewGuid().ToString("N"), [], Path.GetTempPath()));
        Assert.True(DevelopmentFailurePolicy.IsOperational(error));
        Assert.IsType<System.ComponentModel.Win32Exception>(error.InnerException);
    }

    [Fact]
    public async Task WorkerExitBeforeCompletionCannotBecomeADevelopmentSuccess()
    {
        var python = Environment.GetEnvironmentVariable("CSWEET_TEST_OPENHANDS_PYTHON")
            ?? throw new InvalidOperationException("The pinned worker test environment is required.");
        var root = Path.Combine(Path.GetTempPath(), "csweet-worker-exit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "worker.py"), "import sys\nsys.stdin.readline()\n");
        var runtime = new AgentTestRuntime().RegisterCapability<ExecutionCheckpointRead, ExecutionCheckpointReadResult>(
            ExecutionCheckpointCapabilities.Read, (_, _) => Task.FromResult(new ExecutionCheckpointReadResult(null)));
        using var client = new ModelBridgeTests.ScriptedClient((_, _) => throw new Exception("No model call is authorized."));
        var engine = new OpenHandsExecutionEngine(new(python, Path.Combine(root, "runtime"), root));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var error = await Assert.ThrowsAsync<OperationalDevelopmentException>(() => engine.ExecuteAsync(
                new("worker-exit", "test", root, runtime.CreateContext(), client, new ChatOptions(), 2, 32000, 1000), timeout.Token));
            Assert.True(DevelopmentFailurePolicy.IsOperational(error));
            Assert.Contains("ended before completing", error.Message);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
