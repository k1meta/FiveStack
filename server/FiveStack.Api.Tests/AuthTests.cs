using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Hosting;

namespace FiveStack.Api.Tests;

[Collection("api")]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_waits_for_the_emailed_confirmation_then_uses_a_cookie()
    {
        var client = factory.CreateClient(); // keeps cookies between requests
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var creds = new { email = "ana@example.com", password = "Passw0rd!" };
        (await client.PostAsJsonAsync("/api/auth/register", creds)).EnsureSuccessStatusCode();
        var early = await client.PostAsJsonAsync("/api/auth/login?useCookies=true", creds);
        Assert.Equal(HttpStatusCode.Unauthorized, early.StatusCode);
        Assert.Equal("NotAllowed", (await early.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());

        Assert.StartsWith($"{ApiFactory.PublicUrl}/confirm-email?userId=", factory.Mail.LastLink("ana@example.com"));
        await factory.ConfirmEmail(client, "ana@example.com");
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", creds)).EnsureSuccessStatusCode();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("ana@example.com", me.GetProperty("email").GetString());

        (await client.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Forgotten_password_is_reset_with_the_emailed_code()
    {
        const string email = "forgetful@example.com";
        await factory.Player(email, onboard: false);
        var anon = factory.CreateClient();

        // Unknown addresses get the same 200 and no email, so the endpoint doesn't reveal who has an account.
        (await anon.PostAsJsonAsync("/api/auth/forgotPassword", new { email = "nobody-here@example.com" })).EnsureSuccessStatusCode();
        Assert.Equal(0, factory.Mail.CountTo("nobody-here@example.com"));

        (await anon.PostAsJsonAsync("/api/auth/forgotPassword", new { email })).EnsureSuccessStatusCode();
        var link = new Uri(factory.Mail.LastLink(email));
        Assert.StartsWith($"{ApiFactory.PublicUrl}/reset-password?", link.ToString());
        var code = HttpUtility.ParseQueryString(link.Query)["code"];

        var reset = await anon.PostAsJsonAsync("/api/auth/resetPassword", new { email, resetCode = code, newPassword = "N3w-passw0rd" });
        Assert.True(reset.IsSuccessStatusCode, await reset.Content.ReadAsStringAsync());
        var old = await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        (await anon.PostAsJsonAsync("/api/auth/login", new { email, password = "N3w-passw0rd" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        const string email = "guessed@example.com";
        await factory.Player(email, onboard: false);
        var attacker = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            await attacker.PostAsJsonAsync("/api/auth/login", new { email, password = $"Wr0ng-{i}!" });

        var res = await attacker.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" }); // right one, too late
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("LockedOut", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Email_links_ignore_a_forged_Host_header()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "evil.example";
        var res = await client.PostAsJsonAsync("/api/auth/register", new { email = "host@example.com", password = "Passw0rd!" });
        res.EnsureSuccessStatusCode();
        Assert.StartsWith($"{ApiFactory.PublicUrl}/confirm-email?", factory.Mail.LastLink("host@example.com"));
    }

    [Fact]
    public async Task External_login_rejects_offsite_returnUrl()
    {
        var res = await factory.CreateClient().GetAsync("/api/auth/external/Google?returnUrl=https://evil.example");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Only_sign_in_endpoints_are_auth_rate_limited()
    {
        using var strict = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:AuthPerMinute", "2"));
        var client = strict.CreateClient();

        // The SPA calls these on every page load, so they must not eat into the sign-in budget.
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
            (await client.GetAsync("/api/auth/providers")).EnsureSuccessStatusCode();
        }

        var creds = new { email = "nobody@example.com", password = "Wr0ng-pass" };
        HttpResponseMessage res = null!;
        for (var i = 0; i < 3; i++) res = await client.PostAsJsonAsync("/api/auth/login", creds);
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        Assert.NotNull(res.Headers.RetryAfter);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
    }
}
