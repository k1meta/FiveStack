using System.Security.Claims;
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

var authPerMinute = config.GetValue("RateLimits:AuthPerMinute", 20); // tests raise it: they all share one "IP"
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // Squad posts: per signed-in user (the endpoint requires auth).
    o.AddPolicy("squads", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
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

// "Online now" heartbeat: stamp LastActiveAt at most once a minute per signed-in user.
// ponytail: one PK update per authenticated API call; M5's SignalR presence can replace it.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && ctx.User.Identity?.IsAuthenticated == true
        && Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
    {
        var now = DateTimeOffset.UtcNow;
        var stale = now.AddMinutes(-1);
        await ctx.RequestServices.GetRequiredService<AppDbContext>().Users
            .Where(u => u.Id == id && (u.LastActiveAt == null || u.LastActiveAt < stale))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActiveAt, now));
    }
    await next();
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/meta", () => GameData.All);
app.MapGroup("/api/auth").MapIdentityApi<AppUser>().RequireRateLimiting("auth"); // login with ?useCookies=true
app.MapControllers();
app.MapFallbackToFile("index.html"); // client-side routes

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
