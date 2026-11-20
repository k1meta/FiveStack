# Deployment

Production setup costs **$0/month**: one Docker web service on **Render** (free) and **Neon** Postgres (free).

## 1. Database: Neon

The repo is linked to a Neon project (`neon.ts`, `.neon`). With the Neon CLI (`npm i -g neon`):

```bash
neon login
neon deploy      # applies neon.ts and pulls DATABASE_URL + DATABASE_URL_UNPOOLED into .env.local (gitignored)
```

The app reads both URLs directly (`PgUrl` in `Program.cs` turns them into Npgsql connection strings):

- `DATABASE_URL` (pooled, `-pooler` host) for app traffic.
- `DATABASE_URL_UNPOOLED` (direct) for the startup migrations and demo seed. PgBouncer's transaction mode doesn't suit EF migrations.

Migrations run automatically on app startup. To run the API locally against Neon: `set -a; . ./.env.local; set +a`, then `dotnet run` in `server/FiveStack.Api`.

## 2. App: Render

1. New **Web Service**, connect the GitHub repo, runtime **Docker** (uses the root `Dockerfile`).
2. Instance type: **Free**. Health check path: `/api/health`.
3. Environment variables:

   | Key | Value |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `DATABASE_URL` | pooled Neon URL (from `.env.local`) |
   | `DATABASE_URL_UNPOOLED` | direct Neon URL (from `.env.local`) |
   | `Authentication__Google__ClientId` / `__ClientSecret` | from Google Cloud |
   | `Authentication__Discord__ClientId` / `__ClientSecret` | from Discord Developer Portal |
   | `Demo__Seed` | `true`: seeds 150 demo players and keeps 6 demo squad posts open |

4. Settings > Build & Deploy > Auto-Deploy: **After CI Checks Pass**. Render then deploys `main` only after the GitHub Actions CI run is green.
5. Set the GitHub Actions **variable** `APP_URL` (e.g. `https://fivestack.onrender.com`) so `keep-warm.yml` pings the app every 10 minutes. Render's free tier sleeps after 15 minutes of inactivity. One always-on service uses ~744 of the 750 free hours per month.

## OAuth apps

### Google
1. <https://console.cloud.google.com> > APIs & Services > Credentials > **OAuth client ID** (Web application).
2. Authorized redirect URIs:
   - `http://localhost:4200/api/auth/signin-google` (dev, through the Angular proxy)
   - `https://<your-app>.onrender.com/api/auth/signin-google`
3. The OAuth consent screen needs the app's `/privacy` and `/terms` URLs before you publish it.

### Discord
1. <https://discord.com/developers/applications> > New Application > **OAuth2**.
2. Redirects:
   - `http://localhost:4200/api/auth/signin-discord`
   - `https://<your-app>.onrender.com/api/auth/signin-discord`
3. Scopes requested by the app: `identify email`.

### Local secrets

```bash
cd server/FiveStack.Api
dotnet user-secrets init
dotnet user-secrets set "Authentication:Google:ClientId" "<id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<secret>"
dotnet user-secrets set "Authentication:Discord:ClientId" "<id>"
dotnet user-secrets set "Authentication:Discord:ClientSecret" "<secret>"
```

OAuth buttons are hidden when a provider has no ClientId configured, so the app runs fine without them.

## Gotchas

- **Forwarded headers:** Render terminates TLS. The app enables `UseForwardedHeaders` so OAuth builds `https://` redirect URIs.
- **Data Protection keys** are stored in Postgres. Without that, every restart would invalidate all login cookies.
- **Cold starts:** without the keep-warm ping, the first request after idle takes ~30–60s.
- **Custom domain (optional):** add it in Render and update both OAuth redirect URIs.
