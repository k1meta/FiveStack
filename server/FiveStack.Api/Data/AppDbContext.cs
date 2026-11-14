using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FiveStack.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    // Cookie-encryption keys live in Postgres so logins survive restarts and redeploys.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        // GIN indexes back the player search's "any of" filters (array overlap, `&&`).
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.Servers).HasMethod("gin");
            e.HasIndex(u => u.Roles).HasMethod("gin");
            e.HasIndex(u => u.Agents).HasMethod("gin");
            e.HasIndex(u => u.Languages).HasMethod("gin");
            e.HasIndex(u => u.Modes).HasMethod("gin");
            e.HasIndex(u => u.PlayTimes).HasMethod("gin");
        });
    }
}
