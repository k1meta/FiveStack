using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FiveStack.Api.Data;

// Profile values are ids from Meta/GameData.cs, checked on write. Arrays map to Postgres text[].
public class AppUser : IdentityUser<Guid>
{
    [MaxLength(32)] public string? DisplayName { get; set; }
    [MaxLength(32)] public string? DiscordId { get; set; }
    [MaxLength(64)] public string? DiscordUsername { get; set; }
    public bool ShowDiscord { get; set; }

    [MaxLength(16)] public string? RiotName { get; set; }
    [MaxLength(5)] public string? RiotTag { get; set; }
    [MaxLength(16)] public string? Region { get; set; }
    public string[] Servers { get; set; } = [];
    [MaxLength(16)] public string? Platform { get; set; }
    public int? Rank { get; set; } // valorant-api competitive tier: 0 = Unranked, 3 = Iron 1 ... 27 = Radiant
    public int? PeakRank { get; set; }
    public string[] Roles { get; set; } = [];
    public string[] Agents { get; set; } = [];
    public string[] Languages { get; set; } = [];
    [MaxLength(16)] public string? Mic { get; set; }
    public string[] Modes { get; set; } = [];
    [MaxLength(16)] public string? Playstyle { get; set; }
    public string[] PlayTimes { get; set; } = [];
    [MaxLength(300)] public string? Bio { get; set; }
    public DateTimeOffset? OnboardedAt { get; set; } // set on the first profile save
    public DateTimeOffset? LastActiveAt { get; set; } // bumped by authenticated API calls, see Program.cs
    public bool IsDemo { get; set; } // seeded by Data/DemoSeed.cs: can't log in, be messaged or be joined
}
