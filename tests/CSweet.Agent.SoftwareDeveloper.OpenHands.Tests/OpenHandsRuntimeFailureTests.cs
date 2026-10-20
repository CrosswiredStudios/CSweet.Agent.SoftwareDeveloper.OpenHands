using CSweet.Agent.Development;
using CSweet.Agent.OpenHands;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;
using System.Text.Json;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkerTerminationCannotBecomeADevelopmentSuccess(bool reportedFailure)
    {
        var python = Environment.GetEnvironmentVariable("CSWEET_TEST_OPENHANDS_PYTHON")
            ?? throw new InvalidOperationException("The pinned worker test environment is required.");
        var root = Path.Combine(Path.GetTempPath(), "csweet-worker-exit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "worker.py"), "import sys,json\nsys.stdin.readline()\n" +
            (reportedFailure ? "print(json.dumps({'protocol':1,'kind':'failed','code':'BadRequestError','phase':'execution','message':'private-provider-secret'}),flush=True)\n" : ""));
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
            Assert.Contains(reportedFailure ? "execution (BadRequestError)" : "ended before completing", error.Message);
            Assert.DoesNotContain("private-provider-secret", error.ToString());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("{\"code\":\"ValidationError\",\"phase\":\"model_setup\"}", "model_setup (ValidationError)")]
    [InlineData("{\"code\":\"https://private/?token=secret\",\"phase\":\"private-provider-secret\"}", "unknown (WorkerError)")]
    [InlineData("{\"code\":{},\"phase\":null}", "unknown (WorkerError)")]
    public void FailureDiagnosticsExposeOnlyKnownCategories(string json, string expected)
    {
        using var data = JsonDocument.Parse(json);
        var diagnostic = OpenHandsExecutionEngine.WorkerFailureDiagnostic(data.RootElement);
        Assert.Contains(expected, diagnostic);
        Assert.DoesNotContain("secret", diagnostic);
        Assert.DoesNotContain("https:", diagnostic);
    }
}
