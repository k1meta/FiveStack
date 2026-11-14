using FiveStack.Api.Chat;
using FiveStack.Api.Squads;
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
    public DbSet<SquadPost> SquadPosts => Set<SquadPost>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Block> Blocks => Set<Block>();

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
        b.Entity<SquadPost>(e =>
        {
            e.HasIndex(p => p.NeededRoles).HasMethod("gin");
            e.HasIndex(p => p.ExpiresAt);
        });
        b.Entity<JoinRequest>().HasKey(r => new { r.PostId, r.UserId });

        b.Entity<Conversation>(e =>
        {
            // Optional FKs default to no-action; cascade so deleting a post (or its host's account) takes the chat along.
            e.HasOne(c => c.SquadPost).WithOne().HasForeignKey<Conversation>(c => c.SquadPostId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.DmKey).IsUnique();
        });
        b.Entity<ConversationMember>(e =>
        {
            e.HasKey(m => new { m.ConversationId, m.UserId });
            e.HasIndex(m => m.UserId);
        });
        b.Entity<Message>().HasIndex(m => new { m.ConversationId, m.Id });
        b.Entity<Block>(e =>
        {
            e.HasKey(x => new { x.BlockerId, x.BlockedId });
            e.HasOne(x => x.Blocker).WithMany().HasForeignKey(x => x.BlockerId);
            e.HasOne(x => x.Blocked).WithMany().HasForeignKey(x => x.BlockedId);
        });
    }
}
