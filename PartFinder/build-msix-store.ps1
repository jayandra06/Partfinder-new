# Microsoft Store upload package (.msixupload) for PartFinder.
# Uses GenerateTemporaryStoreCertificate (Partner Center re-signs after upload).
# The signing certificate subject must equal the reserved publisher. A different
# subject is written into Identity Publisher and Partner Center rejects the package.
# No Partner Center .pfx required for this workflow.
#
# Run: powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix-store.ps1

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Project = Join-Path $Root "PartFinder\PartFinder.csproj"
$ManifestPath = Join-Path $Root "PartFinder\Package.appxmanifest"
$ExpectedName = "EuroasiannGroup.partfinder"
$ExpectedPublisher = "CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD"

[xml]$manifestDoc = Get-Content -Path $ManifestPath
$sourceIdentity = $manifestDoc.Package.Identity
if (-not $sourceIdentity) { $sourceIdentity = $manifestDoc.DocumentElement.Identity }
$sourceName = $sourceIdentity.GetAttribute("Name")
$sourcePublisher = $sourceIdentity.GetAttribute("Publisher")
if ($sourceName -ne $ExpectedName -or $sourcePublisher -ne $ExpectedPublisher) {
  throw "Package.appxmanifest identity is '$sourceName' / '$sourcePublisher'. Store requires '$ExpectedName' / '$ExpectedPublisher'."
}

$started = Get-Date
Write-Host "Publishing Store upload package (Release, x64, StoreOnly + temporary Store cert)..."
dotnet publish $Project `
  -c Release `
  -p:Platform=x64 `
  -p:PublishProfile=Properties\PublishProfiles\win-x64-Store.pubxml `
  -p:GenerateAppxPackageOnBuild=true `
  -p:UapAppxPackageBuildMode=StoreOnly `
  -p:GenerateTemporaryStoreCertificate=true `
  -p:AppxPackageSigningEnabled=true `
  -p:AppxBundle=Never `
  -p:PublishReadyToRun=false `
  -p:PublishTrimmed=false `
  -p:AppxSymbolPackageEnabled=false

if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed (exit $LASTEXITCODE)."
}

$upload = Get-ChildItem (Join-Path $Root "PartFinder\AppPackages") -Recurse -Include "*.msixupload","*.appxupload" |
  Where-Object { $_.LastWriteTime -ge $started.AddSeconds(-5) } |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

$msix = Get-ChildItem (Join-Path $Root "PartFinder\AppPackages") -Recurse -Filter "*.msix" |
  Where-Object { $_.LastWriteTime -ge $started.AddSeconds(-5) } |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

Write-Host ""
if ($upload) {
  Write-Host "Store upload artifact: $($upload.FullName)"
} else {
  Write-Warning "No .msixupload/.appxupload found. Check AppPackages output."
}
if (-not $msix) { throw "No MSIX produced. Store upload was not created." }
Write-Host "Inner MSIX: $($msix.FullName)"
& (Join-Path $Root "scripts\validate-msix-identity.ps1") -MsixPath $msix.FullName
if ($LASTEXITCODE -ne 0) { throw "Built package identity does not match the Microsoft Store reservation." }
