using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FiveStack.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FiveStack.Api.Tests;

[Collection("api")]
public class ProfileTests(ApiFactory factory)
{
    private static readonly object ValidProfile = new
    {
        riotName = "Ana", riotTag = "EUW1", region = "eu", servers = new[] { "frankfurt", "paris" },
        platform = "pc", rank = 14, peakRank = 17, roles = new[] { "controller" }, agents = new[] { "omen", "viper" },
        languages = new[] { "en" }, mic = "always", modes = new[] { "competitive" }, playstyle = "balanced",
        playTimes = new[] { "weekday-evening" }, bio = "IGL, chill comms.", showDiscord = false,
    };

    private async Task<HttpClient> SignedIn(string email) => (await factory.Player(email, onboard: false)).Client;

    [Fact]
    public async Task Meta_lists_agents_and_ranks()
    {
        var meta = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/meta");
        Assert.True(meta.GetProperty("agents").GetArrayLength() >= 25);
        Assert.Contains(meta.GetProperty("ranks").EnumerateArray(),
            r => r.GetProperty("tier").GetInt32() == 27 && r.GetProperty("name").GetString() == "Radiant");
    }

    [Fact]
    public async Task Profile_requires_auth()
    {
        var res = await factory.CreateClient().GetAsync("/api/me/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Rejects_values_not_in_meta()
    {
        var client = await SignedIn("bad@example.com");
        var res = await client.PutAsJsonAsync("/api/me/profile", new
        {
            riotName = "Ana", riotTag = "EUW1", region = "eu", rank = 14, roles = new[] { "controller" },
            agents = new[] { "not-an-agent" }, servers = new[] { "seoul" }, // seoul isn't an EU server
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("agents", out _));
        Assert.True(errors.TryGetProperty("servers", out _));
    }

    [Fact]
    public async Task Save_round_trips_and_public_profile_hides_private_fields()
    {
        var client = await SignedIn("saver@example.com");
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("onboarded").GetBoolean());

        (await client.PutAsJsonAsync("/api/me/profile", ValidProfile)).EnsureSuccessStatusCode();

        var mine = await client.GetFromJsonAsync<JsonElement>("/api/me/profile");
        Assert.Equal("EUW1", mine.GetProperty("riotTag").GetString());
        Assert.Equal(["omen", "viper"], mine.GetProperty("agents").EnumerateArray().Select(a => a.GetString()));

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("onboarded").GetBoolean());

        // Google sign-ups get DisplayName from the Google account: a real name that must stay private.
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
                .Where(u => u.Email == "saver@example.com")
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DisplayName, "Ana Realname"));

        var id = me.GetProperty("id").GetString();
        var res = await factory.CreateClient().GetAsync($"/api/profiles/{id}"); // anonymous viewer
        var body = await res.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("\"rank\":14", body);
        Assert.DoesNotContain("saver@example.com", body);
        Assert.DoesNotContain("Ana Realname", body);
    }

    [Fact]
    public async Task Public_profile_404s_until_onboarded()
    {
        var client = await SignedIn("fresh@example.com");
        var id = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profiles/{id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_account_signs_out_and_removes_user()
    {
        var client = await SignedIn("gone@example.com");
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login?useCookies=true",
            new { email = "gone@example.com", password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
