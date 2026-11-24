#!/usr/bin/env python3
"""Build tools/sandbox_pr_manifest.json — one small PR per sandbox/CI slice."""

from __future__ import annotations

import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "sandbox_pr_manifest.json")


def read(rel: str) -> str:
    path = os.path.join(ROOT, rel.replace("/", os.sep))
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def single(branch: str, title: str, rel: str) -> dict:
    return {
        "branch": branch,
        "title": title,
        "path": rel.replace("\\", "/"),
        "content": read(rel),
        "body": f"{title}\n\nIsolated sandbox / CI — no AppModule, sln, or portfolio imports.",
        "commit": title,
    }


def multi(branch: str, title: str, paths: list[str]) -> dict:
    return {
        "branch": branch,
        "title": title,
        "paths": [{"path": p.replace("\\", "/"), "content": read(p)} for p in paths],
        "body": f"{title}\n\nIsolated sandbox / CI — no production wiring.",
        "commit": title,
    }


def main() -> None:
    items: list[dict] = []

    items.append(single("sandbox/readme", "sandbox: isolation README", "sandbox/README.md"))
    items.append(
        multi(
            "sandbox/airs-package",
            "sandbox: AIRS radar weather sim package skeleton",
            [
                "sandbox/airs-radar-weather-sim/package.json",
                "sandbox/airs-radar-weather-sim/tsconfig.json",
            ],
        )
    )

    ts_files = [
        "sandbox/airs-radar-weather-sim/src/index.ts",
        "sandbox/airs-radar-weather-sim/src/radar/airs-ingest.ts",
        "sandbox/airs-radar-weather-sim/src/radar/doppler-buffer.ts",
        "sandbox/airs-radar-weather-sim/src/weather/sensor-config.ts",
        "sandbox/airs-radar-weather-sim/src/weather/metar-parser.ts",
        "sandbox/airs-radar-weather-sim/src/fusion/complexity-graph.ts",
        "sandbox/airs-radar-weather-sim/src/fusion/kalman-stub.ts",
    ]
    for i, rel in enumerate(ts_files):
        slug = rel.split("/")[-1].replace(".ts", "")
        items.append(single(f"sandbox/airs-{slug}", f"sandbox: {slug}", rel))

    cobol = [
        "sandbox/cobol-stubs/PF-RADAR.cbl",
        "sandbox/cobol-stubs/PF-WEATHER.cbl",
        "sandbox/cobol-stubs/PF-SENSOR.cbl",
    ]
    for rel in cobol:
        name = os.path.basename(rel).replace(".cbl", "").lower()
        items.append(single(f"sandbox/cobol-{name}", f"sandbox: COBOL stub {name}", rel))

    workflows = [
        ("ci-sandbox-sim.yml", "ci: sandbox AIRS typecheck"),
        ("ci-sandbox-cobol-inventory.yml", "ci: COBOL stub inventory"),
        ("ci-backend-coverage.yml", "ci: backend unit coverage job"),
        ("ci-dotnet-tests.yml", "ci: PartFinder Core tests"),
        ("ci-portfolio-lint.yml", "ci: portfolio eslint"),
    ]
    for fname, title in workflows:
        rel = f".github/workflows/{fname}"
        slug = fname.replace(".yml", "").replace("ci-", "")
        items.append(single(f"ci/{slug}", title, rel))

    items.append(single("docs/analyzer-metrics", "docs: repo analyzer metrics guide", "docs/ANALYZER-METRICS.md"))
    items.append(single("tools/run-analyzer", "tools: run-analyzer-report.ps1", "tools/run-analyzer-report.ps1"))
    items.append(single("tools/batch-open-prs", "tools: batch_open_prs multi-path manifest", "tools/batch_open_prs.py"))
    items.append(
        single(
            "tools/generate-sandbox-manifest",
            "tools: generate_sandbox_pr_manifest.py",
            "tools/generate_sandbox_pr_manifest.py",
        )
    )
    items.append(
        single(
            "sandbox/gitignore-node",
            "sandbox: ignore node_modules in AIRS sim",
            "sandbox/airs-radar-weather-sim/.gitignore",
        )
    )

    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(items, fh, indent=2)
        fh.write("\n")
    print(f"Wrote {len(items)} entries to {OUT}")


if __name__ == "__main__":
    main()
