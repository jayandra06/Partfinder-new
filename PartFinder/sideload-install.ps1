# Trust the PartFinder dev root certificate and install the MSIX (fixes 0x800B010A / 0x800B0109).
# Must run in an elevated PowerShell (Run as administrator).
# Usage:  powershell -ExecutionPolicy Bypass -File .\PartFinder\sideload-install.ps1

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
  [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
  Write-Error @"
This script must run as Administrator so Windows can trust the package root certificate.

1. Open Start -> PowerShell -> Run as administrator
2. cd to your repo folder
3. powershell -ExecutionPolicy Bypass -File .\PartFinder\sideload-install.ps1

Also enable Developer Mode: Settings -> Privacy & security -> For developers -> Developer Mode.
"@
}

$Root = $PSScriptRoot
$TestDir = Join-Path $Root "PartFinder\AppPackages\PartFinder_1.0.0.0_x64_Test"

if (-not (Test-Path $TestDir)) {
  Write-Error "Package folder not found. Build first: powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix.ps1"
}

$msix = Get-ChildItem $TestDir -Filter "*.msix" | Select-Object -First 1
$cer = Get-ChildItem $TestDir -Filter "*.cer" | Select-Object -First 1
if (-not $msix -or -not $cer) {
  Write-Error "Missing .msix or .cer in $TestDir. Run build-msix.ps1 first."
}

Write-Host "Installing publisher root certificate (Local Machine\Trusted Root)..."
certutil -addstore Root $cer.FullName | Out-Null

Write-Host "Installing $([System.IO.Path]::GetFileName($msix.FullName)) ..."
Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion

Write-Host "PartFinder installed successfully."
