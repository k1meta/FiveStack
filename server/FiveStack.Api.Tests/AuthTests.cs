using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace FiveStack.Api.Tests;

[Collection("api")]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_login_me_logout_with_cookie()
    {
        var client = factory.CreateClient(); // keeps cookies between requests
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var creds = new { email = "ana@example.com", password = "Passw0rd!" };
        (await client.PostAsJsonAsync("/api/auth/register", creds)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", creds)).EnsureSuccessStatusCode();

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("ana@example.com", me.GetProperty("email").GetString());

        (await client.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
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
