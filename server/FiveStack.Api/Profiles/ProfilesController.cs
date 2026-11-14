using System.ComponentModel.DataAnnotations;
using FiveStack.Api.Data;
using FiveStack.Api.Meta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiveStack.Api.Profiles;

/// <summary>The editable part of a profile. Values are ids from GET /api/meta.</summary>
public record ProfileDto
{
    [Required, StringLength(16, MinimumLength = 3)] public string RiotName { get; init; } = "";
    [Required, RegularExpression(@"^[\p{L}\p{N}]{3,5}$", ErrorMessage = "Tag is 3-5 letters or digits.")]
    public string RiotTag { get; init; } = "";
    [Required] public string Region { get; init; } = "";
    [MaxLength(10)] public string[] Servers { get; init; } = [];
    public string? Platform { get; init; }
    [Required] public int? Rank { get; init; }
    public int? PeakRank { get; init; }
    [MinLength(1, ErrorMessage = "Pick at least one role."), MaxLength(4)] public string[] Roles { get; init; } = [];
    [MaxLength(10)] public string[] Agents { get; init; } = [];
    [MaxLength(5)] public string[] Languages { get; init; } = [];
    public string? Mic { get; init; }
    [MaxLength(10)] public string[] Modes { get; init; } = [];
    public string? Playstyle { get; init; }
    [MaxLength(10)] public string[] PlayTimes { get; init; } = [];
    [StringLength(300)] public string? Bio { get; init; }
    public bool ShowDiscord { get; init; }
}

public record PublicProfileDto(
    Guid Id, string? DisplayName, string? RiotName, string? RiotTag, string? Region, string[] Servers,
    string? Platform, int? Rank, int? PeakRank, string[] Roles, string[] Agents, string[] Languages, string? Mic,
    string[] Modes, string? Playstyle, string[] PlayTimes, string? Bio, string? DiscordUsername, bool Online);

[ApiController]
[Route("api")]
public class ProfilesController(UserManager<AppUser> users, SignInManager<AppUser> signIn, AppDbContext db) : ControllerBase
{
    [Authorize]
    [HttpGet("me/profile")]
    public async Task<ActionResult<ProfileDto>> GetMine()
    {
        var u = await users.GetUserAsync(User);
        if (u is null) return Unauthorized();
        return new ProfileDto
        {
            RiotName = u.RiotName ?? "", RiotTag = u.RiotTag ?? "", Region = u.Region ?? "", Servers = u.Servers,
            Platform = u.Platform, Rank = u.Rank, PeakRank = u.PeakRank, Roles = u.Roles, Agents = u.Agents,
            Languages = u.Languages, Mic = u.Mic, Modes = u.Modes, Playstyle = u.Playstyle, PlayTimes = u.PlayTimes,
            Bio = u.Bio, ShowDiscord = u.ShowDiscord,
        };
    }

    [Authorize]
    [HttpPut("me/profile")]
    public async Task<IActionResult> SaveMine(ProfileDto p)
    {
        var errors = CheckAgainstMeta(p);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var u = await users.GetUserAsync(User);
        if (u is null) return Unauthorized();
        u.RiotName = p.RiotName.Trim();
        u.RiotTag = p.RiotTag;
        u.Region = p.Region;
        u.Servers = [.. p.Servers.Distinct()];
        u.Platform = p.Platform;
        u.Rank = p.Rank;
        u.PeakRank = p.PeakRank;
        u.Roles = [.. p.Roles.Distinct()];
        u.Agents = [.. p.Agents.Distinct()];
        u.Languages = [.. p.Languages.Distinct()];
        u.Mic = p.Mic;
        u.Modes = [.. p.Modes.Distinct()];
        u.Playstyle = p.Playstyle;
        u.PlayTimes = [.. p.PlayTimes.Distinct()];
        u.Bio = string.IsNullOrWhiteSpace(p.Bio) ? null : p.Bio.Trim();
        u.ShowDiscord = p.ShowDiscord;
        u.OnboardedAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("profiles/{id:guid}")]
    public async Task<ActionResult<PublicProfileDto>> GetPublic(Guid id)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OnboardedAt != null);
        if (u is null) return NotFound();
        return ToPublic(u);
    }

    /// <summary>"Online now" = an authenticated API call within this window (LastActiveAt).</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);

    /// <summary>What anyone may see about a player. Discord only when the player opted in.</summary>
    internal static PublicProfileDto ToPublic(AppUser u) => new(
        u.Id, u.DisplayName, u.RiotName, u.RiotTag, u.Region, u.Servers, u.Platform, u.Rank, u.PeakRank, u.Roles,
        u.Agents, u.Languages, u.Mic, u.Modes, u.Playstyle, u.PlayTimes, u.Bio,
        u.ShowDiscord ? u.DiscordUsername : null,
        u.LastActiveAt > DateTimeOffset.UtcNow - OnlineWindow);

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount()
    {
        var u = await users.GetUserAsync(User);
        if (u is null) return Unauthorized();
        var result = await users.DeleteAsync(u);
        if (!result.Succeeded) return Problem("Could not delete the account.");
        await signIn.SignOutAsync();
        return NoContent();
    }

    /// <summary>Lengths and counts are DataAnnotations; this checks every value is a known meta id.</summary>
    private static Dictionary<string, string[]> CheckAgainstMeta(ProfileDto p)
    {
        var errors = new Dictionary<string, string[]>();
        void Check(string field, bool ok)
        {
            if (!ok) errors[field] = [$"Unknown {field} value."];
        }

        var servers = GameData.ServerIds(p.Region);
        Check("region", GameData.RegionIds.Contains(p.Region));
        Check("servers", p.Servers.All(servers.Contains));
        Check("platform", p.Platform is null || GameData.PlatformIds.Contains(p.Platform));
        Check("rank", p.Rank is { } r && GameData.RankTiers.Contains(r));
        Check("peakRank", p.PeakRank is null || GameData.RankTiers.Contains(p.PeakRank.Value));
        Check("roles", p.Roles.All(GameData.RoleIds.Contains));
        Check("agents", p.Agents.All(GameData.AgentIds.Contains));
        Check("languages", p.Languages.All(GameData.LanguageIds.Contains));
        Check("mic", p.Mic is null || GameData.MicIds.Contains(p.Mic));
        Check("modes", p.Modes.All(GameData.ModeIds.Contains));
        Check("playstyle", p.Playstyle is null || GameData.PlaystyleIds.Contains(p.Playstyle));
        Check("playTimes", p.PlayTimes.All(GameData.PlayTimeIds.Contains));
        return errors;
    }
}
