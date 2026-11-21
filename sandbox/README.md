# PartFinder sandbox (non-production)

This tree is **deliberately isolated** from shipping products:

| Product | Loads sandbox? |
|---------|----------------|
| PartFinder Windows app (`PartFinder.sln`) | **No** — not in solution |
| PartFinder-Backend (`AppModule`) | **No** — not imported |
| Partfinder-Portfolio | **No** |

Used for **R&D stubs** (AIRS/radar ingest simulation, weather sensor config, legacy COBOL samples) and **extra CI** jobs. Safe to push to git; production builds and deploys ignore this folder.

Do **not** add `import` / `ProjectReference` from production code into `sandbox/`.
