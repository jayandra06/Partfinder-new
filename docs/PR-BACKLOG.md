# PR backlog (small, merge-ready slices)

Pick **one row → one issue → one PR**. Goal: many **real** merged PRs without risky mega-diffs.

## Partfinder-Portfolio

| # | PR title idea | Scope |
|---|----------------|-------|
| P1 | SEO meta + Open Graph | `index.html`, `public/` |
| P2 | Public landing feature grid | `App.tsx` hero |
| P3 | 404 page UX + router `Link` | `NotFoundPage` |
| P4 | Focus-visible + skip link | `index.css` a11y |
| P5 | Wire dashboard KPIs to API | `DashboardSection` |
| P6 | Users admin section (read-only list) | `UsersSection` |
| P7 | Releases from API or static JSON | `ReleasesSection` |
| P8 | Organization table empty-state illustration | CSS only |
| P9 | Dark mode polish for org modal | CSS only |
| P10 | `prefers-reduced-motion` for modals | CSS only |

## PartFinder-Backend

| # | PR title idea | Scope |
|---|----------------|-------|
| B1 | Unit tests for `totp.util` | `auth/totp.util.spec.ts` |
| B2 | Unit tests for `mongo-uri` helpers | `mongo-uri.spec.ts` |
| B3 | Health endpoint response shape | `app.controller` |
| B4 | Swagger tag descriptions | docs only |
| B5 | Rate-limit header on 429 | `rate-limit` module |
| B6 | Validate org plan enum in DTO | `create-organization.dto` |
| B7 | Debug log pagination | `debug` module |
| B8 | Casbin policy seed smoke test | `test/` |

## PartFinder (desktop)

| # | PR title idea | Scope |
|---|----------------|-------|
| D1 | Explorer column tooltips | XAML copy |
| D2 | Favourites empty state message | ViewModel / view |
| D3 | Template picker keyboard nav | View |
| D4 | Additional `PartFinder.Core` tests | `PartFinder.Tests` |
| D5 | Converter unit tests batch | `Converters/*Tests` |
| D6 | Loading skeleton on slow Mongo calls | View |
| D7 | Relation hint copy tweaks | `WorksheetRelationUiText` |
| D8 | App manifest display name consistency | `Package.appxmanifest` |

## Repo / engineering

| # | PR title idea | Scope |
|---|----------------|-------|
| R1 | GitHub issue + PR templates | `.github/` |
| R2 | PR smoke workflow | `.github/workflows` |
| R3 | `CONTRIBUTING.md` | docs |
| R4 | Dependabot config | `.github/dependabot.yml` |
| R5 | CODEOWNERS for areas | `.github/CODEOWNERS` |
| R6 | README badges (build status) | root README |

Ship **3–10 PRs per week** from this list to reach **hundreds of merged PRs** over months—each one is genuine work.