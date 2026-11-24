# Downloads boyter/scc for Windows x64 into tools/scc/scc.exe (repo-local, not global PATH).
param(
  [string]$Version = "4.1.0",
  [string]$DestDir = (Join-Path $PSScriptRoot "scc")
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $DestDir | Out-Null

$zipName = "scc_Windows_x86_64.zip"
$url = "https://github.com/boyter/scc/releases/download/v$Version/$zipName"
$zipPath = Join-Path $env:TEMP "scc_Windows_x86_64_$Version.zip"

Write-Host "Downloading scc v$Version..."
Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing

$extractRoot = Join-Path $env:TEMP "scc-extract-$Version"
if (Test-Path $extractRoot) { Remove-Item -Recurse -Force $extractRoot }
Expand-Archive -Path $zipPath -DestinationPath $extractRoot -Force

$exe = Get-ChildItem -Path $extractRoot -Filter "scc.exe" -Recurse | Select-Object -First 1
if (-not $exe) { throw "scc.exe not found in release zip" }

Copy-Item -Force $exe.FullName (Join-Path $DestDir "scc.exe")
Write-Host "Installed: $(Join-Path $DestDir 'scc.exe')"
& (Join-Path $DestDir "scc.exe") --version