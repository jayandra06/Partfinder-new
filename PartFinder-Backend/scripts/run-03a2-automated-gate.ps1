# Phase 03A.2 automated engineering gate (no staging credentials required).
# Manual WinUI checklist still mandatory for ACCEPTED verdict.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "=== 03A.2 automated gate ===" -ForegroundColor Cyan

Write-Host "[1/4] Nest build..."
npm run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[2/4] WMS unit/integration tests..."
npm test -- --testPathPatterns=wms
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[3/4] Receiving workflow integration (replSet)..."
npm test -- --testPathPatterns=receiving-workflow.integration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "[4/4] WinUI build..."
$partfinderProj = Resolve-Path (Join-Path $root "..\PartFinder\PartFinder\PartFinder.csproj")
dotnet build $partfinderProj -c Debug -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Automated gate PASSED." -ForegroundColor Green
Write-Host "Next: staging deploy per docs/wms-phase03/03A-2-staging-deployment-package.md"
Write-Host "      manual WinUI: docs/wms-phase03/03A-2-winui-acceptance-checklist.md"