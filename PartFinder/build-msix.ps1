# Local unsigned MSIX (sideload identity check). For Store upload use build-msix-store.ps1 (.msixupload).
# Run:  powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix.ps1

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

Write-Warning @"
Building an UNSIGNED MSIX for identity verification only.
For Microsoft Store upload (.msixupload), run build-msix-store.ps1 instead.
"@

$Project = Join-Path $Root "PartFinder\PartFinder.csproj"
dotnet publish $Project `
  -c Release `
  -p:Platform=x64 `
  -p:WindowsPackageType=MSIX `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false `
  -p:AppxBundle=Never `
  -p:UapAppxPackageBuildMode=SideloadOnly `
  -p:PublishReadyToRun=false `
  -p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed (exit $LASTEXITCODE)."
}

$msix = Get-ChildItem (Join-Path $Root "PartFinder\AppPackages") -Recurse -Filter "*.msix" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1
if (-not $msix) { throw "MSIX not found." }

Write-Host "Built (unsigned): $($msix.FullName)"
Write-Host "Validate: powershell -File .\PartFinder\scripts\validate-msix-identity.ps1 -MsixPath `"$($msix.FullName)`""
