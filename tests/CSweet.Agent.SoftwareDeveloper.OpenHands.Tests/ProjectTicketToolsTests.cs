using System.Text.Json;
using CSweet.Agent.Development;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class ProjectTicketToolsTests
{
    [Fact]
    public async Task CurrentBoardCommentsUseTypedReceiptsAndStableKeysAcrossRestoration()
    {
        var f = new Fixture();
        var arguments = new Dictionary<string, object?> { ["ticketId"] = f.Item.Id.ToString(), ["body"] = " S1 research is complete; module-only scope retained. " };
        var first = await f.Tool("post_work_ticket_comment").InvokeAsync(new AIFunctionArguments(arguments));
        var restored = new ProjectTicketTools(f.BoardId, f.WorkspaceId, f.Runtime.CreateContext());
        var again = await Functions(restored)["post_work_ticket_comment"].InvokeAsync(new AIFunctionArguments(arguments));
        Assert.Equal(Read<WorkItemComment>(first).Id, Read<WorkItemComment>(again).Id);
        Assert.Equal(2, f.BoardReads); Assert.Equal(2, f.CommentCalls); Assert.Single(f.Comments);
        Assert.StartsWith($"development-comment:{f.WorkspaceId:N}:{f.Item.Id:N}:", f.Comments.Single().Key);
        Assert.All(f.Comments.Values, x => Assert.Equal("S1 research is complete; module-only scope retained.", x.Body));
        Assert.All(Functions(restored).Values, tool => Assert.DoesNotContain("boardId", tool.JsonSchema.GetRawText()));
    }

    [Theory]
    [InlineData("foreign-ticket")]
    [InlineData("deleted-ticket")]
    [InlineData("invalid-id")]
    [InlineData("empty-comment")]
    [InlineData("oversized-comment")]
    public async Task InvalidTargetsAndBodiesCannotPost(string scenario)
    {
        var f = new Fixture();
        if (scenario == "deleted-ticket") f.Available = false;
        var id = scenario == "foreign-ticket" ? Guid.NewGuid().ToString() : scenario == "invalid-id" ? "not-an-id" : f.Item.Id.ToString();
        var body = scenario == "empty-comment" ? " " : scenario == "oversized-comment" ? new string('x', 6001) : "Progress";
        await Assert.ThrowsAsync<ArgumentException>(async () => await f.Tool("post_work_ticket_comment").InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["ticketId"] = id, ["body"] = body })));
        Assert.Equal(0, f.CommentCalls); Assert.Empty(f.Comments);
    }

    [Fact]
    public async Task ReadAndCommentAuthorityAreRecheckedOnEveryCall()
    {
        var f = new Fixture();
        await f.Tool("read_project_tickets").InvokeAsync(new AIFunctionArguments());
        f.Available = false;
        await Assert.ThrowsAsync<ArgumentException>(async () => await f.Tool("read_work_ticket_comments").InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["ticketId"] = f.Item.Id.ToString() })));
        Assert.Equal(0, f.ThreadReads);
        f.Available = true; f.DenyMutation = true;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await f.Tool("post_work_ticket_comment").InvokeAsync(
            new AIFunctionArguments(new Dictionary<string, object?> { ["ticketId"] = f.Item.Id.ToString(), ["body"] = "Progress" })));
        Assert.Empty(f.Comments);
    }

    [Fact]
    public async Task CommentReadsArePagedAndRetainPlatformRevisionMetadata()
    {
        var f = new Fixture();
        var result = await f.Tool("read_work_ticket_comments").InvokeAsync(new AIFunctionArguments(
            new Dictionary<string, object?> { ["ticketId"] = f.Item.Id.ToString(), ["page"] = 2 }));
        var page = Read<WorkItemCommentPage>(result);
        Assert.Equal(2, page.Page); Assert.Equal(20, page.PageSize); Assert.Equal(9, page.SourceRevision);
        Assert.Equal(1, f.ThreadReads);
        await Assert.ThrowsAsync<ArgumentException>(async () => await f.Tool("read_work_ticket_comments").InvokeAsync(new AIFunctionArguments(
            new Dictionary<string, object?> { ["ticketId"] = f.Item.Id.ToString(), ["page"] = 51 })));
        Assert.Equal(1, f.ThreadReads);
    }

    [Fact]
    public async Task AChangedPlatformBoardCannotRedirectTheTools()
    {
        var f = new Fixture { WrongBoard = true };
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await f.Tool("read_project_tickets").InvokeAsync(new AIFunctionArguments()));
        Assert.Equal(0, f.CommentCalls);
    }

    private static Dictionary<string, AIFunction> Functions(ProjectTicketTools tools) => tools.Attach(new() { WorkspacePath = Path.GetTempPath() })
        .ChatOptions!.Tools!.OfType<AIFunction>().ToDictionary(x => x.Name);

    private static T Read<T>(object? result) => Assert.IsType<JsonElement>(result)
        .Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private sealed class Fixture
    {
        internal readonly Guid BoardId = Guid.NewGuid();
        internal readonly Guid WorkspaceId = Guid.NewGuid();
        internal readonly AgentTestRuntime Runtime = new();
        internal WorkItem Item = JsonSerializer.Deserialize<WorkItem>("{}")! with { Id = Guid.NewGuid(), Title = "Pilot", Identifier = "PILOT-1" };
        internal readonly Dictionary<string, WorkItemComment> Comments = [];
        internal bool Available = true, DenyMutation, WrongBoard;
        internal int BoardReads, CommentCalls, ThreadReads;

        internal Fixture()
        {
            Runtime.RegisterCapability<WorkBoardReference, WorkBoardDetail>(WorkItemCapabilities.Read, (input, _) =>
            {
                Assert.Equal(BoardId, input.BoardId); BoardReads++;
                return Task.FromResult(new WorkBoardDetail(JsonSerializer.Deserialize<WorkBoardSummary>("{}")! with
                    { Id = WrongBoard ? Guid.NewGuid() : BoardId, Name = "Pilot board" }, [], Available ? [Item] : []));
            }).RegisterCapability<CommentOnWorkItemRequest, WorkItemComment>(WorkItemCapabilities.Comment, (input, _) =>
            {
                Assert.Equal(BoardId, input.BoardId); Assert.Equal(Item.Id, input.ItemId); CommentCalls++;
                if (DenyMutation) throw new InvalidOperationException("Comment grant was revoked.");
                if (!Comments.TryGetValue(input.IdempotencyKey, out var comment))
                {
                    comment = new(Guid.NewGuid(), Item.Id, "AgentInstallation", Guid.NewGuid(), "Developer", input.Body, 1, DateTimeOffset.UtcNow, null);
                    Comments.Add(input.IdempotencyKey, comment);
                }
                return Task.FromResult(comment);
            }).RegisterCapability<ReadWorkItemCommentsRequest, WorkItemCommentPage>(WorkItemCapabilities.ReadComments, (input, _) =>
            {
                Assert.Equal(BoardId, input.BoardId); Assert.Equal(Item.Id, input.ItemId); ThreadReads++;
                return Task.FromResult(new WorkItemCommentPage([], input.Page, input.PageSize, true, 9));
            });
        }

        internal AIFunction Tool(string name) => Functions(new(BoardId, WorkspaceId, Runtime.CreateContext()))[name];
    }
}
