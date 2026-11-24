# PartFinder 3.0.5.0

## Highlights

- Invite emails use the [Microsoft Store download link](https://apps.microsoft.com/detail/9n6xvkpx189w?hl=en-GB&gl=IN) (legacy shipspan URLs remapped on server and client).
- Login two-factor authentication (TOTP) gate before opening the app when 2FA is enabled on the device.
- Faster home/dashboard load (parallel API calls, dashboard warm-up during shell init).
- Template canvas no longer overlaps the all-templates view when creating a template.
- Admin invites receive full permissions and all-template access automatically.
- Stable invite-user dialog (ContentDialog / WinUI COM fixes).

## Microsoft Store upload

| Asset | Use |
|-------|-----|
| **PartFinder_3.0.5.0_x64.msixupload** | Partner Center → PartFinder → Update → Packages |
| **PartFinder_3.0.5.0_x64.msix** | GitHub release / sideload testing |

## Package identity

| Field | Value |
|-------|--------|
| Identity Name | `EuroasiannGroup.partfinder` |
| Publisher | `CN=B96C9A11-7C90-42E9-8C6C-14AD9097B6CD` |
| MSIX version | **3.0.5.0** |
