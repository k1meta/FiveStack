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

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/profiles/00000000-0000-0000-0000-000000000000")] // error responses keep them too
    public async Task Security_headers_on_responses(string path)
    {
        var res = await factory.CreateClient().GetAsync(path);
        string Header(string name) => string.Join(",", res.Headers.GetValues(name));

        Assert.Equal("nosniff", Header("X-Content-Type-Options"));
        Assert.Equal("DENY", Header("X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Header("Referrer-Policy"));
        var csp = Header("Content-Security-Policy");
        Assert.StartsWith("default-src 'self';", csp);
        Assert.DoesNotContain("script-src", csp); // scripts fall back to default-src 'self': no inline, no eval, no CDN
        Assert.Contains("frame-ancestors 'none'", csp);
    }
}
