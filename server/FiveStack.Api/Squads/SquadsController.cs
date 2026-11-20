using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using FiveStack.Api.Chat;
using FiveStack.Api.Data;
using FiveStack.Api.Meta;
using FiveStack.Api.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiveStack.Api.Squads;

/// <summary>A new squad post. Values are ids from GET /api/meta.</summary>
public record SquadInput
{
    [Required, StringLength(60, MinimumLength = 3)] public string Title { get; init; } = "";
    [Required] public string Mode { get; init; } = "";
    [Required] public string Region { get; init; } = "";
    public string? Server { get; init; }
    public int? RankMin { get; init; }
    public int? RankMax { get; init; }
    [MaxLength(4)] public string[] NeededRoles { get; init; } = [];
    [MaxLength(5)] public string[] NeededAgents { get; init; } = [];
    [Range(1, 4)] public int SlotsOpen { get; init; } = 1;
    public bool MicRequired { get; init; }
}

/// <summary>Board filters, all optional. `Rank` = posts whose range fits it; `Roles` = needs any of them.</summary>
public record SquadSearch
{
    public string? Region { get; init; }
    public string? Server { get; init; }
    public string? Mode { get; init; }
    [MaxLength(30)] public string[] Roles { get; init; } = [];
    public int? Rank { get; init; }
    public bool NoMic { get; init; }
    public int Page { get; init; } = 1;
}

public record SquadDto(
    Guid Id, string Title, string Mode, string Region, string? Server, int? RankMin, int? RankMax,
    string[] NeededRoles, string[] NeededAgents, int SlotsOpen, bool MicRequired, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, bool Open, PublicProfileDto Host);

public record SquadPage(SquadDto[] Items, int Total, int Page, int PageSize);
public record RequestDto(PublicProfileDto Player, DateTimeOffset CreatedAt);
/// <summary>
/// `Requests` (pending) only for the host; `MyStatus` only for a player who asked to join;
/// `ConversationId` (the squad chat) only for the host and accepted players.
/// </summary>
public record SquadDetailDto(SquadDto Post, PublicProfileDto[] Roster, RequestDto[] Requests, string? MyStatus, Guid? ConversationId);
public record Decision([Required] string Status);

[ApiController]
[Route("api/squads")]
public class SquadsController(AppDbContext db, UserManager<AppUser> users, IHubContext<ChatHub> hub, IConfiguration config) : ControllerBase
{
    const int PageSize = 24;
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(3);

    internal static Expression<Func<SquadPost, bool>> IsOpen(DateTimeOffset now) =>
        p => p.ClosedAt == null && p.ExpiresAt > now && p.SlotsOpen > 0;

    Guid? MeId => Guid.TryParse(users.GetUserId(User), out var id) ? id : null;

    [HttpGet]
    public async Task<SquadPage> Search([FromQuery] SquadSearch f)
    {
        // ponytail: one COUNT per board load; demo posts expire after 3h and keep-warm keeps the instance up.
        if (config.GetValue<bool>("Demo:Seed")) await DemoSeed.TopUpPostsAsync(db);
        var q = db.SquadPosts.AsNoTracking().Include(p => p.Host).Where(IsOpen(DateTimeOffset.UtcNow));

        if (f.Region is not null) q = q.Where(p => p.Region == f.Region);
        if (f.Server is not null) q = q.Where(p => p.Server == null || p.Server == f.Server);
        if (f.Mode is not null) q = q.Where(p => p.Mode == f.Mode);
        if (f.Roles.Length > 0) q = q.Where(p => p.NeededRoles.Length == 0 || p.NeededRoles.Any(x => f.Roles.Contains(x)));
        if (f.Rank is { } r) q = q.Where(p => (p.RankMin == null || p.RankMin <= r) && (p.RankMax == null || p.RankMax >= r));
        if (f.NoMic) q = q.Where(p => !p.MicRequired);

        var page = Math.Clamp(f.Page, 1, 10_000);
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return new SquadPage([.. rows.Select(ToDto)], total, page, PageSize);
    }

    /// <summary>The signed-in player's open post, or 204.</summary>
    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<SquadDto>> Mine()
    {
        var post = await db.SquadPosts.AsNoTracking().Include(p => p.Host)
            .Where(p => p.HostId == MeId).Where(IsOpen(DateTimeOffset.UtcNow)).FirstOrDefaultAsync();
        return post is null ? NoContent() : ToDto(post);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SquadDetailDto>> Get(Guid id)
    {
        var post = await db.SquadPosts.AsNoTracking().Include(p => p.Host)
            .Include(p => p.Requests).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return NotFound();

        var me = MeId;
        var member = post.HostId == me || post.Requests.Any(r => r.UserId == me && r.Status == JoinRequest.Accepted);
        var conversationId = member
            ? await db.Conversations.Where(c => c.SquadPostId == id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync()
            : null;
        var requests = post.HostId == me
            ? post.Requests.Where(r => r.Status == JoinRequest.Pending).OrderBy(r => r.CreatedAt)
                .Select(r => new RequestDto(ProfilesController.ToPublic(r.User), r.CreatedAt)).ToArray()
            : [];
        return new SquadDetailDto(
            ToDto(post),
            [.. post.Requests.Where(r => r.Status == JoinRequest.Accepted).Select(r => ProfilesController.ToPublic(r.User))],
            requests,
            post.Requests.FirstOrDefault(r => r.UserId == me)?.Status,
            conversationId);
    }

    [Authorize]
    [HttpPost]
    [EnableRateLimiting("squads")]
    public async Task<IActionResult> Create(SquadInput s)
    {
        var errors = CheckAgainstMeta(s);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));
        if (await Onboarded() is { } fail) return fail;

        var me = MeId!.Value;
        var now = DateTimeOffset.UtcNow;
        // ponytail: check-then-insert can race into two posts; the rate limit bounds it. A time-based rule can't be a unique index.
        if (await db.SquadPosts.Where(p => p.HostId == me).Where(IsOpen(now)).AnyAsync())
            return Problem("You already have an open squad. Close it before posting another.", statusCode: 409);

        var post = new SquadPost
        {
            HostId = me, Title = s.Title.Trim(), Mode = s.Mode, Region = s.Region, Server = s.Server,
            RankMin = s.RankMin, RankMax = s.RankMax, NeededRoles = [.. s.NeededRoles.Distinct()],
            NeededAgents = [.. s.NeededAgents.Distinct()], SlotsOpen = s.SlotsOpen, MicRequired = s.MicRequired,
            CreatedAt = now, ExpiresAt = now + Lifetime,
        };
        db.SquadPosts.Add(post);
        // The squad chat starts with the host; each accepted player is added in Decide.
        db.Conversations.Add(new Conversation { SquadPost = post, CreatedAt = now, Members = [new() { UserId = me, LastReadAt = now }] });
        await db.SaveChangesAsync();
        AppMetrics.SquadsCreated.Add(1, new("mode", post.Mode), new("region", post.Region));
        return CreatedAtAction(nameof(Get), new { id = post.Id }, new { post.Id });
    }

    /// <summary>The host closes the post early.</summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Close(Guid id)
    {
        var n = await db.SquadPosts.Where(p => p.Id == id && p.HostId == MeId && p.ClosedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.ClosedAt, DateTimeOffset.UtcNow));
        return n == 0 ? NotFound() : NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/requests")]
    public async Task<IActionResult> Join(Guid id)
    {
        if (await Onboarded() is { } fail) return fail;
        var me = MeId!.Value;
        var post = await db.SquadPosts.AsNoTracking().Include(p => p.Host).Where(IsOpen(DateTimeOffset.UtcNow)).FirstOrDefaultAsync(p => p.Id == id);
        if (post is null) return Problem("This squad is closed, full or expired.", statusCode: 409);
        if (post.HostId == me) return Problem("You host this squad.", statusCode: 409);
        if (post.Host.IsDemo) return Problem("Demo squads don't take sign-ups.", statusCode: 409);

        db.JoinRequests.Add(new JoinRequest { PostId = id, UserId = me, CreatedAt = DateTimeOffset.UtcNow });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) // primary key: one request per player per post
        {
            return Problem("You already asked to join.", statusCode: 409);
        }
        AppMetrics.JoinRequests.Add(1);
        await SquadChanged(post.HostId, id);
        return NoContent();
    }

    /// <summary>Withdraw a request the host hasn't answered yet.</summary>
    [Authorize]
    [HttpDelete("{id:guid}/requests/me")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var n = await db.JoinRequests.Where(r => r.PostId == id && r.UserId == MeId && r.Status == JoinRequest.Pending)
            .ExecuteDeleteAsync();
        if (n == 0) return NotFound();
        await SquadChanged(await db.SquadPosts.Where(p => p.Id == id).Select(p => p.HostId).FirstAsync(), id);
        return NoContent();
    }

    /// <summary>The host accepts or declines a pending request. Accepting takes one open slot.</summary>
    [Authorize]
    [HttpPut("{id:guid}/requests/{userId:guid}")]
    public async Task<IActionResult> Decide(Guid id, Guid userId, Decision d)
    {
        if (d.Status is not (JoinRequest.Accepted or JoinRequest.Declined))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["status"] = ["Use accepted or declined."] }));
        if (!await db.SquadPosts.AnyAsync(p => p.Id == id && p.HostId == MeId)) return NotFound();

        // Both updates are conditional, so concurrent accepts can't oversell slots or accept a request twice.
        await using var tx = await db.Database.BeginTransactionAsync();
        var flipped = await db.JoinRequests
            .Where(r => r.PostId == id && r.UserId == userId && r.Status == JoinRequest.Pending)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, d.Status));
        if (flipped == 0) return NotFound();
        if (d.Status == JoinRequest.Accepted)
        {
            var took = await db.SquadPosts.Where(p => p.Id == id).Where(IsOpen(DateTimeOffset.UtcNow))
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.SlotsOpen, p => p.SlotsOpen - 1));
            if (took == 0) return Problem("This squad is closed, full or expired.", statusCode: 409); // tx rolls back on dispose

            // Posts from before M5 have no chat, hence the null check.
            if (await db.Conversations.Where(c => c.SquadPostId == id).Select(c => (Guid?)c.Id).FirstOrDefaultAsync() is { } chat)
            {
                db.ConversationMembers.Add(new ConversationMember { ConversationId = chat, UserId = userId, LastReadAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
        }
        await tx.CommitAsync();
        await SquadChanged(userId, id);
        return NoContent();
    }

    /// <summary>Tells a user's open tabs to reload this squad (replaces polling).</summary>
    Task SquadChanged(Guid userId, Guid postId) => hub.Clients.User(userId.ToString()).SendAsync("SquadChanged", postId);

    async Task<IActionResult?> Onboarded() =>
        await db.Users.AnyAsync(u => u.Id == MeId && u.OnboardedAt != null)
            ? null
            : Problem("Finish your profile first.", statusCode: 403);

    static SquadDto ToDto(SquadPost p) => new(
        p.Id, p.Title, p.Mode, p.Region, p.Server, p.RankMin, p.RankMax, p.NeededRoles, p.NeededAgents, p.SlotsOpen,
        p.MicRequired, p.CreatedAt, p.ExpiresAt, p.ClosedAt == null && p.ExpiresAt > DateTimeOffset.UtcNow && p.SlotsOpen > 0,
        ProfilesController.ToPublic(p.Host));

    /// <summary>Lengths and counts are DataAnnotations; this checks every value is a known meta id.</summary>
    static Dictionary<string, string[]> CheckAgainstMeta(SquadInput s)
    {
        var errors = new Dictionary<string, string[]>();
        void Check(string field, bool ok, string message)
        {
            if (!ok) errors[field] = [message];
        }

        Check("mode", GameData.ModeIds.Contains(s.Mode), "Unknown mode.");
        Check("region", GameData.RegionIds.Contains(s.Region), "Unknown region.");
        Check("server", s.Server is null || GameData.ServerIds(s.Region).Contains(s.Server), "Unknown server.");
        Check("rankMin", s.RankMin is null || GameData.RankTiers.Contains(s.RankMin.Value), "Unknown rank.");
        Check("rankMax", s.RankMax is null || GameData.RankTiers.Contains(s.RankMax.Value), "Unknown rank.");
        Check("rankMax", !(s.RankMin > s.RankMax), "Max rank is below min rank.");
        Check("neededRoles", s.NeededRoles.All(GameData.RoleIds.Contains), "Unknown role.");
        Check("neededAgents", s.NeededAgents.All(GameData.AgentIds.Contains), "Unknown agent.");
        return errors;
    }
}
