# Bumps or sets the MSIX four-part Identity Version for Microsoft Store uploads.
# Scheme: 3.0.3.0 -> 3.0.3.1 ... 3.0.3.9 -> 3.0.4.0 (revision 0-9, then increment build).
param(
  [string]$Set,
  [switch]$BumpRevision
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

function Set-MsixVersion([string]$quad) {
  if ($quad -notmatch '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
    throw "MSIX version must be four parts (e.g. 3.0.3.0), got: $quad"
  }
  & (Join-Path $PSScriptRoot "set-version.ps1") -Version $quad
}

if ($Set) {
  Set-MsixVersion $Set.Trim()
  Write-Host "Set MSIX version to $Set"
  exit 0
}

if (-not $BumpRevision) {
  throw "Specify -Set <M.m.b.r> or -BumpRevision"
}

$current = Get-CurrentMsixVersion
if ($current -notmatch '^(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
  throw "Current MSIX version is not four-part: $current"
}

$major = [int]$Matches[1]
$minor = [int]$Matches[2]
$build = [int]$Matches[3]
$rev = [int]$Matches[4]

if ($rev -ge 9) {
  $build++
  $rev = 0
} else {
  $rev++
}

$next = "$major.$minor.$build.$rev"
Set-MsixVersion $next
Write-Host "Bumped MSIX version: $current -> $next"
