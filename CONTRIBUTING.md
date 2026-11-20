# Contributing

## Workflow
1. Branch from `main`: `feat/<thing>`, `fix/<thing>`, `docs/<thing>`.
2. Keep PRs small, ideally one roadmap item each.
3. CI must pass before merge. Merging to `main` deploys (Render waits for green checks).

## Commits
[Conventional Commits](https://www.conventionalcommits.org/): `feat(squads): add join requests`, `fix(chat): enforce blocks on send`, `docs: update roadmap`.

## Before opening a PR
CI runs the same commands:
```bash
dotnet tool restore
cd server && dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet ef migrations has-pending-model-changes --project FiveStack.Api && dotnet test
cd client && npx prettier --check . && npm run lint && npm run build && npm test -- --watch=false
docker compose --profile app up -d --build && cd client && npm run e2e
```

## Code style
- `.editorconfig` covers formatting (`dotnet format` for the server). Prettier formats the client (`npx prettier --write .`) and angular-eslint lints it, including template accessibility rules.
- Bulk-formatting commits go in `.git-blame-ignore-revs`, so `git blame` skips them.
- Prefer the framework and standard library over new dependencies.

## Dependencies and security
- Dependabot opens grouped weekly PRs for NuGet, npm, GitHub Actions and Docker base images.
- Repo settings, which live outside the code: CodeQL default setup (C# + JS/TS), secret scanning with push protection, and a `main` ruleset that requires a PR and passing `client`, `server` and `e2e` checks.
- Report vulnerabilities privately: see [SECURITY.md](SECURITY.md).
