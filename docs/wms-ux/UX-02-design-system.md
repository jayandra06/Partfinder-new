# UX-02 — PartFinder Warehouse Design System (Proposal)

**Status:** Design specification for product-owner review — **not applied** to production XAML in UX-01.  
**Relationship to PartFinder:** Extends existing `Colors.xaml` / `Typography.xaml` / `Spacing.xaml`; does not replace retail styling globally.

---

## 1. Visual direction

| Principle | Specification |
|-----------|----------------|
| Positioning | Premium **enterprise WMS** — calm, precise, trustworthy |
| Shell (WMS mode) | **Deep navy** sidebar `#0B1F3A` (light: `#0F2744`) |
| Workspace | **White / cool gray** canvas `#F4F6F9` (dark: `#0E1520` card on `#070C14`) |
| Primary accent | **Teal** `#0D9488` (hover `#14B8A6`, pressed `#0F766E`) — distinct from retail blue |
| Secondary accent | Retain PartFinder blue for **cross-app** links only |
| Status | Restrained semantic palette (see §6) |
| Typography | **Segoe UI Variable** / system default; tabular nums for quantities |
| Density | **Comfortable** default; **Compact** −2px row height, −4px padding (user setting) |
| Iconography | **Segoe Fluent Icons** (existing); outline for nav, filled for selected |
| Elevation | Subtle 1px borders + soft shadow on modals only (avoid heavy glass in data-dense areas) |

---

## 2. Design tokens (WMS extension)

Add as `Styles/WmsTokens.xaml` (future implementation). Names align with existing `*Color` → `*Brush` pattern.

### 2.1 Color

| Token | Light | Dark | Usage |
|-------|-------|------|--------|
| `WmsSidebarBackground` | `#0F2744` | `#0B1F3A` | WMS nav rail |
| `WmsSidebarText` | `#E8EEF4` | `#E8EEF4` | Nav labels |
| `WmsSidebarTextMuted` | `#9FB3C8` | `#8FA3B8` | Group headers |
| `WmsWorkspaceBackground` | `#F4F6F9` | `#070C14` | Main content |
| `WmsCardBackground` | `#FFFFFF` | `#141D2A` | Worksheet card |
| `WmsAccentPrimary` | `#0D9488` | `#14B8A6` | Primary buttons, selection |
| `WmsAccentSubtle` | `#CCFBF1` | `#134E4A` | Selected row wash |
| `WmsBorderSubtle` | `#D1D9E6` | `#3D4F66` | Grid lines |
| `WmsFocusRing` | `#0D9488` | `#5EEAD4` | Focus (2px) |

Map existing semantic tokens for parity:

| Semantic | Light | Dark |
|----------|-------|------|
| Success | `#059669` | `#34D399` |
| Warning | `#D97706` | `#FBBF24` |
| Danger | `#DC2626` | `#F87171` |
| Info | `#0284C7` | `#38BDF8` |
| Blocked stock | `#7C3AED` | `#A78BFA` |

### 2.2 Typography

| Token | Size | Weight | Use |
|-------|------|--------|-----|
| `WmsFontPageTitle` | 24px | SemiBold | Worksheet title |
| `WmsFontSection` | 18px | SemiBold | Card headers |
| `WmsFontBody` | 14px | Normal | Cells |
| `WmsFontCaption` | 12px | Normal | Hints, units |
| `WmsFontColumnHeader` | 12px | SemiBold | Grid header |
| `WmsFontMono` | 13px | Normal | IDs, txn keys (Cascadia Mono) |

**Quantity columns:** `Typography.NumeralAlignment = Tabular`.

### 2.3 Spacing and radius

Reuse `Spacing.xaml` keys; WMS-specific:

| Token | Value |
|-------|-------|
| `WmsNavWidth` | 280px (comfortable) / 248px (compact) |
| `WmsContextBarHeight` | 56px |
| `WmsGridRowHeight` | 36px / 32px compact |
| `WmsGridHeaderHeight` | 40px |
| `WmsRadiusCard` | 12px (tighter than retail 16px for density) |
| `WmsRadiusButton` | 8px |

### 2.4 Status chips

| State | Background | Text | Icon |
|-------|------------|------|------|
| Open / pending | `#FEF3C7` | `#92400E` | Clock |
| Posted / immutable | `#E5E7EB` | `#374151` | Lock |
| Blocked / QC hold | `#EDE9FE` | `#5B21B6` | Shield |
| Available | `#D1FAE5` | `#065F46` | Check |
| Error | `#FEE2E2` | `#991B1B` | Error |

---

## 3. Core components (spec)

| Component | Purpose |
|-----------|---------|
| `WmsContextBar` | Warehouse, owner, site time, operator, gate badge |
| `WmsWorksheetChrome` | Title, search, filters, export, density toggle |
| `WmsStepper` | Receiving/QC/put-away guided flow |
| `WmsDataGrid` | Shared grid (see UX-04) |
| `WmsTaskCard` | My open put-away / QC count |
| `WmsConfirmDialog` | Irreversible stock actions (2-step) |
| `WmsPrototypeBanner` | “Design prototype — Phase 03B+” |

### Buttons

| Type | Style |
|------|--------|
| Primary | Teal fill, white text |
| Secondary | Outlined teal |
| Destructive | Danger fill — disposition / write-off only |
| Ghost | Nav and toolbar icon |

---

## 4. Light and dark themes

- WMS tokens defined in **both** `ResourceDictionary` theme dictionaries (mirror `Colors.xaml` structure).
- `ThemeService` continues to flip `ElementTheme`; WMS brushes re-resolve automatically.
- **Contrast:** Primary teal on white ≥ **4.5:1** for body; large buttons ≥ **3:1**.

---

## 5. Data density modes

| Mode | Row height | Font | Nav width |
|------|------------|------|-----------|
| Comfortable | 36px | 14px | 280px |
| Compact | 32px | 13px | 248px |

Stored in user settings: `wms.dataDensity` (local JSON alongside `ui-theme.json`).

---

## 6. Icon and illustration

- Use existing `FontIcon` glyphs; add WMS-specific nav set (Receipt, QC, PutAway, Pick, Pack, Dispatch).
- Empty states: simple line illustrations (SVG in `Assets/Wms/`), not stock photography.

---

## 7. Migration strategy (design-only)

1. **Do not** replace retail accent globally.
2. Scope new brushes to `WmsWorkbookPage` + future `WmsShell` user control.
3. Phase UX-02 implementation behind feature flag `wms.premiumChrome` (default off until PO sign-off).

---

## 8. Figma / handoff (optional)

If design team uses Figma: export token JSON matching §2 for `WmsTokens.xaml` generation script (future).

---

*Review with product owner before engineering rollout.*