# Syncs version.json and Package.appxmanifest Identity Version from a semver (e.g. 1.2.3).
param(
  [Parameter(Mandatory = $true)]
  [string]$Version
)

$ErrorActionPreference = "Stop"
$semver = $Version.TrimStart('v')
if ($semver -notmatch '^\d+\.\d+\.\d+') {
  throw "Version must be semver like 1.2.3 (got: $Version)"
}

$parts = $semver.Split('.')
while ($parts.Count -lt 4) { $parts += '0' }
$appxVersion = ($parts[0..3] -join '.')

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$versionFile = Join-Path $repoRoot "version.json"
$manifest = Join-Path $repoRoot "PartFinder\PartFinder\Package.appxmanifest"

@{
  version     = ($parts[0..2] -join '.')
  msixVersion = $appxVersion
} | ConvertTo-Json | Set-Content -Path $versionFile -Encoding utf8

$xml = Get-Content -Path $manifest -Raw
$xml = $xml -replace '(<Identity[^>]*\sVersion=")[^"]+(")', "`${1}$appxVersion`${2}"
Set-Content -Path $manifest -Value $xml -Encoding utf8 -NoNewline

Write-Host "version.json -> $($parts[0..2] -join '.')"
Write-Host "Package.appxmanifest Version -> $appxVersion"
