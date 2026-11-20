using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using FiveStack.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace FiveStack.Api.Auth;

/// <summary>
/// Sends Identity's account emails (confirm address, reset password) through Resend's HTTP API.
/// Without Email:ResendApiKey (local dev) it logs the link instead. Program.cs refuses to start in production
/// without a key while email confirmation is required.
/// </summary>
public class EmailSender(IHttpClientFactory http, IConfiguration config, IHostEnvironment env, ILogger<EmailSender> log)
    : IEmailSender<AppUser>
{
    public const string HttpClientName = "resend";

    // Links always start from App:PublicUrl, never the request's Host header. Otherwise a register or
    // forgot-password request with a forged Host would email the victim a valid code on the attacker's site.
    string PublicUrl => config["App:PublicUrl"]!.TrimEnd('/');

    // MapIdentityApi HTML-encodes the link and the code before handing them over (`&` arrives as `&amp;`), so decode first.

    /// <summary>MapIdentityApi's link points at its API endpoint; keep its query and send the user to the SPA page.</summary>
    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        Send(email, "Confirm your FiveStack email",
            "Confirm your email address to finish setting up your FiveStack account.",
            "Confirm email", $"{PublicUrl}/confirm-email{new Uri(WebUtility.HtmlDecode(confirmationLink)).Query}");

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        Send(email, "Reset your FiveStack password",
            "Someone (hopefully you) asked to reset your FiveStack password. The link works once.",
            "Choose a new password",
            $"{PublicUrl}/reset-password?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(WebUtility.HtmlDecode(resetCode))}");

    // MapIdentityApi only sends reset codes.
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) =>
        throw new NotSupportedException("Password resets use codes (SendPasswordResetCodeAsync).");

    async Task Send(string to, string subject, string intro, string action, string link)
    {
        var key = config["Email:ResendApiKey"];
        if (string.IsNullOrEmpty(key))
        {
            if (env.IsDevelopment()) log.LogInformation("Email (not sent, no Resend key) to {To}: {Subject} {Link}", to, subject, link);
            else log.LogError("Email not sent: Email:ResendApiKey is not set");
            return;
        }

        var footer = "If you didn't ask for this, you can ignore this email.";
        var html = $"<p>{HtmlEncoder.Default.Encode(intro)}</p>" +
                   $"<p><a href=\"{HtmlEncoder.Default.Encode(link)}\">{HtmlEncoder.Default.Encode(action)}</a></p>" +
                   $"<p style=\"color:#666\">{HtmlEncoder.Default.Encode(footer)}</p>";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", key) },
            Content = JsonContent.Create(new
            {
                from = config["Email:From"], to = new[] { to }, subject, html, text = $"{intro}\n\n{action}: {link}\n\n{footer}",
            }),
        };
        using var res = await http.CreateClient(HttpClientName).SendAsync(request);
        // Logged, not thrown: a 500 from forgot-password would reveal that the account exists.
        if (!res.IsSuccessStatusCode)
            log.LogError("Resend rejected an email ({Status}): {Body}", (int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }
}

/// <summary>
/// Who may sign in under RequireConfirmedAccount: a confirmed email, or a Google/Discord login (the provider vouches
/// for the account). Without the second part, ExternalLoginSignInAsync would lock out OAuth users with no email.
/// </summary>
public class ConfirmedEmailOrExternalLogin : IUserConfirmation<AppUser>
{
    public async Task<bool> IsConfirmedAsync(UserManager<AppUser> manager, AppUser user) =>
        await manager.IsEmailConfirmedAsync(user) || (await manager.GetLoginsAsync(user)).Count > 0;
}
