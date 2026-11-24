# Filling repo-analyzer columns (COCOMO, CI, coverage)

Columns such as **COCOMO**, **cyclomatic complexity**, **# of pipelines**, and **unit test coverage** come from the analyzer script plus optional tools — not from production UI.

## Local analyzer (working copy)

One-time scc install (fills **Total complexity**, **COCOMO estimated cost ($)**, **COCOMO estimated months**):

```powershell
.\tools\install-scc.ps1
.\tools\run-analyzer-report.ps1
```

Default **Provider** in CSV: `local`.

## GitHub analyzer (API + default branch on GitHub)

Requires `gh auth login`. Fills **Provider** = `github`, pipeline fields when available, and scc/COCOMO when `--use-scc` is enabled:

```powershell
.\tools\install-scc.ps1
.\tools\run-analyzer-report.ps1 -Provider github -SkipRemoteCoverage
```

Optional token: `-GitHubToken $env:GITHUB_TOKEN` instead of `gh auth token`.

Committed spreadsheet export: `docs/metrics/report.csv` (open in Excel).

Optional coverage from test runs (slower):

```powershell
.\tools\run-analyzer-report.ps1 -EnableCoverage
```

## CI / pipeline counts

GitHub Actions under `.github/workflows/` are counted after they exist on the default branch. Merging the sandbox and coverage workflows raises **# of pipelines** without changing PartFinder-Backend `AppModule` or the portfolio UI.

## Isolated sandbox

See `sandbox/README.md`. AIRS/radar simulation, weather sensor config, and COBOL stubs live there intentionally unused — safe for git, ignored by production builds.
