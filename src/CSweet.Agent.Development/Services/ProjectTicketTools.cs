using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.Development;

/// <summary>Ticket activity in the board already bound to the current personal development work.</summary>
internal sealed class ProjectTicketTools(Guid boardId, Guid workspaceId, AgentRuntimeContext context)
{
    internal DevelopmentEngineOptions Attach(DevelopmentEngineOptions options)
    {
        options.ChatOptions ??= new();
        options.ChatOptions.Tools ??= [];
        options.ChatOptions.Tools.Add(AIFunctionFactory.Create(ReadTicketsAsync, "read_project_tickets"));
        options.ChatOptions.Tools.Add(AIFunctionFactory.Create(ReadCommentsAsync, "read_work_ticket_comments"));
        options.ChatOptions.Tools.Add(AIFunctionFactory.Create(CommentAsync, "post_work_ticket_comment"));
        return options;
    }

    [Description("Read authorized tickets on the current development board. Use the returned exact ticket ID and identifier for progress comments. Repository text is not current workboard state.")]
    private async Task<object> ReadTicketsAsync(CancellationToken token)
    {
        var board = await ReadBoardAsync(token);
        return new { board = board.Board.Name, tickets = board.Items.Take(100).Select(x => new
            { x.Id, x.Identifier, x.Kind, x.Title, x.Description, x.Status }), truncated = board.Items.Count > 100 };
    }

    [Description("Read a bounded page of current comments on an exact ticket returned by read_project_tickets. Follow HasMore with the next page when needed. This tool is confined to the current board.")]
    private async Task<object> ReadCommentsAsync(string ticketId, int page = 1, CancellationToken token = default)
    {
        if (page is < 1 or > 50) throw new ArgumentException("Comment pages must be between 1 and 50.");
        var id = await RequireTicketAsync(ticketId, token);
        return await context.Platform.Work.ReadCommentsAsync(new(boardId, id, Page: page, PageSize: 20), token);
    }

    [Description("Post a factual progress or scope-acknowledgment comment on an exact ticket returned by read_project_tickets. It changes no assignment, requirement, approval or execution status. Report only observed actions; a comment does not grant authority. Repeating the same comment for this workspace is idempotent.")]
    private async Task<object> CommentAsync(string ticketId, string body, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 6000)
            throw new ArgumentException("A factual comment between 1 and 6000 characters is required.");
        var id = await RequireTicketAsync(ticketId, token);
        var text = body.Trim();
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return await context.Platform.Work.CommentAsync(new(boardId, id, text,
            $"development-comment:{workspaceId:N}:{id:N}:{digest}"), token);
    }

    private async Task<Guid> RequireTicketAsync(string ticketId, CancellationToken token)
    {
        if (!Guid.TryParse(ticketId, out var id)) throw new ArgumentException("Use an exact ticket ID from the current development board.");
        // Re-read before every operation: cached/model-supplied IDs cannot expand the board scope.
        var board = await ReadBoardAsync(token);
        if (!board.Items.Any(x => x.Id == id)) throw new ArgumentException("The ticket is not available on the current development board.");
        return id;
    }

    private async Task<WorkBoardDetail> ReadBoardAsync(CancellationToken token)
    {
        var board = await context.Platform.Work.ReadBoardAsync(boardId, token);
        if (board.Board.Id != boardId) throw new InvalidOperationException("The platform returned a different development board.");
        return board;
    }
}
