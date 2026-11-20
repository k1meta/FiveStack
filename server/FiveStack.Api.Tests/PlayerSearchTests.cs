using System.Net.Http.Json;
using System.Text.Json;

namespace FiveStack.Api.Tests;

// The DB is shared with other test classes, so every player here is in region "kr" and every query filters on it.
[Collection("api")]
public class PlayerSearchTests(ApiFactory factory)
{
    private Task<(HttpClient Client, string Id)> Player(string email, object? profile) =>
        factory.Player(email, onboard: profile is not null, profile: profile);

    private static object Kr(string name, int rank, string role, string agent) => new
    {
        riotName = name, riotTag = "KR1", region = "kr", rank, roles = new[] { role }, agents = new[] { agent },
    };

    private static async Task<string[]> Ids(HttpClient client, string query)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/players?region=kr&{query}");
        return [.. page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetString()!)];
    }

    [Fact]
    public async Task Filters_by_agent_and_role_overlap_and_rank_range()
    {
        var (_, a) = await Player("kr-a@example.com", Kr("Alpha", 14, "controller", "omen"));
        var (_, b) = await Player("kr-b@example.com", Kr("Bravo", 20, "duelist", "jett"));
        var (_, c) = await Player("kr-c@example.com", Kr("Charlie", 8, "controller", "viper"));
        var (_, d) = await Player("kr-d@example.com", null); // never onboarded
        var (me, myId) = await Player("kr-me@example.com", Kr("Myself", 14, "controller", "omen"));
        var anon = factory.CreateClient();

        var all = await Ids(anon, "");
        Assert.Superset(new HashSet<string> { a, b, c, myId }, all.ToHashSet());
        Assert.DoesNotContain(d, all);

        Assert.Equal(new HashSet<string> { a, c, myId }, (await Ids(anon, "agents=omen&agents=viper")).ToHashSet());
        Assert.Equal(new HashSet<string> { a, myId }, (await Ids(anon, "roles=controller&rankMin=10&rankMax=18")).ToHashSet());

        // Signed in: you don't find yourself.
        Assert.Equal([a], await Ids(me, "roles=controller&rankMin=10&rankMax=18"));

        // Everyone above just made authenticated calls, so they're online.
        Assert.Contains(a, await Ids(anon, "online=true"));
    }
}
