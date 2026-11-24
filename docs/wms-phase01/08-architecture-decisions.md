# 08 — Architecture decisions (index)

**Not authoritative.** The **single decision register** is **`06-wms-risks-and-questions.md`** (D01–D28, P01–P04, blockers B1–B7, final readiness checklist).

Use this page only to navigate Phase 01 artifacts.

| Document | Purpose |
|----------|---------|
| `01-existing-system-audit.md` | As-built WinUI, NestJS, tenant Mongo |
| `02-wms-gap-analysis.md` | 19-module E/M/D matrix |
| `03-wms-architecture.md` | Approved architecture, quantity model, pack/dispatch gates |
| `04-wms-database-proposal.md` | Additive `wms_*` schema, invariants |
| `05-wms-development-roadmap.md` | Phases 02A–07, AT mapping |
| `06-wms-risks-and-questions.md` | **Decision register**, risks, blockers, readiness |
| `07-blueprint-traceability.md` | Blueprint → phase → tests |

**External attachment:** `PartFinder_WMS_Phase01_Architecture_Decisions.md` — historical PO input; if text conflicts with **`06`**, **`06` wins**.

**Implementation:** Do not start stock-changing WMS until `06` readiness checklist rows 3, 8, 9 are verified (B1, B7, B6).