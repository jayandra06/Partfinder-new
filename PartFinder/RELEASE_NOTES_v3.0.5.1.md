# PartFinder 3.0.5.1

Store submission build including fixes after 3.0.5.0.

## Highlights

- **Master Data:** Full-width data grid; column header search boxes (filter without losing cursor); template-specific row loading (no cross-template bleed).
- **Templates:** All-templates carousel uses full card width, horizontal scroll when needed, header tooltips; preview data bound to the correct template.
- **Link Templates:** Save/update relations fixed (`id` in API responses, PATCH body without immutable template ids); default match columns are **shared names only** on both templates.
- **Backend:** Relations list/detail return a stable `id` field for updates.

Includes everything from **3.0.5.0** (Store invite link, login 2FA, dashboard performance, template canvas, admin invites).

## Microsoft Store upload

| Asset | Use |
|-------|-----|
| **PartFinder_3.0.5.1_x64.msixupload** | Partner Center → PartFinder → Update → Packages |
| **PartFinder_3.0.5.1_x64.msix** | GitHub release / sideload testing |

Use **3.0.5.1** (not 3.0.5.0) if 3.0.5.0 was already uploaded to Partner Center.

## Package identity

| Field | Value |
|-------|--------|
| Identity Name | `EuroasiannGroup.partfinder` |
| Publisher | `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD` |
| MSIX version | **3.0.5.1** |
