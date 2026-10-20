using System.Text.Json;
using CSweet.Agent.Development;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class TicketQuestionTests
{
    [Theory]
    [InlineData(true, "active")]
    [InlineData(false, "active")]
    [InlineData(true, "revision")]
    [InlineData(true, "unavailable")]
    [InlineData(true, "not-manager")]
    [InlineData(true, "self")]
    public async Task QuestionsBindAnAuthorizedRecipientAndRestoreWithoutDuplicateComments(bool human, string scenario)
    {
        var self = Guid.NewGuid(); var employee = Guid.NewGuid(); var recipient = scenario == "self" ? employee : Guid.NewGuid();
        var installation = Guid.NewGuid(); var board = Guid.NewGuid(); var attempt = Guid.NewGuid();
        var item = JsonSerializer.Deserialize<WorkItem>("{}")! with {Id = Guid.NewGuid(), PlanningRevision = 7, StageAssignments = []};
        WorkItemComment? saved = null; var posts = 0;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<TeamRosterRequest, TeamRosterResponse>(PlatformCapabilities.TeamRosterRead, (_, _) => Task.FromResult(new TeamRosterResponse(
                new("team", "team", "Team", 1, recipient.ToString(), "Manager",
                    [new(recipient.ToString(), "Manager", human ? "Human" : "Agent", null, null, "Manager", "Active")
                        {AgentInstallationId = human ? null : installation, IsAvailable = scenario != "unavailable"}], [], 1, false))))
            .RegisterCapability<WorkItemReference, WorkItem>(WorkItemCapabilities.Read, (_, _) => Task.FromResult(item))
            .RegisterCapability<WorkBoardListRequest, IReadOnlyList<WorkBoardSummary>>(WorkBoardCapabilities.Read, (_, _) => Task.FromResult<IReadOnlyList<WorkBoardSummary>>(
                [JsonSerializer.Deserialize<WorkBoardSummary>("{}")! with { Id=board, ManagerOrganizationUserId = scenario == "not-manager" ? Guid.NewGuid() : recipient }]))
            .RegisterCapability<CommentOnWorkItemRequest, WorkItemComment>(WorkItemCapabilities.Comment, (request, _) =>
            {
                posts++;
                saved = new(Guid.NewGuid(), item.Id, "AgentInstallation", self, "Developer", request.Body, 1, DateTimeOffset.UtcNow, null)
                    {Kind = request.Kind, CausationId = request.CausationId};
                return Task.FromResult(saved);
            })
            .RegisterCapability<ReadWorkItemCommentsRequest, WorkItemCommentPage>(WorkItemCapabilities.ReadComments, (_, _) =>
                Task.FromResult(new WorkItemCommentPage(saved is null ? [] : [saved], 1, 100, false, 1)));
        var context = runtime.CreateContext(installationId: self.ToString(),
            identity: new(employee.ToString(), "Developer", null, null, null, [], null, recipient.ToString(), "Manager"));
        var tools = new TicketDiscussionTools(board, item, attempt, context);
        var function = tools.Attach(new() { WorkspacePath = Path.GetTempPath() }).ChatOptions!.Tools!.OfType<AIFunction>().Single(x => x.Name == "request_ticket_response");
        if (scenario is "unavailable" or "not-manager" or "self")
        {
            await Assert.ThrowsAsync<ArgumentException>(async () => await function.InvokeAsync(new AIFunctionArguments(
                new Dictionary<string, object?> { ["recipientEmployeeId"] = recipient.ToString(), ["question"] = "Which existing behavior applies?" })));
            Assert.Null(tools.Pending); Assert.Equal(0, posts); return;
        }
        if (scenario == "revision")
            await tools.RequestRevisionAsync(recipient.ToString(), "Earlier assumption", "Observed code", "Proposed correction", default);
        else
            await function.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
                { ["recipientEmployeeId"] = recipient.ToString(), ["question"] = "Which existing behavior applies?" }));
        var wait = Assert.IsType<TicketConversations.Discussion.ResponseWait>(tools.Pending);
        Assert.Equal(human ? Guid.Empty : installation, wait.RespondingInstallationId);
        Assert.Equal(human ? recipient : (Guid?)null, wait.RespondingEmployeeId);
        Assert.Equal(scenario == "revision", wait.RequiresRevision);
        Assert.Equal(human ? $"{attempt:D}:{recipient:D}:human" : $"{attempt:D}:{installation:D}", saved!.CausationId);
        var recovered = new TicketDiscussionTools(board, item, attempt, context);
        await recovered.RecoverAsync(default);
        Assert.Equal(wait, recovered.Pending);
        Assert.Equal(1, posts);
    }
}
