using System.ComponentModel.DataAnnotations;
using FiveStack.Api.Data;
using FiveStack.Api.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiveStack.Api.Chat;

public record MessageInput([Required, StringLength(1000)] string Body);
public record MessageDto(long Id, Guid ConversationId, Guid SenderId, string SenderName, string Body, DateTimeOffset SentAt);
/// <summary>`Other` is the other player in a DM; null for squad chats (then `SquadPostId` is set).</summary>
public record ConversationDto(Guid Id, Guid? SquadPostId, string Title, PublicProfileDto? Other, MessageDto? Last, int Unread);

[ApiController]
[Authorize]
[Route("api")]
public class ChatController(AppDbContext db, UserManager<AppUser> users, IHubContext<ChatHub> hub) : ControllerBase
{
    const int PageSize = 50;

    Guid Me => Guid.Parse(users.GetUserId(User)!); // [Authorize] guarantees the claim

    /// <summary>My conversations, most recent activity first, with unread counts.</summary>
    [HttpGet("conversations")]
    public async Task<ConversationDto[]> List()
    {
        var me = Me;
        var rows = await db.ConversationMembers.AsNoTracking().Where(m => m.UserId == me)
            .Select(m => new
            {
                m.Conversation.Id,
                m.Conversation.SquadPostId,
                m.Conversation.CreatedAt,
                SquadTitle = m.Conversation.SquadPost!.Title,
                Other = m.Conversation.Members.Where(o => o.UserId != me).Select(o => o.User).FirstOrDefault(),
                Last = m.Conversation.Messages.OrderByDescending(x => x.Id)
                    .Select(x => new { x.Id, x.SenderId, x.Sender.RiotName, x.Body, x.SentAt }).FirstOrDefault(),
                Unread = m.Conversation.Messages.Count(x => x.SenderId != me && x.SentAt > m.LastReadAt),
            })
            .ToListAsync();

        return [.. rows
            .OrderByDescending(r => r.Last?.SentAt ?? r.CreatedAt)
            .Select(r => new ConversationDto(
                r.Id, r.SquadPostId,
                r.SquadPostId is null ? r.Other is { } o ? $"{o.RiotName}#{o.RiotTag}" : "Deleted player" : r.SquadTitle,
                r.SquadPostId is null && r.Other is { } other ? ProfilesController.ToPublic(other) : null,
                r.Last is { } l ? new MessageDto(l.Id, r.Id, l.SenderId, l.RiotName ?? "", l.Body, l.SentAt) : null,
                r.Unread))];
    }

    /// <summary>Find or start the DM with a player. Returns its id.</summary>
    [HttpPost("conversations/dm/{userId:guid}")]
    [EnableRateLimiting("dm-start")]
    public async Task<IActionResult> StartDm(Guid userId)
    {
        var me = Me;
        if (userId == me) return Problem("You can't message yourself.", statusCode: 400);
        if (!await db.Users.AnyAsync(u => u.Id == me && u.OnboardedAt != null))
            return Problem("Finish your profile first.", statusCode: 403);
        if (await db.Users.Where(u => u.Id == userId && u.OnboardedAt != null).Select(u => (bool?)u.IsDemo).FirstOrDefaultAsync()
            is not { } demo) return NotFound();
        if (demo) return Problem("Demo players can't be messaged.", statusCode: 409);
        if (await Blocked(me, userId)) return Problem("You can't message this player.", statusCode: 403);

        var key = Conversation.KeyFor(me, userId);
        if (await DmId(key) is { } existing) return Ok(new { id = existing });

        var now = DateTimeOffset.UtcNow;
        var dm = new Conversation
        {
            DmKey = key, CreatedAt = now,
            Members = [new() { UserId = me, LastReadAt = now }, new() { UserId = userId, LastReadAt = now }],
        };
        db.Conversations.Add(dm);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) // the other player started it at the same moment (unique DmKey)
        {
            return Ok(new { id = await DmId(key) });
        }
        return Ok(new { id = dm.Id });
    }

    /// <summary>Newest page of messages (oldest first). Pass `before` = the oldest id you have for the page before it.</summary>
    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<ActionResult<MessageDto[]>> Messages(Guid id, long? before)
    {
        var me = Me;
        if (!await db.ConversationMembers.AnyAsync(m => m.ConversationId == id && m.UserId == me)) return NotFound();

        var q = db.Messages.AsNoTracking().Where(m => m.ConversationId == id);
        if (before is { } b) q = q.Where(m => m.Id < b);
        var rows = await q.OrderByDescending(m => m.Id).Take(PageSize)
            .Select(m => new MessageDto(m.Id, m.ConversationId, m.SenderId, m.Sender.RiotName ?? "", m.Body, m.SentAt))
            .ToListAsync();
        rows.Reverse();
        return rows.ToArray();
    }

    [HttpPost("conversations/{id:guid}/messages")]
    [EnableRateLimiting("messages")]
    public async Task<IActionResult> Send(Guid id, MessageInput input)
    {
        var me = Me;
        var body = input.Body.Trim();
        if (body.Length == 0)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["body"] = ["Message is empty."] }));

        var c = await db.Conversations.AsNoTracking().Where(c => c.Id == id && c.Members.Any(m => m.UserId == me))
            .Select(c => new { c.DmKey, Members = c.Members.Select(m => m.UserId).ToList() })
            .FirstOrDefaultAsync();
        if (c is null) return NotFound(); // not a member: same answer as a missing conversation
        if (c.DmKey is not null && c.Members.FirstOrDefault(m => m != me) is var other && await Blocked(me, other))
            return Problem("You can't message this player.", statusCode: 403);

        var msg = new Message { ConversationId = id, SenderId = me, Body = body, SentAt = DateTimeOffset.UtcNow };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
        await db.ConversationMembers.Where(m => m.ConversationId == id && m.UserId == me) // your own message is read
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastReadAt, msg.SentAt));

        AppMetrics.MessagesSent.Add(1, new KeyValuePair<string, object?>("kind", c.DmKey is null ? "squad" : "dm"));

        var name = await db.Users.Where(u => u.Id == me).Select(u => u.RiotName).FirstAsync() ?? "";
        var dto = new MessageDto(msg.Id, id, me, name, msg.Body, msg.SentAt);
        await hub.Clients.Users(c.Members.Select(m => m.ToString())).SendAsync("MessageReceived", dto);
        return Ok(dto);
    }

    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var me = Me;
        var n = await db.ConversationMembers.Where(m => m.ConversationId == id && m.UserId == me)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LastReadAt, DateTimeOffset.UtcNow));
        return n == 0 ? NotFound() : NoContent();
    }

    /// <summary>Ids of the players I blocked.</summary>
    [HttpGet("blocks")]
    public async Task<Guid[]> Blocks()
    {
        var me = Me;
        return await db.Blocks.Where(b => b.BlockerId == me).Select(b => b.BlockedId).ToArrayAsync();
    }

    [HttpPut("blocks/{userId:guid}")]
    public async Task<IActionResult> BlockUser(Guid userId)
    {
        var me = Me;
        if (userId == me) return Problem("You can't block yourself.", statusCode: 400);
        if (!await db.Users.AnyAsync(u => u.Id == userId)) return NotFound();
        if (await db.Blocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == userId)) return NoContent();
        db.Blocks.Add(new Block { BlockerId = me, BlockedId = userId });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) { } // a double click raced: already blocked
        return NoContent();
    }

    [HttpDelete("blocks/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId)
    {
        var me = Me;
        await db.Blocks.Where(b => b.BlockerId == me && b.BlockedId == userId).ExecuteDeleteAsync();
        return NoContent();
    }

    Task<bool> Blocked(Guid a, Guid b) =>
        db.Blocks.AnyAsync(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a));

    Task<Guid?> DmId(string key) => db.Conversations.Where(c => c.DmKey == key).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
}
