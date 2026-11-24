# Bumps or sets the MSIX four-part Identity Version for Microsoft Store uploads.
# Microsoft Store requires the revision (4th segment) to always be 0.
# Scheme: 3.0.5.0 -> 3.0.6.0 -> 3.0.7.0 (increment build segment only).
param(
  [string]$Set,
  [switch]$BumpRevision,
  [switch]$BumpBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$manifest = Join-Path $repoRoot "PartFinder\PartFinder\Package.appxmanifest"
$versionFile = Join-Path $repoRoot "version.json"

function Get-CurrentMsixVersion {
  if (Test-Path $versionFile) {
    $vj = Get-Content $versionFile -Raw | ConvertFrom-Json
    if ($vj.msixVersion) { return [string]$vj.msixVersion }
  }
  [xml]$doc = Get-Content -Path $manifest
  $id = $doc.Package.Identity
  if (-not $id) { $id = $doc.DocumentElement.Identity }
  return $id.GetAttribute("Version")
}

function Assert-StoreRevisionZero([string]$quad) {
  if ($quad -notmatch '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
    throw "MSIX version must be four parts (e.g. 3.0.6.0), got: $quad"
  }
  if ([int]$Matches[4] -ne 0) {
    throw "Microsoft Store requires revision (4th segment) to be 0. Use e.g. 3.0.6.0, not $quad."
  }
}

function Set-MsixVersion([string]$quad) {
  Assert-StoreRevisionZero $quad
  & (Join-Path $PSScriptRoot "set-version.ps1") -Version $quad
}

if ($Set) {
  Set-MsixVersion $Set.Trim()
  Write-Host "Set MSIX version to $Set"
  exit 0
}

$doBump = $BumpBuild -or $BumpRevision
if (-not $doBump) {
  throw "Specify -Set <M.m.b.0>, -BumpBuild, or -BumpRevision (alias for -BumpBuild; revision segment must stay 0)."
}

if ($BumpRevision) {
  Write-Warning "-BumpRevision is deprecated: Store only allows *.0 as the 4th version segment. Using -BumpBuild instead."
}

$current = Get-CurrentMsixVersion
if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
  throw "Current MSIX version is not four-part: $current"
}

$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$build = [int]$Matches[3]
$rev = [int]$Matches[4]

if ($rev -ne 0) {
  Write-Warning "Current version $current has non-zero revision; next Store bump will normalize to *.0."
}

$build++
$next = "$major.$minor.$build.0"
Set-MsixVersion $next
Write-Host "Bumped MSIX version: $current -> $next"
