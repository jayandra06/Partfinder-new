#!/usr/bin/env python3
"""Open many small, single-file PRs from a manifest (tests + docs)."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import textwrap

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_MANIFEST = os.path.join(ROOT, "tools", "batch_pr_manifest.json")
GIT_ID = ["-c", "user.email=jayandra06@users.noreply.github.com", "-c", "user.name=jayandra06"]


def write_entry_files(entry: dict) -> list[str]:
    """Return list of repo-relative paths written for this manifest entry."""
    rel_paths: list[str] = []
    if "paths" in entry:
        for item in entry["paths"]:
            rel = item["path"].replace("\\", "/")
            rel_paths.append(rel)
            abs_path = os.path.join(ROOT, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(abs_path), exist_ok=True)
            content = item["content"]
            with open(abs_path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(content if content.endswith("\n") else content + "\n")
    else:
        rel = entry["path"].replace("\\", "/")
        rel_paths.append(rel)
        abs_path = os.path.join(ROOT, rel.replace("/", os.sep))
        parent = os.path.dirname(abs_path)
        if parent:
            os.makedirs(parent, exist_ok=True)
        content = entry["content"]
        with open(abs_path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(content if content.endswith("\n") else content + "\n")
    return rel_paths


def run(cmd: list[str], check: bool = True) -> subprocess.CompletedProcess:
    return subprocess.run(
        cmd,
        cwd=ROOT,
        text=True,
        capture_output=True,
        check=check,
    )


def main() -> int:
    manifest = DEFAULT_MANIFEST
    if len(sys.argv) > 1:
        manifest = sys.argv[1]
        if not os.path.isabs(manifest):
            manifest = os.path.join(ROOT, manifest)

    if not os.path.isfile(manifest):
        print(f"Missing {manifest}", file=sys.stderr)
        return 1

    with open(manifest, encoding="utf-8") as fh:
        items = json.load(fh)

    opened: list[str] = []
    skipped: list[str] = []
    failed: list[str] = []

    for entry in items:
        branch = entry["branch"]
        title = entry["title"]
        body = entry.get("body", title)
        run(["git", "fetch", "origin", "main"], check=False)
        if run(["git", "checkout", "main"]).returncode != 0:
            failed.append(f"{branch}: checkout main")
            continue
        if run(["git", "pull", "--ff-only", "origin", "main"], check=False).returncode != 0:
            failed.append(f"{branch}: pull main")
            continue

        if run(["git", "checkout", "-b", branch], check=False).returncode != 0:
            run(["git", "checkout", branch], check=False)
            if run(["git", "merge", "origin/main"], check=False).returncode != 0:
                skipped.append(f"{branch}: branch stuck")
                continue

        rel_paths = write_entry_files(entry)

        run(["git", "add", *rel_paths])
        msg = entry.get("commit", title)
        if run(["git", *GIT_ID, "commit", "-m", msg], check=False).returncode != 0:
            run(["git", "checkout", "main"], check=False)
            skipped.append(f"{branch}: nothing to commit")
            continue

        push = run(["git", "push", "-u", "origin", branch], check=False)
        if push.returncode != 0:
            failed.append(f"{branch}: push — {push.stderr.strip()[:120]}")
            run(["git", "checkout", "main"], check=False)
            continue

        pr = subprocess.run(
            [
                "gh",
                "pr",
                "create",
                "--repo",
                "jayandra06/Partfinder-new",
                "--head",
                branch,
                "--title",
                title,
                "--body",
                body,
            ],
            cwd=ROOT,
            text=True,
            capture_output=True,
        )
        if pr.returncode != 0:
            if "already exists" in (pr.stderr + pr.stdout).lower():
                skipped.append(f"{branch}: pr exists")
            else:
                failed.append(f"{branch}: pr — {pr.stderr.strip()[:120]}")
        else:
            url = pr.stdout.strip().splitlines()[-1] if pr.stdout else branch
            opened.append(url)

        run(["git", "checkout", "main"], check=False)

    print(f"Opened {len(opened)} PRs")
    for u in opened[:5]:
        print(f"  {u}")
    if len(opened) > 5:
        print(f"  ... and {len(opened) - 5} more")
    print(f"Skipped {len(skipped)}, failed {len(failed)}")
    if failed:
        for x in failed[:10]:
            print(f"  FAIL: {x}")
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
