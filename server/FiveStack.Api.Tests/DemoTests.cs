using System.Net;
using System.Net.Http.Json;
using FiveStack.Api.Data;
using FiveStack.Api.Meta;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FiveStack.Api.Tests;

// Own container (not the "api" collection): demo rows would otherwise show up in other tests' searches and boards.
public class DemoTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Seed_is_meta_valid_idempotent_and_demo_players_cant_be_messaged_or_joined()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DemoSeed.SeedAsync(db);
            await DemoSeed.SeedAsync(db); // second run is a no-op
            await DemoSeed.TopUpPostsAsync(db);
        }

        using var check = factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var demo = await db2.Users.AsNoTracking().Where(u => u.IsDemo).ToListAsync();
        Assert.Equal(DemoSeed.Players, demo.Count);
        foreach (var u in demo)
        {
            Assert.Contains(u.Region!, GameData.RegionIds);
            Assert.All(u.Servers, s => Assert.Contains(s, GameData.ServerIds(u.Region)));
            Assert.Contains(u.Platform!, GameData.PlatformIds);
            Assert.Contains(u.Rank!.Value, GameData.RankTiers);
            if (u.PeakRank is { } peak) Assert.Contains(peak, GameData.RankTiers);
            Assert.NotEmpty(u.Roles);
            Assert.All(u.Roles, r => Assert.Contains(r, GameData.RoleIds));
            Assert.All(u.Agents, a => Assert.Contains(a, GameData.AgentIds));
            Assert.All(u.Languages, l => Assert.Contains(l, GameData.LanguageIds));
            Assert.Contains(u.Mic!, GameData.MicIds);
            Assert.All(u.Modes, m => Assert.Contains(m, GameData.ModeIds));
            Assert.Contains(u.Playstyle!, GameData.PlaystyleIds);
            Assert.All(u.PlayTimes, t => Assert.Contains(t, GameData.PlayTimeIds));
            Assert.Matches(@"^[\p{L}\p{N}]{3,5}$", u.RiotTag!);
            Assert.InRange(u.RiotName!.Length, 3, 16);
        }

        var now = DateTimeOffset.UtcNow;
        var post = await db2.SquadPosts.AsNoTracking()
            .Where(p => p.Host.IsDemo && p.ClosedAt == null && p.ExpiresAt > now && p.SlotsOpen > 0).ToListAsync();
        Assert.True(post.Count >= DemoSeed.OpenPosts);

        var (me, _) = await factory.Player("demo-visitor@example.com");
        Assert.Equal(HttpStatusCode.Conflict, (await me.PostAsync($"/api/conversations/dm/{demo[0].Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await me.PostAsync($"/api/squads/{post[0].Id}/requests", null)).StatusCode);

        var profile = await me.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/profiles/{demo[0].Id}");
        Assert.True(profile.GetProperty("isDemo").GetBoolean());
    }
}
