## PartFinder 1.0.0 (x64)

Windows desktop app (WinUI 3) for inventory and parts management.

### Install (sideload)

1. Enable **Developer Mode** in Windows Settings.
2. Build or obtain a signed MSIX (see `PartFinder/build-msix.ps1` and `PartFinder/sideload-install.ps1`).
3. For **Microsoft Store**, submit a Store-signed package with publisher **Euroasiann Group** via Partner Center.

### Build MSIX locally

```powershell
powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix.ps1
```
