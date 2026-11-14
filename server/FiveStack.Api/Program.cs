using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(); // API docs at /scalar

app.UseDefaultFiles();
app.UseStaticFiles(); // Angular build is copied into wwwroot by the Dockerfile

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.MapFallbackToFile("index.html"); // client-side routes

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
