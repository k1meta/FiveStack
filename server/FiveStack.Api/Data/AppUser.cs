using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FiveStack.Api.Data;

// Profile fields (rank, roles, agents, ...) arrive with M2's migration.
public class AppUser : IdentityUser<Guid>
{
    [MaxLength(32)] public string? DisplayName { get; set; }
    [MaxLength(32)] public string? DiscordId { get; set; }
    [MaxLength(64)] public string? DiscordUsername { get; set; }
}
