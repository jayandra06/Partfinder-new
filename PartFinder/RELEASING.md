# PartFinder releases (automated)

Repo: [jayandra06/Partfinder-new](https://github.com/jayandra06/Partfinder-new)

## Fix: "Actions is not permitted to create or approve pull requests"

Release Please needs one of these:

1. **Repo setting (recommended)**  
   **Settings → Actions → General → Workflow permissions**  
   - Select **Read and write permissions**  
   - Enable **Allow GitHub Actions to create and approve pull requests**  
   (Org owners may need to allow this for the organization first.)

2. **Personal access token (optional)**  
   Create a classic PAT with `repo` scope, add repository secret **`RELEASE_PLEASE_TOKEN`**, re-run **Release Please**.

If you cannot enable PR creation, use **Release (direct)** below (no PR).

## Release (direct) — works without PR permission

**Actions → Release (direct) → Run workflow** → choose `patch` / `minor` / `major`.

This will:

1. Bump `.github/.release-please-manifest.json`, `version.json`, `CHANGELOG.md`, and `Package.appxmanifest`
2. Commit and push to `main`
3. Create tag `vX.Y.Z` and a **GitHub Release**
4. Trigger **Build MSIX and attach to Release** (uploads `PartFinder_X.Y.Z_x64.msix`)

## Release Please (optional, manual only)

This workflow **does not run on every push** (it failed when Actions could not open PRs).

Run **Actions → Release Please** manually only after enabling PR permission (see top of this doc).

1. Use conventional commits on `main`: `feat:`, `fix:`, `feat!:` / `BREAKING CHANGE:`
2. Release Please opens a PR *chore(main): release x.y.z*
3. Merge the PR → tag + release → MSIX workflow runs

**“commit could not be parsed”** in logs is **not a failure** — Release Please only understands conventional commit titles. Old messages like `Merge pull request #2` or `Fix admin portal…` are skipped on purpose; a few dozen warnings are normal for this repo’s history.

## Version files

| File | Purpose |
|------|---------|
| `.github/.release-please-manifest.json` | Semver source of truth |
| `version.json` | App version (synced on bump / CI) |
| `PartFinder/PartFinder/Package.appxmanifest` | MSIX four-part version |
| `CHANGELOG.md` | Release notes |

## Microsoft Store MSIX version (Partner Center)

Each upload needs a **unique** four-part manifest version. After a duplicate-version error, set a new line explicitly, then bump revision for each GitHub release:

```powershell
powershell -File .\PartFinder\scripts\bump-store-msix-version.ps1 -Set 3.0.3.0
powershell -File .\PartFinder\scripts\bump-store-msix-version.ps1 -BumpRevision   # 3.0.3.1, … 3.0.3.9, 3.0.4.0
powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix-store.ps1
```

Tag releases with the MSIX quad (e.g. `v3.0.3.0`) and attach `PartFinder_<quad>_x64.msixupload`.

## Manual MSIX only

**Actions → Build MSIX and attach to Release** → enter an existing semver (e.g. `1.0.1`).

## Local build

```powershell
powershell -ExecutionPolicy Bypass -File .\PartFinder\build-msix.ps1
```

## Microsoft Store

Use Partner Center with publisher **Euroasiann Group**. The package identity must stay `EuroasiannGroup.partfinder` / `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD`. The GitHub MSIX is signed with a certificate whose subject is that same publisher; a different subject (such as `CN=DELL`) is copied into the package and Partner Center rejects it. Prefer `PartFinder/build-msix-store.ps1` for the `.msixupload` you submit.
