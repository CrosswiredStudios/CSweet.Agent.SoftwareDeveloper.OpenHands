using System.Text.Json;
using CSweet.Agent.Development;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

/// <summary>Test-only interpreter for the legacy scripted workflow fixtures; production uses the real pinned worker.</summary>
internal sealed class ScriptedWorkflowEngine : IDevelopmentExecutionEngine
{
    public async Task<DevelopmentExecutionResult> ExecuteAsync(DevelopmentExecutionRequest request,CancellationToken token)
    {
        var messages=new List<ChatMessage>{new(ChatRole.User,request.Prompt)};
        for(var turn=0;turn<16;turn++)
        {
            var response=await request.ChatClient.GetResponseAsync(messages,request.Options,token);
            messages.AddRange(response.Messages);
            var calls=response.Messages.SelectMany(x=>x.Contents).OfType<FunctionCallContent>().ToArray();
            if(calls.Length==0) return new(response.Text);
            foreach(var call in calls)
            {
                Assert.Equal("file_access_write",call.Name);
                var args=JsonSerializer.SerializeToElement(call.Arguments);
                var path=Path.GetFullPath(Path.Combine(request.WorkspacePath,args.GetProperty("fileName").GetString()!));
                Assert.StartsWith(Path.GetFullPath(request.WorkspacePath)+Path.DirectorySeparatorChar,path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path,args.GetProperty("content").GetString(),token);
                if(request.CheckpointSource is not null && !Path.GetRelativePath(request.WorkspacePath,path).StartsWith(".csweet"+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                    await request.CheckpointSource(token);
                messages.Add(new(ChatRole.Tool,[new FunctionResultContent(call.CallId,"written")]));
            }
        }
        throw new InvalidOperationException("Scripted workflow fixture exceeded its bounded turn limit.");
    }
}
