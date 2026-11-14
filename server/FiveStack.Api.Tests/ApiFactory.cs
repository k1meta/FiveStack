using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace FiveStack.Api.Tests;

// The API against a throwaway Postgres container. Migrations run on app startup.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseSetting("ConnectionStrings:Default", _db.GetConnectionString())
        .UseSetting("RateLimits:AuthPerMinute", "1000");

    public Task InitializeAsync() => _db.StartAsync();

    /// <summary>A signed-in client (cookie auth) and the player's id. Onboarded in region "br" unless told otherwise.</summary>
    public async Task<(HttpClient Client, string Id)> Player(string email, int rank = 14, bool onboard = true)
    {
        var client = CreateClient();
        var creds = new { email, password = "Passw0rd!" };
        (await client.PostAsJsonAsync("/api/auth/register", creds)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/auth/login?useCookies=true", creds)).EnsureSuccessStatusCode();
        if (onboard)
        {
            var profile = new { riotName = "Br" + rank, riotTag = "BR1", region = "br", rank, roles = new[] { "duelist" } };
            (await client.PutAsJsonAsync("/api/me/profile", profile)).EnsureSuccessStatusCode();
        }
        var id = (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetString()!;
        return (client, id);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}

// One container shared by every test class in the collection.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
