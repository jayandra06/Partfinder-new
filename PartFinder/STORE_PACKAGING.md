# Microsoft Store MSIX (PartFinder)

## Store identity (source of truth)

File: `PartFinder/Package.appxmanifest`

| Field | Value |
|-------|--------|
| Identity Name | `EuroasiannGroup.partfinder` |
| Publisher | `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD` |
| Publisher display | `Euroasiann Group` |
| Package family (derived) | `EuroasiannGroup.partfinder_8gdvw9rseb738` |
| Architecture | x64 |
| Version (current submission) | `3.0.3.0` (see MSIX version scheme below) |

### MSIX version scheme (Partner Center)

Each **new upload** must have a unique four-part `Identity Version`. Use:

```powershell
# First package on a line: set explicitly (e.g. after a duplicate-version error)
powershell -File .\PartFinder\scripts\bump-store-msix-version.ps1 -Set 3.0.3.0
# Each subsequent GitHub Store release: bump revision
powershell -File .\PartFinder\scripts\bump-store-msix-version.ps1 -BumpRevision
```

Sequence: `3.0.3.0` → `3.0.3.1` → … → `3.0.3.9` → `3.0.4.0`. Rebuild with `build-msix-store.ps1` after every bump.

## Store upload package (preferred)

Partner Center expects a **`.msixupload`** (or legacy `.appxupload`) bundle for WinUI/UWP-style submissions. The upload contains a **disposable-locally-signed** MSIX; Microsoft re-signs with your reserved Store identity after upload. A Partner Center `.pfx` is **not** required for this path.

The signing certificate **subject must be** `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD`. The packager copies a different subject into `Identity Publisher`. That is why a package signed as `CN=DELL` fails with publisher `CN=DELL` and family name `…_hqhyg1cvr7f3y`. Do not upload `PartFinder_1.0.0.0_x64.msix` from the old manifest (`608d9293-…` / `CN=DELL`).

```powershell
powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix-store.ps1
```

**Output:** `PartFinder\PartFinder\AppPackages\PartFinder_<version>_x64.msixupload`

Publish profile: `Properties/PublishProfiles/win-x64-Store.pubxml` (`UapAppxPackageBuildMode=StoreOnly`, `GenerateTemporaryStoreCertificate=true`).

### Headless / `dotnet publish` notes

- **Windows App SDK** MSIX tasks on **.NET SDK (Core MSBuild)** need `System.Security.Permissions` beside `Microsoft.Windows.SDK.BuildTools.MSIX.dll` (see `PartFinder/Directory.Build.props` + staging target).
- **`GenerateAppxPackageOnBuild=true`** skips `_ValidateSigningCertificate`; the project hooks `_GenerateAppxPackageFileDependsOn` to run disposable Store cert generation (see `PartFinder/PartFinder/Directory.Build.targets`).
- **`Package.StoreAssociation.xml`** must match the existing Store app identity (checked in for CI; refresh via **Associate with the Store** in Visual Studio if Partner Center rejects association).
- **Symbol packages** need `mspdbcmf.exe` (Visual Studio *MSIX Packaging* / AppxPackage tools). Store builds set `AppxSymbolPackageEnabled=false` when only the .NET SDK is installed.

## Unsigned sideload (identity check only)

```powershell
powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix.ps1
```

Produces an unsigned `_Test` MSIX for local manifest checks, not for Store upload.

## Validate identity

```powershell
powershell -File .\PartFinder\scripts\validate-msix-identity.ps1 -MsixPath "path\to\PartFinder_3.0.3.0_x64.msix"
```

## Visual Studio (alternative)

1. Open the solution in Visual Studio 2022 with **Windows application development** workload.
2. **Project → Publish → Associate with the Store** (existing PartFinder listing) to refresh `Package.StoreAssociation.xml` if needed.
3. **Publish → Create App Packages** → Microsoft Store → **StoreOnly** → x64 Release.
4. Upload the generated `.msixupload` from `AppPackages`.

## Partner Center upload

1. [Partner Center](https://partner.microsoft.com/dashboard) → **PartFinder** (existing app).
2. **Update** submission (not a new product).
3. **Packages** → upload `PartFinder_3.0.3.0_x64.msixupload` (unique four-part version; bump with `bump-store-msix-version.ps1` before each upload).
4. Complete certification sections and submit.

Leave existing **1.0.0** packages on disk unchanged; bump `Package.appxmanifest` / `version.json` only when Partner Center requires a higher version.
