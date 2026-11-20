using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FiveStack.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FiveStack.Api.Tests;

// The DB is shared with other test classes, so every squad here is in region "br" and every board query filters on it.
[Collection("api")]
public class SquadTests(ApiFactory factory)
{
    private static async Task<string> Post(HttpClient host, object squad)
    {
        var res = await host.PostAsJsonAsync("/api/squads", squad);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static object Squad(string title, int? rankMin = null, int? rankMax = null, string[]? roles = null, int slots = 1) => new
    {
        title, mode = "competitive", region = "br", server = "sao-paulo", rankMin, rankMax,
        neededRoles = roles ?? [], slotsOpen = slots,
    };

    private static async Task<string[]> Board(HttpClient client, string query = "")
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/squads?region=br&{query}");
        return [.. page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetString()!)];
    }

    private static Task<HttpResponseMessage> Decide(HttpClient host, string post, string user, string status) =>
        host.PutAsJsonAsync($"/api/squads/{post}/requests/{user}", new { status });

    [Fact]
    public async Task Board_filters_by_role_overlap_and_rank_fit_and_one_open_post_per_host()
    {
        var (h1, _) = await factory.Player("sq-h1@example.com");
        var (h2, _) = await factory.Player("sq-h2@example.com");
        var (h3, _) = await factory.Player("sq-h3@example.com");
        var smokes = await Post(h1, Squad("Need smokes", 10, 18, ["controller"]));
        var entry = await Post(h2, Squad("Need entry", 20, 26, ["duelist"]));
        var anyone = await Post(h3, Squad("Anyone", roles: []));
        var anon = factory.CreateClient();

        Assert.Superset(new HashSet<string> { smokes, entry, anyone }, (await Board(anon)).ToHashSet());
        // A post that needs no specific role matches any role filter.
        Assert.Equal(new HashSet<string> { smokes, anyone }, (await Board(anon, "roles=controller&roles=sentinel")).ToHashSet());
        Assert.Equal(new HashSet<string> { entry, anyone }, (await Board(anon, "rank=22")).ToHashSet());

        var again = await h1.PostAsJsonAsync("/api/squads", Squad("Second"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        (await h1.DeleteAsync($"/api/squads/{smokes}")).EnsureSuccessStatusCode();
        Assert.DoesNotContain(smokes, await Board(anon));
        await Post(h1, Squad("After closing")); // closing frees the host to post again
    }

    [Fact]
    public async Task Accepting_takes_a_slot_and_a_full_squad_leaves_the_board()
    {
        var (host, hostId) = await factory.Player("sq-host@example.com");
        var (a, aId) = await factory.Player("sq-a@example.com");
        var (b, bId) = await factory.Player("sq-b@example.com");
        var (newbie, _) = await factory.Player("sq-new@example.com", onboard: false);
        var post = await Post(host, Squad("One slot", slots: 1));

        Assert.Equal(HttpStatusCode.Forbidden, (await newbie.PostAsync($"/api/squads/{post}/requests", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await host.PostAsync($"/api/squads/{post}/requests", null)).StatusCode);
        (await a.PostAsync($"/api/squads/{post}/requests", null)).EnsureSuccessStatusCode();
        (await b.PostAsync($"/api/squads/{post}/requests", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsync($"/api/squads/{post}/requests", null)).StatusCode);

        // Only the host sees requests, and only the host can decide.
        var seenByA = await a.GetFromJsonAsync<JsonElement>($"/api/squads/{post}");
        Assert.Equal(0, seenByA.GetProperty("requests").GetArrayLength());
        Assert.Equal("pending", seenByA.GetProperty("myStatus").GetString());
        Assert.Equal(2, (await host.GetFromJsonAsync<JsonElement>($"/api/squads/{post}")).GetProperty("requests").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await Decide(a, post, bId, "accepted")).StatusCode);

        (await Decide(host, post, aId, "accepted")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(host, post, bId, "accepted")).StatusCode); // full
        Assert.Equal(HttpStatusCode.NotFound, (await Decide(host, post, aId, "accepted")).StatusCode); // not pending anymore

        var detail = await b.GetFromJsonAsync<JsonElement>($"/api/squads/{post}");
        Assert.Equal("pending", detail.GetProperty("myStatus").GetString()); // the failed accept rolled back
        Assert.Equal(aId, detail.GetProperty("roster")[0].GetProperty("id").GetString());
        Assert.False(detail.GetProperty("post").GetProperty("open").GetBoolean());
        Assert.Equal(0, detail.GetProperty("post").GetProperty("slotsOpen").GetInt32());
        Assert.DoesNotContain(post, await Board(factory.CreateClient()));
        Assert.Equal(hostId, detail.GetProperty("post").GetProperty("host").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Concurrent_accepts_cannot_oversell_slots()
    {
        var (host, _) = await factory.Player("sq-race-host@example.com");
        var post = await Post(host, Squad("Race for one slot", slots: 1));
        var players = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var (p, id) = await factory.Player($"sq-race-{i}@example.com");
            (await p.PostAsync($"/api/squads/{post}/requests", null)).EnsureSuccessStatusCode();
            players.Add(id);
        }

        var results = await Task.WhenAll(players.Select(id => Decide(host, post, id, "accepted")));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.NoContent),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var detail = await host.GetFromJsonAsync<JsonElement>($"/api/squads/{post}");
        Assert.Equal(0, detail.GetProperty("post").GetProperty("slotsOpen").GetInt32());
        Assert.Single(detail.GetProperty("roster").EnumerateArray());
    }

    [Fact]
    public async Task Expired_post_is_off_the_board_and_not_joinable()
    {
        var (host, _) = await factory.Player("sq-exp-host@example.com");
        var (joiner, _) = await factory.Player("sq-exp-joiner@example.com");
        var post = await Post(host, Squad("Old news"));

        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SquadPosts
                .Where(p => p.Id == Guid.Parse(post))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.DoesNotContain(post, await Board(joiner));
        Assert.Equal(HttpStatusCode.Conflict, (await joiner.PostAsync($"/api/squads/{post}/requests", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await host.GetAsync("/api/squads/mine")).StatusCode);
    }
}
