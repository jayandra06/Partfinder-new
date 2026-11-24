# Validates MSIX package identity against Microsoft Store requirements for PartFinder.
param(
  [Parameter(Mandatory = $true)]
  [string]$MsixPath
)

$ErrorActionPreference = "Stop"

$expected = @{
  Name      = "EuroasiannGroup.partfinder"
  Publisher = "CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD"
  Family    = "EuroasiannGroup.partfinder_8gdvw9rseb738"
  Arch      = "x64"
}

if (-not (Test-Path $MsixPath)) { throw "File not found: $MsixPath" }

$temp = Join-Path $env:TEMP ("pf-msix-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
  $zipPath = Join-Path $temp "package.zip"
  Copy-Item $MsixPath $zipPath
  Expand-Archive -Path $zipPath -DestinationPath (Join-Path $temp "pkg") -Force
  $manifestPath = Join-Path $temp "pkg\AppxManifest.xml"
  if (-not (Test-Path $manifestPath)) { throw "AppxManifest.xml not found inside MSIX." }

  [xml]$doc = Get-Content $manifestPath
  $identity = $doc.Package.Identity
  if (-not $identity) { $identity = $doc.DocumentElement.Identity }

  $actualName = $identity.GetAttribute("Name")
  $actualPublisher = $identity.GetAttribute("Publisher")
  $actualVersion = $identity.GetAttribute("Version")
  $actualArch = $identity.GetAttribute("ProcessorArchitecture")

  if (-not ("PfnNative" -as [type])) {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class PfnNative {
  [StructLayout(LayoutKind.Sequential)]
  public struct PACKAGE_VERSION {
    public ushort Revision;
    public ushort Build;
    public ushort Minor;
    public ushort Major;
  }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  public struct PACKAGE_ID {
    public uint reserved;
    public uint processorArchitecture;
    public PACKAGE_VERSION version;
    public string name;
    public string publisher;
    public string resourceId;
    public string publisherId;
  }
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
  public static extern int PackageFamilyNameFromId(ref PACKAGE_ID packageId, ref uint packageFamilyNameLength, StringBuilder packageFamilyName);
  public static string Get(string name, string publisher) {
    var id = new PACKAGE_ID();
    id.name = name;
    id.publisher = publisher;
    uint len = 0;
    PackageFamilyNameFromId(ref id, ref len, null);
    var sb = new StringBuilder((int)len);
    int hr = PackageFamilyNameFromId(ref id, ref len, sb);
    if (hr != 0) throw new System.InvalidOperationException("PackageFamilyNameFromId failed: " + hr);
    return sb.ToString();
  }
}
"@
  }
  $pfn = [PfnNative]::Get($actualName, $actualPublisher)

  Write-Host ""
  Write-Host "MSIX identity validation"
  Write-Host "------------------------"
  foreach ($key in @("Name", "Publisher", "Arch")) {
    $prop = switch ($key) {
      "Name" { @{ a = $actualName; e = $expected.Name } }
      "Publisher" { @{ a = $actualPublisher; e = $expected.Publisher } }
      "Arch" { @{ a = $actualArch; e = $expected.Arch } }
    }
    $ok = ($prop.a -eq $prop.e)
    $mark = if ($ok) { "OK" } else { "MISMATCH" }
    Write-Host ("{0,-12} expected: {1}" -f $key, $prop.e)
    Write-Host ("{0,-12} actual:   {1}  [{2}]" -f "", $prop.a, $mark)
  }
  Write-Host ("Version      actual:   {0}" -f $actualVersion)
  $storeVersionInvalid = $false
  if ($actualVersion -match '^(\d+)\.(\d+)\.(\d+)\.(\d+)$' -and [int]$Matches[4] -ne 0) {
    Write-Host "Version      Store:    revision (4th segment) must be 0  [INVALID FOR STORE]"
    $storeVersionInvalid = $true
  }
  $fOk = ($pfn -eq $expected.Family)
  Write-Host ("Family (PFN) expected: {0}" -f $expected.Family)
  Write-Host ("Family (PFN) actual:   {0}  [{1}]" -f $pfn, $(if ($fOk) { "OK" } else { "MISMATCH" }))

  $failed = $storeVersionInvalid -or ($actualName -ne $expected.Name) -or ($actualPublisher -ne $expected.Publisher) -or ($actualArch -ne $expected.Arch) -or (-not $fOk)
  if ($failed) { exit 1 }
}
finally {
  Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
