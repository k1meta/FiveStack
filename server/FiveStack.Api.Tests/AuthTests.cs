using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
}
