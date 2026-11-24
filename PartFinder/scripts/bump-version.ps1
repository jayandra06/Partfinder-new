# Bumps semver in .github/.release-please-manifest.json and syncs version files.
param(
  [ValidateSet("patch", "minor", "major")]
  [string]$Bump = "patch"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$manifestPath = Join-Path $repoRoot ".github\.release-please-manifest.json"
$changelogPath = Join-Path $repoRoot "CHANGELOG.md"

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$current = [string]$manifest.'.'
if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
  throw "Invalid manifest version: $current"
}

$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$patch = [int]$Matches[3]

switch ($Bump) {
  "major" { $major++; $minor = 0; $patch = 0 }
  "minor" { $minor++; $patch = 0 }
  "patch" { $patch++ }
}

$newVersion = "$major.$minor.$patch"
@{
  "." = $newVersion
} | ConvertTo-Json | Set-Content -Path $manifestPath -Encoding utf8

& (Join-Path $PSScriptRoot "set-version.ps1") -Version $newVersion

$today = (Get-Date).ToString("yyyy-MM-dd")
$entry = @"

## [$newVersion] - $today

Automated release bump ($Bump).

"@

if (Test-Path $changelogPath) {
  $existing = Get-Content $changelogPath -Raw
  if ($existing -match "# Changelog") {
    $updated = $existing -replace "(# Changelog\r?\n)", "`$1$entry"
    Set-Content -Path $changelogPath -Value $updated -Encoding utf8
  } else {
    Add-Content -Path $changelogPath -Value $entry
  }
}

Write-Host "Bumped version: $current -> $newVersion ($Bump)"
