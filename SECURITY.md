# Security policy

## Reporting a vulnerability

Please report vulnerabilities privately through GitHub: **Security → Report a vulnerability** on this repository. Don't open a public issue.

Include the steps to reproduce and what an attacker gains. I'll acknowledge the report within a few days and credit you in the fix unless you'd rather stay anonymous.

Only the live deployment (`main`) is supported. Please test against your own local copy (`docker compose --profile app up -d --build`) rather than the live site, and don't access other players' data.

## How the app is protected

The full checklist, with the reasoning, is in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#security-checklist). In short:

- Same-origin `HttpOnly`, `Secure`, `SameSite=Lax` cookie auth (ASP.NET Core Identity), JSON-only bodies, account lockout after failed logins.
- OAuth sign-in never auto-links accounts by email, and keeps a provider's email only if the provider verified it (prevents pre-account hijacking).
- Email confirmation for password accounts. Reset and confirmation links are built from configuration, never the Host header, and "forgot password" doesn't reveal whether an account exists.
- Optional TOTP two-factor authentication with one-time recovery codes, for password and Google/Discord sign-ins.
- Authorization on every chat read and send (non-members get 404), blocks enforced on DMs.
- Per-IP and per-user rate limits; a 429 carries `Retry-After`.
- A strict Content Security Policy (no inline or third-party scripts), HSTS and the other standard security headers.
- Public profiles expose no email or real name. Discord shows only when the player opts in.
- Errors return problem details without stack traces. The container runs as a non-root user.
- Dependabot, CodeQL, `npm audit` and NuGet vulnerability audit in CI.
