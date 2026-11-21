# Full local report: CI-detectable tree + optional scc complexity (install scc for COCOMO columns).
param(
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$AnalyzerScript = "c:\Users\euroa\OneDrive\Desktop\Jay Docs\Final Script\repo_analyzer_UPDATED.py",
  [switch]$UseScc,
  [switch]$EnableCoverage
)

$argsList = @(
  "--provider", "local",
  "--path", $RepoRoot,
  "--name", "Partfinder",
  "--output", (Join-Path $RepoRoot "report.csv"),
  "--local-mrs", "merges"
)

if ($UseScc) { $argsList += "--use-scc" }
if ($EnableCoverage) { $argsList += "--enable-code-execution" }

Write-Host "Running repo analyzer..."
& python $AnalyzerScript @argsList
