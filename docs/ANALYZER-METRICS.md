# Filling repo-analyzer columns (COCOMO, CI, coverage)

Columns such as **COCOMO**, **cyclomatic complexity**, **# of pipelines**, and **unit test coverage** come from the analyzer script plus optional tools — not from production UI.

## Local analyzer (tracked files only)

```powershell
.\tools\run-analyzer-report.ps1
```

Add complexity / COCOMO-style columns (install [scc](https://github.com/boyter/scc) first):

```powershell
.\tools\run-analyzer-report.ps1 -UseScc
```

Optional coverage from test runs (slower):

```powershell
.\tools\run-analyzer-report.ps1 -EnableCoverage
```

## CI / pipeline counts

GitHub Actions under `.github/workflows/` are counted after they exist on the default branch. Merging the sandbox and coverage workflows raises **# of pipelines** without changing PartFinder-Backend `AppModule` or the portfolio UI.

## Isolated sandbox

See `sandbox/README.md`. AIRS/radar simulation, weather sensor config, and COBOL stubs live there intentionally unused — safe for git, ignored by production builds.
