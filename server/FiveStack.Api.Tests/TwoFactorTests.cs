using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace FiveStack.Api.Tests;

[Collection("api")]
public class TwoFactorTests(ApiFactory factory)
{
    /// <summary>RFC 6238 TOTP (HMAC-SHA1, 30s, 6 digits), what an authenticator app shows for this key.</summary>
    private static string Totp(string base32Key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var key = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in base32Key.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            if ((bits += 5) >= 8) key.Add((byte)(buffer >> (bits -= 8)));
        }
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);
        var hash = HMACSHA1.HashData(key.ToArray(), counter);
        var offset = hash[^1] & 0x0f;
        var value = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private static async Task<string> Detail(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;

    [Fact]
    public async Task Login_needs_the_authenticator_code_or_a_one_time_recovery_code()
    {
        const string email = "tfa@example.com";
        var (client, _) = await factory.Player(email, onboard: false);

        var setup = await (await client.PostAsJsonAsync("/api/auth/manage/2fa", new { })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(setup.GetProperty("isTwoFactorEnabled").GetBoolean());
        var key = setup.GetProperty("sharedKey").GetString()!;

        var wrongSetup = await client.PostAsJsonAsync("/api/auth/manage/2fa", new { enable = true, twoFactorCode = "12345" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongSetup.StatusCode); // a bad code doesn't turn it on
        var on = await (await client.PostAsJsonAsync("/api/auth/manage/2fa", new { enable = true, twoFactorCode = Totp(key) }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(on.GetProperty("isTwoFactorEnabled").GetBoolean());
        var recovery = on.GetProperty("recoveryCodes")[0].GetString()!;

        var creds = new { email, password = "Passw0rd!" };
        var noCode = await factory.CreateClient().PostAsJsonAsync("/api/auth/login?useCookies=true", creds);
        Assert.Equal(HttpStatusCode.Unauthorized, noCode.StatusCode);
        Assert.Equal("RequiresTwoFactor", await Detail(noCode));

        var device = factory.CreateClient();
        var withCode = await device.PostAsJsonAsync("/api/auth/login?useCookies=true", new { email, password = "Passw0rd!", twoFactorCode = Totp(key) });
        Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/auth/me")).StatusCode);

        var withRecovery = new { email, password = "Passw0rd!", twoFactorRecoveryCode = recovery };
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().PostAsJsonAsync("/api/auth/login?useCookies=true", withRecovery)).StatusCode);
        var reused = await factory.CreateClient().PostAsJsonAsync("/api/auth/login?useCookies=true", withRecovery);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode); // each recovery code works once
    }

    [Fact]
    public async Task External_second_step_needs_a_pending_sign_in()
    {
        // Without the TwoFactorUserId cookie from a Google/Discord callback there is nobody to sign in.
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/2fa", new { code = "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Equal("Failed", await Detail(res));
    }
}
