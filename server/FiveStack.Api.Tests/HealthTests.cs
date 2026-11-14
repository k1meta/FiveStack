using Microsoft.AspNetCore.Mvc.Testing;

namespace FiveStack.Api.Tests;

public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await factory.CreateClient().GetAsync("/api/health");
        res.EnsureSuccessStatusCode();
    }
}
