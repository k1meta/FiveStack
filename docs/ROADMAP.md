# Roadmap

## M0: Scaffold
- [x] Git repo, Angular 22 client, .NET 10 API + test project
- [x] Tailwind theme, dev proxy, app shell with Riot disclaimer
- [x] docker-compose (Postgres), multi-stage Dockerfile, CI + deploy hook + keep-warm workflows
- [x] README, architecture, roadmap, deployment, contributing, LICENSE

## M1: Auth
- [x] `AppUser`, `AppDbContext`, first EF migration
- [x] Identity cookie auth (`MapIdentityApi`) + logout
- [x] Google + Discord external login, Discord account linking
- [x] Data Protection keys persisted in Postgres
- [x] Login / register pages, `AuthService`, `authGuard`

## M2: Profiles
- [x] `Meta/GameData.cs` + `agents.json` (from valorant-api.com), `GET /api/meta`
- [x] `GET/PUT /api/me/profile` with validation against meta
- [x] Onboarding wizard (Riot ID, region/servers, platform, ranks, roles, agents, languages, mic, modes, playstyle, play times, bio)
- [x] Public profile page with "Copy Discord" + "View on tracker.gg"
- [x] Settings: edit profile, linked accounts, delete account

## M3: Player finder
- [x] `GET /api/players` with all filters + GIN indexes
- [x] Filter sidebar synced to URL, player cards, pagination, "online now"
- [x] Integration test: agent/role overlap + rank range

## M4: Squads
- [x] `SquadPost`, `JoinRequest`, expiry (3h), one open post per host
- [x] Squads board + filters synced to URL, create post form
- [x] Join request, cancel, accept/decline (atomic slot count), squad page with roster + Discord
- [x] Integration tests: board filters, one open post, accept until full, expiry

## M5: Chat
- [ ] `ChatHub`, conversations, messages, unread counts
- [ ] Squad conversation: host on post create, accepted player added on accept; push join requests instead of the 15s poll
- [ ] Presence ("online now"), blocks, rate limits
- [ ] Messages UI (list + thread), live updates
- [ ] Integration tests: non-member can't read/send, blocked user can't DM

## M6: Landing + art
- [ ] Higgsfield art on the Starter plan (200 credits total, no top-ups planned). Original characters only. Stills first: hero, 4 role cards, OG image (reuse a crop of the hero). Check each model's credit cost before generating, and keep a few credits back for one retry.
- [ ] Hero motion: CSS (slow pan/zoom on the still). A looping Higgsfield video only if enough credits are left after the stills.
- [ ] Landing page: hero, how it works, role showcase, CTA
- [ ] `/privacy`, `/terms`
- [ ] Responsive + accessibility pass

## M7: Ship
- [ ] Neon database + Render web service (Docker)
- [ ] Production OAuth redirect URIs
- [ ] Seed ~150 demo profiles (Bogus, `IsDemo` badge)
- [ ] README screenshots + GIF, live link on resume

## Later (not in MVP)
- Riot Sign On (RSO) rank verification (requires a Riot production API key)
- Email confirmation + password reset (Resend)
- Report user / moderation queue
- "Quick match" compatibility scoring
- Redis SignalR backplane (only if running more than one instance)
