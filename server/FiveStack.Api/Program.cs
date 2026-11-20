using System.Security.Claims;
using System.Threading.RateLimiting;
using FiveStack.Api.Chat;
using FiveStack.Api.Data;
using FiveStack.Api.Meta;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(); // RFC 9457 bodies (with traceId) for exceptions and empty 4xx/5xx

// Neon (`neon env pull`, or Render env vars) gives postgres:// URLs: pooled DATABASE_URL for app traffic,
// direct DATABASE_URL_UNPOOLED for migrations. Without them, ConnectionStrings:Default (local Docker Postgres).
var connectionString = PgUrl(config["DATABASE_URL"]) ?? config.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

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

// Tests raise both limits: they all share one "IP".
var authPerMinute = config.GetValue("RateLimits:AuthPerMinute", 20);
var globalPerMinute = config.GetValue("RateLimits:GlobalPerMinute", 300);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, _) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        return ValueTask.CompletedTask;
    };
    // Every API call, per IP, on top of the named policies below. Caps scraping of the public search endpoints.
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        ctx.Request.Path.StartsWithSegments("/api")
            ? RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = globalPerMinute, Window = TimeSpan.FromMinutes(1) })
            : RateLimitPartition.GetNoLimiter(""));
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // Squad posts: per signed-in user (the endpoint requires auth).
    o.AddPolicy("squads", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));
    // Chat: per signed-in user. Starting DMs is the spam vector, so it's the tighter one.
    o.AddPolicy("messages", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("dm-start", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromHours(1) }));
});
builder.Services.AddSignalR();

var app = builder.Build();

// ponytail: migrate on startup, fine for one instance; move to a release step if we ever scale out.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (PgUrl(config["DATABASE_URL_UNPOOLED"]) is { } direct) db.Database.SetConnectionString(direct); // PgBouncer can't run migrations
    db.Database.Migrate();
    if (config.GetValue<bool>("Demo:Seed"))
    {
        await DemoSeed.SeedAsync(db);
        await DemoSeed.TopUpPostsAsync(db);
    }
}

// Security headers on every response. Set in OnStarting so error responses (which clear headers) keep them too.
// Styles need 'unsafe-inline' for Angular's component <style> tags; scripts stay 'self' only.
const string csp = "default-src 'self'; img-src 'self' data: https://media.valorant-api.com; style-src 'self' 'unsafe-inline'; " +
    "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        var h = ctx.Response.Headers;
        h.XContentTypeOptions = "nosniff";
        h.XFrameOptions = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (!ctx.Request.Path.StartsWithSegments("/scalar")) h.ContentSecurityPolicy = csp; // Scalar runs an inline script
        return Task.CompletedTask;
    });
    await next();
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts(); // only sent on HTTPS requests; Render's proxy forwards the scheme
    app.UseExceptionHandler(); // Development keeps the detailed error page
}
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference(); // API docs at /scalar

app.UseDefaultFiles();
app.UseStaticFiles(); // Angular build is copied into wwwroot by the Dockerfile
app.UseRateLimiter();

// "Online now" heartbeat: stamp LastActiveAt at most once a minute per signed-in user.
// ponytail: one PK update per authenticated API call. Kept next to hub presence: it feeds the "recently active" sort.
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
app.MapHub<ChatHub>("/hubs/chat");
app.MapFallbackToFile("index.html"); // client-side routes

app.Run();

// postgresql://user:pass@host[:port]/db?... to an Npgsql connection string. Npgsql doesn't parse URLs.
// ponytail: SSL + channel binding are always required (Neon's URLs ask for both); parse the query if another host needs less.
static string? PgUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;
    var u = new Uri(url);
    var user = u.UserInfo.Split(':', 2);
    return new NpgsqlConnectionStringBuilder
    {
        Host = u.Host, Port = u.Port > 0 ? u.Port : 5432, Database = u.AbsolutePath.Trim('/'),
        Username = Uri.UnescapeDataString(user[0]), Password = user.Length > 1 ? Uri.UnescapeDataString(user[1]) : null,
        SslMode = SslMode.Require, ChannelBinding = ChannelBinding.Require,
    }.ConnectionString;
}

public partial class Program; // exposed for WebApplicationFactory in tests
