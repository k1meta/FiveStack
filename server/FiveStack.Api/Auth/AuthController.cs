using System.Security.Claims;
using FiveStack.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FiveStack.Api.Auth;

public record MeDto(Guid Id, string? Email, string? DisplayName, string? DiscordUsername, bool Onboarded, IEnumerable<string> Logins);

// Email/password endpoints (register, login, ...) come from MapIdentityApi in Program.cs.
// Only the sign-in flows are auth-rate-limited: the SPA calls /me and /providers on every page load.
[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn) : ControllerBase
{
    /// <summary>Set from Google's `email_verified` and Discord's `verified` (see Program.cs).</summary>
    public const string EmailVerifiedClaim = "email_verified";

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeDto>> Me()
    {
        var u = await users.GetUserAsync(User);
        if (u is null) return Unauthorized();
        var logins = (await users.GetLoginsAsync(u)).Select(l => l.LoginProvider);
        return new MeDto(u.Id, u.Email, u.DisplayName, u.DiscordUsername, u.OnboardedAt is not null, logins);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    // Configured external providers, so the client only shows working buttons.
    [HttpGet("providers")]
    public async Task<IEnumerable<string>> Providers() =>
        (await signIn.GetExternalAuthenticationSchemesAsync()).Select(s => s.Name);

    // Signed out: log in or sign up. Signed in: link the provider to the current account.
    [HttpGet("external/{provider}")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> External(string provider, string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) return BadRequest("returnUrl must be a local path.");
        if ((await signIn.GetExternalAuthenticationSchemesAsync()).All(s => s.Name != provider)) return NotFound();

        var callback = $"/api/auth/external/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var userId = User.Identity?.IsAuthenticated == true ? users.GetUserId(User) : null;
        return Challenge(signIn.ConfigureExternalAuthenticationProperties(provider, callback, userId), provider);
    }

    [HttpGet("external/callback")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Callback(string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";

        var current = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        // Passing the user id checks the xsrf value set in External(), so a link can't be forged.
        var info = await signIn.GetExternalLoginInfoAsync(current?.Id.ToString());
        if (info is null) return Redirect("/login?error=external-failed");

        if (current is not null) return await Link(current, info, returnUrl);

        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
        if (result.Succeeded)
        {
            var existing = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            return Redirect(existing?.OnboardedAt is null ? Onboarding(returnUrl) : returnUrl);
        }
        if (result.IsLockedOut || result.IsNotAllowed) return Redirect("/login?error=not-allowed");

        // Keep the provider's email only if the provider verified it. An unverified one could belong to someone else,
        // who would then be locked out of signing up with it, or could take this account over with a password reset.
        var verified = string.Equals(info.Principal.FindFirstValue(EmailVerifiedClaim), "true", StringComparison.OrdinalIgnoreCase);
        var email = verified ? info.Principal.FindFirstValue(ClaimTypes.Email) : null;
        // Never auto-link by email: password-account emails are unverified, so that would allow pre-account hijacking.
        if (email is not null && await users.FindByEmailAsync(email) is not null)
            return Redirect("/login?error=email-in-use");

        var user = new AppUser
        {
            UserName = email ?? $"{info.LoginProvider}-{info.ProviderKey}",
            Email = email,
            EmailConfirmed = email is not null,
            DisplayName = Truncate(info.Principal.FindFirstValue(ClaimTypes.Name), 32),
        };
        ApplyDiscord(user, info);
        if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddLoginAsync(user, info)).Succeeded)
            return Redirect("/login?error=external-failed");

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await signIn.SignInAsync(user, isPersistent: true, info.LoginProvider);
        return Redirect(Onboarding(returnUrl));
    }

    private async Task<IActionResult> Link(AppUser user, ExternalLoginInfo info, string returnUrl)
    {
        var added = await users.AddLoginAsync(user, info); // fails if this login already belongs to someone
        if (!added.Succeeded) return Redirect("/settings?error=link-failed");
        ApplyDiscord(user, info);
        await users.UpdateAsync(user);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await signIn.RefreshSignInAsync(user);
        return Redirect(returnUrl);
    }

    private static void ApplyDiscord(AppUser user, ExternalLoginInfo info)
    {
        if (info.LoginProvider != "Discord") return;
        user.DiscordId = info.ProviderKey;
        user.DiscordUsername = Truncate(info.Principal.FindFirstValue(ClaimTypes.Name), 64);
    }

    // No profile yet: set it up first, then continue to where the user was going.
    private static string Onboarding(string returnUrl) => $"/onboarding?returnUrl={Uri.EscapeDataString(returnUrl)}";

    private static string? Truncate(string? s, int max) => s is { Length: > 0 } && s.Length > max ? s[..max] : s;
}
