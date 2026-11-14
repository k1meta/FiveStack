namespace FiveStack.Api.Tests;

[Collection("api")]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await factory.CreateClient().GetAsync("/api/health");
        res.EnsureSuccessStatusCode();
    }
}
