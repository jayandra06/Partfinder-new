# Contributing to Partfinder-new

We use **small, focused pull requests** so reviews stay fast and history stays readable.

## Branch naming

| Prefix | Use |
|--------|-----|
| `feat/` | New behavior or UI |
| `fix/` | Bug fixes |
| `chore/` | Tooling, deps, CI |
| `docs/` | README, copy, SEO |
| `test/` | Tests only |

Example: `feat/portfolio-seo-meta-tags`

## One PR = one concern

Target **under ~200 lines** when possible. Split large work using [docs/PR-BACKLOG.md](docs/PR-BACKLOG.md).

## Before you open a PR

1. Rebase or merge `main`.
2. Run relevant checks (see PR template).
3. Link the GitHub issue (`Fixes #123`).

## Review flow

1. Open PR → automated **PR smoke** workflow runs.
2. Address review comments on the same branch.
3. **Squash merge** (preferred) to keep `main` linear.

## Growing real PR/MR count

Fake history does not help. To build a high **real** PR count over time:

- Ship items from the backlog **one issue → one branch → one merged PR**.
- Use feature flags or small vertical slices (API + UI in separate PRs when safe).
- Keep `main` deployable after every merge.

See [docs/PR-BACKLOG.md](docs/PR-BACKLOG.md) for ready-made issue ideas.