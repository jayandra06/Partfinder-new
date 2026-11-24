# PartFinder 3.0.6.0

**Use this build for Microsoft Partner Center** (replaces 3.0.5.1, which fails Store validation because the manifest revision must be `0`).

## Highlights

Same as 3.0.5.1 / recent fixes:

- Master Data: full-width grid, column search filters, per-template data loading
- Templates: carousel layout, horizontal scroll, header tooltips, correct preview data per template
- Link Templates: save/update relations, shared-column defaults
- Backend: relations API returns stable `id`

Plus 3.0.5.0 items: Store invite link, login 2FA, dashboard performance, template canvas, admin invites.

## Microsoft Store upload

| Asset | Use |
|-------|-----|
| **PartFinder_3.0.6.0_x64.msixupload** | Partner Center → PartFinder → Update → **Packages** (preferred) |
| **PartFinder_3.0.6.0_x64.msix** | GitHub / sideload |

Do **not** upload 3.0.5.1 — Partner Center error: *revision number other than zero*.

## Package identity

| Field | Value |
|-------|--------|
| Identity Name | `EuroasiannGroup.partfinder` |
| Publisher | `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD` |
| MSIX version | **3.0.6.0** |
