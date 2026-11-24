# Repo analyzer report: local clone or GitHub API (COCOMO via tools/install-scc.ps1 + --use-scc).
param(
  [ValidateSet("local", "github")]
  [string]$Provider = "local",
  [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
  [string]$GithubProject = "jayandra06/Partfinder-new",
  [string]$AnalyzerScript = "c:\Users\euroa\OneDrive\Desktop\Jay Docs\Final Script\repo_analyzer_UPDATED.py",
  [string]$SccPath = (Join-Path $PSScriptRoot "scc\scc.exe"),
  [string]$OutputCsv = (Join-Path $RepoRoot "docs\metrics\report.csv"),
  [string]$GitHubToken = "",
  [switch]$UseScc = $true,
  [switch]$EnableCoverage,
  [switch]$SkipSccInstall,
  [switch]$SkipRemoteCoverage
)

$detailJson = [System.IO.Path]::ChangeExtension($OutputCsv, ".json")
$detailJson = $detailJson -replace '\.json$', '_detail.json'
$detailJson = Join-Path (Split-Path $OutputCsv) "report_detail.json"

if ($UseScc -and -not (Test-Path $SccPath)) {
  if ($SkipSccInstall) {
    Write-Warning "scc not found at $SccPath — run .\tools\install-scc.ps1 or use -UseScc:`$false"
    $UseScc = $false
  } else {
    Write-Host "Installing scc into tools/scc..."
    & (Join-Path $PSScriptRoot "install-scc.ps1")
  }
}

$metricsDir = Split-Path $OutputCsv
if (-not (Test-Path $metricsDir)) {
  New-Item -ItemType Directory -Force -Path $metricsDir | Out-Null
}

$argsList = @(
  "--provider", $Provider,
  "--name", "Partfinder",
  "--output", $OutputCsv
)

if ($Provider -eq "local") {
  $argsList += "--path", $RepoRoot
  $argsList += "--local-mrs", "merges"
} else {
  if (-not $GitHubToken) {
    try { $GitHubToken = (gh auth token 2>$null) } catch { }
  }
  if (-not $GitHubToken) {
    throw "GitHub provider requires a token: set GITHUB_TOKEN or run gh auth login"
  }
  $argsList += "--project", $GithubProject
  $argsList += "--token", $GitHubToken
  if ($SkipRemoteCoverage) { $argsList += "--skip-remote-coverage" }
}

if ($UseScc -and (Test-Path $SccPath)) {
  $argsList += "--use-scc", "--scc-path", $SccPath
}

if ($EnableCoverage) { $argsList += "--enable-code-execution" }

Write-Host "Running repo analyzer -> $OutputCsv"
& python $AnalyzerScript @argsList

# Convenience copy at repo root for local Excel opens (not required for git).
$rootCopy = Join-Path $RepoRoot "report.csv"
if ((Resolve-Path $OutputCsv).Path -ne (Resolve-Path $rootCopy -ErrorAction SilentlyContinue).Path) {
  Copy-Item -Force $OutputCsv $rootCopy -ErrorAction SilentlyContinue
}
$detailSrc = Join-Path $RepoRoot "report_detail.json"
if (Test-Path $detailSrc) {
  Copy-Item -Force $detailSrc (Join-Path $metricsDir "report_detail.json")
}