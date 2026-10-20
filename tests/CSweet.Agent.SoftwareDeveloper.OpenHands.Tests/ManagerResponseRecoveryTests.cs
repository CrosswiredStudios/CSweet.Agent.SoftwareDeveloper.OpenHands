using System.Text.Json;
using CSweet.Agent.Development;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;
using TicketConversations;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class ManagerResponseRecoveryTests
{
    [Theory]
    [InlineData(true,false,false)]
    [InlineData(false,false,false)]
    [InlineData(true,true,false)]
    [InlineData(true,false,true)]
    public async Task OnlyCurrentAuthorizedResolvedClarificationRequestsOneGovernedRetry(bool resolved,bool revisionQuestion,bool foreignReply)
    {
        var f=new Fixture(revisionQuestion,foreignReply);
        var modelCalls=0;
        var client=new ModelBridgeTests.ScriptedClient((_,_)=> { modelCalls++; return new(new ChatMessage(ChatRole.Assistant,resolved?"{\"resolved\":true}":"{\"resolved\":false}")); });
        for(var i=0;i<2;i++) await ManagerResponseRecovery.RecoverAsync(f.Runtime.CreateContext(installationId:f.Self.ToString()),_=>Task.FromResult<IChatClient>(client),default);
        Assert.Equal(resolved&&!revisionQuestion&&!foreignReply?1:0,f.Retries);
        Assert.Equal(revisionQuestion||foreignReply?0:1,modelCalls); // Unchanged answers do not repeatedly spend model turns.
    }

    [Fact]
    public async Task RetryFailureRetainsDecisionAndRechecksStateWithoutRepeatingInference()
    {
        var f=new Fixture(false,false) {FailRetry=true};
        var calls=0;
        var client=new ModelBridgeTests.ScriptedClient((_,_)=> { calls++; return new(new ChatMessage(ChatRole.Assistant,"{\"resolved\":true}")); });
        await Assert.ThrowsAsync<InvalidOperationException>(()=>ManagerResponseRecovery.RecoverAsync(f.Runtime.CreateContext(installationId:f.Self.ToString()),_=>Task.FromResult<IChatClient>(client),default));
        f.FailRetry=false;
        await ManagerResponseRecovery.RecoverAsync(f.Runtime.CreateContext(installationId:f.Self.ToString()),_=>Task.FromResult<IChatClient>(client),default);
        Assert.Equal(1,calls); Assert.Equal(2,f.Retries);
    }

    [Fact]
    public async Task ChangedPlanningRevisionNeverSpendsInferenceOrRetries()
    {
        var f=new Fixture(false,false);
        f.Item=f.Item with {PlanningRevision=8};
        await ManagerResponseRecovery.RecoverAsync(f.Runtime.CreateContext(installationId:f.Self.ToString()),_=>throw new Exception("No inference"),default);
        Assert.Equal(0,f.Retries);
    }

    private sealed class Fixture
    {
        internal readonly Guid Self=Guid.NewGuid();
        internal readonly AgentTestRuntime Runtime=new();
        internal WorkItem Item;
        internal int Retries;
        internal bool FailRetry;
        private AgentOperatingStateResponse _state;

        internal Fixture(bool revision,bool foreign)
        {
            var board=Guid.NewGuid(); var item=Guid.NewGuid(); var recipient=Guid.NewGuid(); var stage=Guid.NewGuid();
            var question=new WorkItemComment(Guid.NewGuid(),item,"AgentInstallation",Self,"Developer","What is the expected behavior?",1,DateTimeOffset.UtcNow,null){Kind="discussion.request"};
            var answer=new WorkItemComment(Guid.NewGuid(),item,"AgentInstallation",foreign?Guid.NewGuid():recipient,"Manager","Use the ticket's existing documented behavior.",1,DateTimeOffset.UtcNow,null)
                {Kind="discussion.reply",CausationId=Discussion.Correlation(question.Id,question.Revision)};
            Item=JsonSerializer.Deserialize<WorkItem>("{}")! with {Id=item,SprintId=Guid.NewGuid(),PlanningRevision=7,Status="Blocked",
                StageAssignments=[new("technical-review","AgentInstallation",AgentInstallationId:recipient)]};
            var stageRecord=JsonSerializer.Deserialize<WorkStageExecutionResponse>("{}")! with {Id=stage,Status="Blocked",AgentInstallationId=Self,AssignmentRevision=42};
            var execution=JsonSerializer.Deserialize<WorkSprintExecutionResponse>("{}")! with {Id=Guid.NewGuid(),Items=[new(Guid.NewGuid(),item,"DEV-1","develop",1,"Blocked",null,[stageRecord],DateTimeOffset.UtcNow)]};
            var waits=new ManagerResponseRecovery.Waits([new(stage,42,7,new(board,item,question.Id,question.Revision,recipient){RequiresRevision=revision})]);
            _state=new(Guid.NewGuid(),"development/response-waits","development",1,"Active",new Dictionary<string,string>(),[],"wait",[],item,
                JsonSerializer.SerializeToElement(waits,new JsonSerializerOptions(JsonSerializerDefaults.Web)),1,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
            Runtime.RegisterCapability<AgentOperatingStateReadRequest,AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,(_,_)=>Task.FromResult(new AgentOperatingStateReadResponse(_state)))
                .RegisterCapability<AgentOperatingStateWriteRequest,AgentOperatingStateResponse>(PlatformCapabilities.AgentOperatingStateWrite,(input,_)=>
                {
                    Assert.Equal(_state.Revision,input.ExpectedRevision);
                    _state=_state with {Payload=input.Payload,Revision=_state.Revision+1};return Task.FromResult(_state);
                })
                .RegisterCapability<WorkItemReference,WorkItem>(WorkItemCapabilities.Read,(_,_)=>Task.FromResult(Item))
                .RegisterCapability<ReadWorkOrchestrationRequest,WorkSprintExecutionResponse>(WorkOrchestrationCapabilities.Read,(_,_)=>Task.FromResult(execution))
                .RegisterCapability<ReadWorkItemCommentsRequest,WorkItemCommentPage>(WorkItemCapabilities.ReadComments,(_,_)=>Task.FromResult(new WorkItemCommentPage([question,answer],1,100,false,1)))
                .RegisterCapability<RetryWorkStageExecutionRequest,WorkStageExecutionResponse>(WorkOrchestrationCapabilities.Retry,(request,_)=>
                {
                    Assert.Equal(stage,request.StageExecutionId); Assert.Equal(42,request.ExpectedAssignmentRevision); Retries++;
                    if(FailRetry)throw new InvalidOperationException("Interrupted retry response");
                    return Task.FromResult(stageRecord with {Status="Pending"});
                });
        }
    }
}
