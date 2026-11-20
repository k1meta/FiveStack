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
   | `OTEL_EXPORTER_OTLP_ENDPOINT` / `_HEADERS` / `_PROTOCOL` | from Grafana Cloud (see below). Leave unset to turn telemetry off. |
   | `App__PublicUrl` | `https://<your-domain>` (or the `onrender.com` URL). Email links start here. **Required.** |
   | `Email__ResendApiKey` / `Email__From` | from Resend (see below), e.g. `FiveStack <noreply@<your-domain>>`. **Required** while email confirmation is on. |

   The app won't start without the required variables, so set them **before** deploying a version that needs them. Render keeps the old version running when the new one fails its health check.

4. Settings > Build & Deploy > Auto-Deploy: **After CI Checks Pass**. Render then deploys `main` only after the GitHub Actions CI run is green.
5. Set the GitHub Actions **variable** `APP_URL` (e.g. `https://fivestack.onrender.com`) so `keep-warm.yml` pings the app every 10 minutes. Render's free tier sleeps after 15 minutes of inactivity. One always-on service uses ~744 of the 750 free hours per month.

## 3. Domain and email: Render + Resend

Resend sends to any inbox only from a domain you've verified, so email needs a domain of your own.

1. **Domain on Render:** Settings > Custom Domains > add `<your-domain>` (and `www.` if you want it). At your registrar, add the records Render shows: an `ALIAS`/`ANAME` (or `A`) record for the apex and a `CNAME` for `www`. Render issues the TLS certificate.
2. **Resend:** add the domain at <https://resend.com/domains>. At your registrar, add the `MX` + SPF `TXT` records (for the `send.` subdomain) and the DKIM `TXT` record it shows. Add a DMARC record too: `TXT _dmarc.<your-domain> "v=DMARC1; p=none;"`. Then create an API key with "sending access" only.
3. **Render env:** `App__PublicUrl=https://<your-domain>`, `Email__ResendApiKey=re_…`, `Email__From=FiveStack <noreply@<your-domain>>`.
4. **OAuth:** add `https://<your-domain>/api/auth/signin-google` and `…/signin-discord` as redirect URIs, and add the domain to Google's consent screen. Update the GitHub `APP_URL` variable and the README live link.
5. Check it: sign up with a real inbox, confirm, log out, run "Forgot your password?", and reset.

Accounts that existed before email confirmation are marked confirmed by the `GrandfatherEmailConfirmed` migration, so they keep working.

## 4. Observability: Grafana Cloud

1. Create a free Grafana Cloud stack (14-day retention, well within the free limits at this traffic).
2. **Connections > OpenTelemetry (OTLP)**: generate a token, then copy the environment variables it shows into Render:
   - `OTEL_EXPORTER_OTLP_ENDPOINT` (e.g. `https://otlp-gateway-prod-eu-west-2.grafana.net/otlp`)
   - `OTEL_EXPORTER_OTLP_HEADERS` (`Authorization=Basic%20<base64 of instanceId:token>`)
   - `OTEL_EXPORTER_OTLP_PROTOCOL` = `http/protobuf`
3. **Dashboards > New > Import**: upload `docs/observability/grafana-dashboard.json` and pick the `grafanacloud-…-prom` datasource.
4. Explore > Tempo shows request traces with SQL spans. Loki has the JSON logs (`{service_name="fivestack"}`).

Try it locally first: `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel:4317 docker compose --profile app --profile otel up -d --build`, then open Grafana at http://localhost:3000 (admin/admin) and import the same JSON.

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

- **Forwarded headers:** Render terminates TLS. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (set in the Dockerfile) trusts `X-Forwarded-For/Proto`, so OAuth builds `https://` redirect URIs and the per-IP rate limits see the visitor's IP. To check the IP part after a deploy, send 21 bad logins in a minute from one network: the last one should get 429. A login from another network (e.g. your phone on mobile data) right after should still get 401, not 429. If it gets 429 too, every visitor shares one partition: set `ForwardedHeadersOptions.ForwardLimit` to the number of proxies in front of the app.
- **Data Protection keys** are stored in Postgres. Without that, every restart would invalidate all login cookies.
- **Cold starts:** without the keep-warm ping, the first request after idle takes ~30–60s.
- **Custom domain (optional):** add it in Render and update both OAuth redirect URIs.
