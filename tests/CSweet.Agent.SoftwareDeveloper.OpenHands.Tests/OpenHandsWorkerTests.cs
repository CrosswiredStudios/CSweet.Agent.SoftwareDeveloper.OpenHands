using System.IO.Compression;
using System.Text.Json;
using CSweet.Agent.Development;
using CSweet.Agent.OpenHands;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class OpenHandsWorkerTests
{
    [Theory]
    [InlineData(32000, 4000)]
    [InlineData(256000, 128000)]
    public async Task RealPinnedWorkerEditsCallsTypedToolsCheckpointsAndRestoresWithoutRepeatingModelWork(int contextTokens, int outputTokens)
    {
        var python = Environment.GetEnvironmentVariable("CSWEET_TEST_OPENHANDS_PYTHON");
        if (string.IsNullOrWhiteSpace(python))
            throw new InvalidOperationException("Set CSWEET_TEST_OPENHANDS_PYTHON to a hash-locked OpenHands 1.56.0 test environment. This integration test must not silently skip.");
        var root = Path.Combine(Path.GetTempPath(), "csweet-openhands-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var count = 0; var commands = 0; var searches = 0; var writes = 0;
        ExecutionCheckpoint? checkpoint = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<ExecutionCheckpointRead,ExecutionCheckpointReadResult>(ExecutionCheckpointCapabilities.Read, (_, _) => Task.FromResult(new ExecutionCheckpointReadResult(checkpoint)))
            .RegisterCapability<ExecutionCheckpointWrite,ExecutionCheckpoint>(ExecutionCheckpointCapabilities.Write, (input, _) =>
            {
                Assert.Equal(checkpoint?.Revision ?? 0, input.ExpectedRevision);
                Assert.NotNull(input.SourceArchive);
                writes++;
                checkpoint = new(input.SessionKey,input.EngineVersion,input.ExpectedRevision+1,input.EventCursor,input.EngineArchive,"digest",SourceArchive:input.SourceArchive);
                return Task.FromResult(checkpoint);
            })
            .RegisterCapability<WebSearchRequest,WebSearchResult>(ResearchCapabilities.Search, (_, _) =>
            {
                searches++; return Task.FromResult(new WebSearchResult([new("Documentation","https://example.com/docs","API")],[]));
            });
        using var client = new ModelBridgeTests.ScriptedClient((messages, opts) =>
        {
            Assert.Null(opts!.Reasoning); // Leave provider/model defaults under C-Sweet's control.
            Assert.Contains(opts!.Tools!, x => x.Name == "file_editor");
            var call = ++count switch
            {
                1 => new FunctionCallContent("edit", "file_editor", new Dictionary<string,object?> { ["command"]="create",["path"]=Path.Combine(source,"hello.txt"),["file_text"]="hello" }),
                2 => new FunctionCallContent("command", "terminal", new Dictionary<string,object?> { ["command"]="focused-test" }),
                3 => new FunctionCallContent("search", "search_web", new Dictionary<string,object?> { ["query"]="library docs" }),
                4 => new FunctionCallContent("finish", "finish", new Dictionary<string,object?> { ["message"]="Implemented and tested." }),
                _ => throw new InvalidOperationException("Completed model work was repeated.")
            };
            return new(new ChatMessage(ChatRole.Assistant,[call]));
        });
        Environment.SetEnvironmentVariable("CSWEET_WORKSPACE_MAXIMUM_ARCHIVE_BYTES","8388608");
        Environment.SetEnvironmentVariable("CSWEET_WORKSPACE_MAXIMUM_EXPANDED_BYTES","67108864");
        Environment.SetEnvironmentVariable("CSWEET_WORKSPACE_MAXIMUM_FILE_COUNT","20000");
        var engine = new OpenHandsExecutionEngine(new(python, Path.Combine(root,"runtime"), Path.Combine(RepositoryRoot(),"src/CSweet.Agent.OpenHands/worker")) { TestDiagnostics=Console.Error.WriteLine });
        var options = new ChatOptions { Instructions="Use the tools and finish the task.", Tools=[AIFunctionFactory.Create((string command) =>
        {
            Assert.Equal("focused-test",command); commands++; return "Exit code: 0. Focused tests passed.";
        },"execute_workspace_command")] };
        var request = new DevelopmentExecutionRequest("worker-test","Implement, test and research.",source,runtime.CreateContext(),client,options,12,contextTokens,outputTokens);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            var result = await engine.ExecuteAsync(request,timeout.Token);
            Assert.Equal("Implemented and tested.",result.Text);
            Assert.Equal("hello",File.ReadAllText(Path.Combine(source,"hello.txt")));
            Assert.Equal(1,commands); Assert.Equal(1,searches); Assert.Equal(4,count); Assert.True(writes>4);
            File.WriteAllText(Path.Combine(source,"hello.txt"),"uncheckpointed damage");
            var resumed = await engine.ExecuteAsync(request,timeout.Token);
            Assert.Equal(result,resumed);
            Assert.Equal("hello",File.ReadAllText(Path.Combine(source,"hello.txt")));
            Assert.Equal(4,count); Assert.Equal(1,commands); Assert.Equal(1,searches);
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }

    [Fact]
    public async Task FailedPostEditCheckpointStopsInferenceAndRestoresTheLastAcknowledgedAction()
    {
        var python=Environment.GetEnvironmentVariable("CSWEET_TEST_OPENHANDS_PYTHON")
            ?? throw new InvalidOperationException("Set CSWEET_TEST_OPENHANDS_PYTHON to the pinned test environment.");
        var root=Path.Combine(Path.GetTempPath(),"csweet-worker-crash-"+Guid.NewGuid().ToString("N"));
        var source=Path.Combine(root,"source"); Directory.CreateDirectory(source);
        var edited=Path.Combine(source,"unacknowledged.txt");
        ExecutionCheckpoint? checkpoint=null;
        var calls=0; var injectFailure=true;
        var runtime=new AgentTestRuntime()
            .RegisterCapability<ExecutionCheckpointRead,ExecutionCheckpointReadResult>(ExecutionCheckpointCapabilities.Read,
                (_,_)=>Task.FromResult(new ExecutionCheckpointReadResult(checkpoint)))
            .RegisterCapability<ExecutionCheckpointWrite,ExecutionCheckpoint>(ExecutionCheckpointCapabilities.Write,(input,_)=>
            {
                if(injectFailure && File.Exists(edited)) throw new IOException("Injected checkpoint outage after file edit.");
                Assert.Equal(checkpoint?.Revision??0,input.ExpectedRevision);
                checkpoint=new(input.SessionKey,input.EngineVersion,input.ExpectedRevision+1,input.EventCursor,input.EngineArchive,"digest",SourceArchive:input.SourceArchive);
                return Task.FromResult(checkpoint);
            });
        using var client=new ModelBridgeTests.ScriptedClient((messages,_)=>
        {
            if(++calls==1) return new(new ChatMessage(ChatRole.Assistant,[new FunctionCallContent("edit","file_editor",
                new Dictionary<string,object?> { ["command"]="create",["path"]=edited,["file_text"]="unacknowledged" })]));
            Assert.False(File.Exists(edited));
            Assert.Contains(messages.SelectMany(x=>x.Contents).OfType<FunctionResultContent>(),x=>x.Result?.ToString()?.Contains("Interrupted action") == true);
            return new(new ChatMessage(ChatRole.Assistant,[new FunctionCallContent("finish","finish",
                new Dictionary<string,object?> { ["message"]="Interrupted action reconciled; no command was replayed." })]));
        });
        var engine=new OpenHandsExecutionEngine(new(python,Path.Combine(root,"runtime"),Path.Combine(RepositoryRoot(),"src/CSweet.Agent.OpenHands/worker")) {TestDiagnostics=Console.Error.WriteLine});
        var request=new DevelopmentExecutionRequest("crash-test","Inspect and reconcile the current workspace.",source,runtime.CreateContext(),client,new(),8,32000,4000);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await Assert.ThrowsAsync<IOException>(()=>engine.ExecuteAsync(request,timeout.Token));
            Assert.Equal(1,calls); Assert.NotNull(checkpoint); Assert.True(File.Exists(edited));
            injectFailure=false;
            var result=await engine.ExecuteAsync(request,timeout.Token);
            Assert.Contains("reconciled",result.Text); Assert.Equal(2,calls); Assert.False(File.Exists(edited));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }

    private static string RepositoryRoot()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null && !Directory.Exists(Path.Combine(directory.FullName,"src/CSweet.Agent.OpenHands"))) directory=directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }
}
