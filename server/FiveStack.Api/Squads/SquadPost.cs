using System.ComponentModel.DataAnnotations;
using FiveStack.Api.Data;

namespace FiveStack.Api.Squads;

/// <summary>An LFG post. Values are meta ids, like AppUser. Open = not closed, not expired, slots left.</summary>
public class SquadPost
{
    public Guid Id { get; set; }
    public Guid HostId { get; set; }
    public AppUser Host { get; set; } = null!;
    [MaxLength(60)] public string Title { get; set; } = "";
    [MaxLength(16)] public string Mode { get; set; } = "";
    [MaxLength(16)] public string Region { get; set; } = "";
    [MaxLength(16)] public string? Server { get; set; }
    public int? RankMin { get; set; } // null bound = any rank
    public int? RankMax { get; set; }
    public string[] NeededRoles { get; set; } = []; // empty = any role
    public string[] NeededAgents { get; set; } = [];
    public int SlotsOpen { get; set; } // decremented on each accept
    public bool MicRequired { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; } // the host closed it early
    public List<JoinRequest> Requests { get; set; } = [];
}

/// <summary>One per player per post (composite key).</summary>
public class JoinRequest
{
    public const string Pending = "pending", Accepted = "accepted", Declined = "declined";

    public Guid PostId { get; set; }
    public SquadPost Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    [MaxLength(16)] public string Status { get; set; } = Pending;
    public DateTimeOffset CreatedAt { get; set; }
}
