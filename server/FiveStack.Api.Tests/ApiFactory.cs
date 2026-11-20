using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FiveStack.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FiveStack.Api.Tests;

// The API against a throwaway Postgres container. Migrations run on app startup.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PublicUrl = "https://fivestack.test";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Every email the app sent. The real EmailSender runs; only Resend's HTTP API is faked.</summary>
    public MailStub Mail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting("ConnectionStrings:Default", _db.GetConnectionString())
        .UseSetting("RateLimits:AuthPerMinute", "1000")
        .UseSetting("RateLimits:GlobalPerMinute", "100000")
        .UseSetting("Demo:Seed", "false") // appsettings.Development turns it on; tests seed explicitly (DemoTests)
        .UseSetting("DATABASE_URL", "").UseSetting("DATABASE_URL_UNPOOLED", "") // never hit Neon from tests
        .UseSetting("App:PublicUrl", PublicUrl)
        .UseSetting("Email:ResendApiKey", "re_test")
        .UseSetting("Email:From", "FiveStack <noreply@fivestack.test>")
        .ConfigureTestServices(s => s.AddHttpClient(EmailSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Mail));

    public Task InitializeAsync() => _db.StartAsync();

    /// <summary>
    /// A signed-in client (cookie auth) and the player's id: registered, email confirmed through the emailed link,
    /// logged in. Onboarded in region "br" unless told otherwise.
    /// </summary>
    public async Task<(HttpClient Client, string Id)> Player(string email, int rank = 14, bool onboard = true, object? profile = null)
    {
        var client = CreateClient();
        var creds = new { email, password = "Passw0rd!" };
        (await client.PostAsJsonAsync("/api/auth/register", creds)).EnsureSuccessStatusCode();
        await ConfirmEmail(client, email);
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", creds)).EnsureSuccessStatusCode();
        if (onboard)
        {
            profile ??= new { riotName = "Br" + rank, riotTag = "BR1", region = "br", rank, roles = new[] { "duelist" } };
            var res = await client.PutAsJsonAsync("/api/me/profile", profile);
            Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        }
        var id = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
        return (client, id);
    }

    /// <summary>Follows the emailed link the way the SPA's /confirm-email page does: its query goes to the API.</summary>
    public async Task ConfirmEmail(HttpClient client, string email) =>
        (await client.GetAsync($"/api/auth/confirmEmail{new Uri(Mail.LastLink(email)).Query}")).EnsureSuccessStatusCode();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}

/// <summary>Stands in for Resend's API: records each email and answers 200.</summary>
public partial class MailStub : HttpMessageHandler
{
    private readonly ConcurrentQueue<(string To, string Text)> _sent = new();

    public int CountTo(string to) => _sent.Count(m => m.To == to);

    /// <summary>The link in the newest email to this address.</summary>
    public string LastLink(string to) => Link().Match(_sent.Last(m => m.To == to).Text).Value;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("https://api.resend.com/emails", request.RequestUri?.ToString());
        Assert.Equal("Bearer re_test", request.Headers.Authorization?.ToString());
        var mail = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
        _sent.Enqueue((mail.GetProperty("to")[0].GetString()!, mail.GetProperty("text").GetString()!));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = "test" }) };
    }

    [GeneratedRegex(@"https?://\S+")] private static partial Regex Link();
}

// One container shared by every test class in the collection.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
