# Contributing

## Workflow
1. Branch from `main`: `feat/<thing>`, `fix/<thing>`, `docs/<thing>`.
2. Keep PRs small, ideally one roadmap item each.
3. CI (build + tests) must pass before merge. Merging to `main` deploys.

## Commits
[Conventional Commits](https://www.conventionalcommits.org/): `feat(squads): add join requests`, `fix(chat): enforce blocks on send`, `docs: update roadmap`.

## Before opening a PR
```bash
cd server && dotnet test
cd client && npm run build && npm test -- --watch=false
```

## Code style
- `.editorconfig` covers formatting. Prettier handles the client (`npx prettier --write src`).
- Prefer the framework and standard library over new dependencies.
