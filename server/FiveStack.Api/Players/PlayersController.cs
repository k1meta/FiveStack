using System.ComponentModel.DataAnnotations;
using FiveStack.Api.Chat;
using FiveStack.Api.Data;
using FiveStack.Api.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiveStack.Api.Players;

/// <summary>Player search filters. Every one is optional; arrays mean "has any of these" (repeat the key).</summary>
public record PlayerSearch
{
    public string? Region { get; init; }
    [MaxLength(30)] public string[] Servers { get; init; } = [];
    public string? Platform { get; init; }
    public int? RankMin { get; init; }
    public int? RankMax { get; init; }
    [MaxLength(30)] public string[] Roles { get; init; } = [];
    [MaxLength(30)] public string[] Agents { get; init; } = [];
    [MaxLength(30)] public string[] Languages { get; init; } = [];
    public string? Mic { get; init; }
    [MaxLength(30)] public string[] Modes { get; init; } = [];
    public string? Playstyle { get; init; }
    [MaxLength(30)] public string[] PlayTimes { get; init; } = [];
    public bool Online { get; init; }
    public int Page { get; init; } = 1;
}

public record PlayerPage(PublicProfileDto[] Items, int Total, int Page, int PageSize);

[ApiController]
[Route("api/players")]
public class PlayersController(AppDbContext db, UserManager<AppUser> users) : ControllerBase
{
    const int PageSize = 24;

    // Unknown filter ids aren't rejected: they just match nothing.
    [HttpGet]
    public async Task<PlayerPage> Search([FromQuery] PlayerSearch f)
    {
        var q = db.Users.AsNoTracking().Where(u => u.OnboardedAt != null);

        if (users.GetUserId(User) is { } me && Guid.TryParse(me, out var myId)) q = q.Where(u => u.Id != myId);
        if (f.Region is not null) q = q.Where(u => u.Region == f.Region);
        if (f.Platform is not null) q = q.Where(u => u.Platform == f.Platform);
        if (f.Mic is not null) q = q.Where(u => u.Mic == f.Mic);
        if (f.Playstyle is not null) q = q.Where(u => u.Playstyle == f.Playstyle);
        if (f.RankMin is { } min) q = q.Where(u => u.Rank >= min);
        if (f.RankMax is { } max) q = q.Where(u => u.Rank <= max);
        // Npgsql turns `x.Any(f.Contains)` into array overlap (`&&`), which the GIN indexes serve.
        if (f.Servers.Length > 0) q = q.Where(u => u.Servers.Any(x => f.Servers.Contains(x)));
        if (f.Roles.Length > 0) q = q.Where(u => u.Roles.Any(x => f.Roles.Contains(x)));
        if (f.Agents.Length > 0) q = q.Where(u => u.Agents.Any(x => f.Agents.Contains(x)));
        if (f.Languages.Length > 0) q = q.Where(u => u.Languages.Any(x => f.Languages.Contains(x)));
        if (f.Modes.Length > 0) q = q.Where(u => u.Modes.Any(x => f.Modes.Contains(x)));
        if (f.PlayTimes.Length > 0) q = q.Where(u => u.PlayTimes.Any(x => f.PlayTimes.Contains(x)));
        if (f.Online)
        {
            var since = DateTimeOffset.UtcNow - ProfilesController.OnlineWindow;
            var live = Presence.Online();
            q = q.Where(u => u.LastActiveAt > since || live.Contains(u.Id));
        }

        var page = Math.Clamp(f.Page, 1, 10_000); // upper bound keeps Skip() from overflowing
        var total = await q.CountAsync();
        var rows = await q
            .OrderByDescending(u => u.LastActiveAt != null) // Postgres sorts NULLs first on DESC
            .ThenByDescending(u => u.LastActiveAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync();
        return new PlayerPage([.. rows.Select(ProfilesController.ToPublic)], total, page, PageSize);
    }
}
