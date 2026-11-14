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
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(UserManager<AppUser> users, SignInManager<AppUser> signIn) : ControllerBase
{
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
    public async Task<IActionResult> External(string provider, string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) return BadRequest("returnUrl must be a local path.");
        if ((await signIn.GetExternalAuthenticationSchemesAsync()).All(s => s.Name != provider)) return NotFound();

        var callback = $"/api/auth/external/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var userId = User.Identity?.IsAuthenticated == true ? users.GetUserId(User) : null;
        return Challenge(signIn.ConfigureExternalAuthenticationProperties(provider, callback, userId), provider);
    }

    [HttpGet("external/callback")]
    public async Task<IActionResult> Callback(string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";

        var current = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        // Passing the user id checks the xsrf value set in External(), so a link can't be forged.
        var info = await signIn.GetExternalLoginInfoAsync(current?.Id.ToString());
        if (info is null) return Redirect("/login?error=external-failed");

        if (current is not null) return await Link(current, info, returnUrl);

        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
        if (result.Succeeded) return Redirect(returnUrl);
        if (result.IsLockedOut || result.IsNotAllowed) return Redirect("/login?error=not-allowed");

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        // Never auto-link by email: password-account emails are unverified, so that would allow pre-account hijacking.
        if (email is not null && await users.FindByEmailAsync(email) is not null)
            return Redirect("/login?error=email-in-use");

        var user = new AppUser
        {
            UserName = email ?? $"{info.LoginProvider}-{info.ProviderKey}",
            Email = email,
            DisplayName = Truncate(info.Principal.FindFirstValue(ClaimTypes.Name), 32),
        };
        ApplyDiscord(user, info);
        if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddLoginAsync(user, info)).Succeeded)
            return Redirect("/login?error=external-failed");

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        await signIn.SignInAsync(user, isPersistent: true, info.LoginProvider);
        return Redirect($"/onboarding?returnUrl={Uri.EscapeDataString(returnUrl)}"); // new account: set up the profile first
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

    private static string? Truncate(string? s, int max) => s is { Length: > 0 } && s.Length > max ? s[..max] : s;
}
