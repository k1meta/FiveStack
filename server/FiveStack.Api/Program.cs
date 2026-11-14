using System.Threading.RateLimiting;
using FiveStack.Api.Data;
using FiveStack.Api.Meta;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Default")));

// Cookie auth via Identity's API endpoints. Unauthenticated API calls get 401, not a redirect.
builder.Services.AddIdentityApiEndpoints<AppUser>(o => o.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>();

// OAuth providers are optional: each one is registered only when its ClientId is set.
var auth = builder.Services.AddAuthentication();
if (config["Authentication:Google:ClientId"] is { Length: > 0 } googleId)
    auth.AddGoogle(o =>
    {
        o.ClientId = googleId;
        o.ClientSecret = config["Authentication:Google:ClientSecret"]!;
        o.CallbackPath = "/api/auth/signin-google"; // under /api so the dev proxy forwards it
        o.SignInScheme = IdentityConstants.ExternalScheme;
    });
if (config["Authentication:Discord:ClientId"] is { Length: > 0 } discordId)
    auth.AddDiscord(o =>
    {
        o.ClientId = discordId;
        o.ClientSecret = config["Authentication:Discord:ClientSecret"]!;
        o.CallbackPath = "/api/auth/signin-discord";
        o.SignInScheme = IdentityConstants.ExternalScheme;
        o.Scope.Add("email");
    });

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("FiveStack");

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// ponytail: migrate on startup, fine for one instance; move to a release step if we ever scale out.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.MapOpenApi();
app.MapScalarApiReference(); // API docs at /scalar

app.UseDefaultFiles();
app.UseStaticFiles(); // Angular build is copied into wwwroot by the Dockerfile
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/meta", () => GameData.All);
app.MapGroup("/api/auth").MapIdentityApi<AppUser>().RequireRateLimiting("auth"); // login with ?useCookies=true
app.MapControllers();
app.MapFallbackToFile("index.html"); // client-side routes

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
