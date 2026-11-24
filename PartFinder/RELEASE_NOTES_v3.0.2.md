# PartFinder 3.0.2 — Microsoft Store submission

## Download for Partner Center

| Asset | Use |
|-------|-----|
| **PartFinder_3.0.2.0_x64.msixupload** | Upload this file in [Partner Center](https://partner.microsoft.com/dashboard) → **PartFinder** → **Update** submission → **Packages**. |
| PartFinder_3.0.2.0_x64.msix | Optional local sideload/test layout package (disposable cert). Not the preferred Store upload format. |

Partner Center re-signs the upload package with your reserved Store identity after upload. A Partner Center `.pfx` is not required for this workflow.

## Package identity (must match existing Store app)

| Field | Value |
|-------|--------|
| Identity Name | `EuroasiannGroup.partfinder` |
| Publisher | `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD` |
| Publisher display | `Euroasiann Group` |
| Package family (PFN) | `EuroasiannGroup.partfinder_8gdvw9rseb738` |
| Architecture | x64 |
| MSIX version | `3.0.2.0` |
| App semver | `3.0.2` |

## Partner Center steps

1. Open **Apps and games** → **PartFinder** (existing listing — **Update**, not a new app).
2. Start or open a submission with version **3.0.2.0** (must be higher than the last published package).
3. **Packages** → upload **PartFinder_3.0.2.0_x64.msixupload**.
4. Complete remaining certification sections and submit.

## Validate identity locally

```powershell
powershell -File .\PartFinder\scripts\validate-msix-identity.ps1 -MsixPath ".\PartFinder_3.0.2.0_x64.msix"
```

## Build reproduction

Built with `PartFinder/build-msix-store.ps1` (`UapAppxPackageBuildMode=StoreOnly`, `GenerateTemporaryStoreCertificate=true`). See `PartFinder/STORE_PACKAGING.md`.
