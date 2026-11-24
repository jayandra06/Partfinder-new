# Repo analyzer exports (Excel / CSV)

| File | Purpose |
|------|---------|
| `report.csv` | Spreadsheet row from Jay's `repo_analyzer_UPDATED.py` (includes **COCOMO** when scc is installed) |
| `report_detail.json` | Full analyzer JSON detail |

Regenerate:

```powershell
.\tools\install-scc.ps1   # once
.\tools\run-analyzer-report.ps1
```

Commit updated `report.csv` after regenerating on `main` so metrics stay in sync with the repo.