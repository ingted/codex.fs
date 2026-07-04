# WBS Detail: REL-004 Global tool install and host handoff

WBS ID：`REL-004`
狀態：Done
Progress：100
StartTime：2026-07-05 00:41 +08:00
UpdatedAt：2026-07-05 01:00 +08:00
Previous：`REL-003`, `HOST-004`, `DOC-004`
RFC：`RFC-OPS-0001`
SD：`SD §2`, `SD §9`, `SD §10`
Test：`T-REL-004`

## Scope

修正前一輪把 `dotnet run --project` 的開發驗證誤當成使用者可用 dotnet tool 的問題。使用者預期在 `C:\Users\Administrator\.dotnet\tools` 找到可執行 tool，因此 release handoff 必須驗證 global tool path。

## Decision

- User-facing handoff uses global dotnet tools unless explicitly documented otherwise.
- Long-running host handoff must not use `dotnet run` over dev build output because it can lock `bin/Debug` assemblies and break subsequent build/test.
- Package rebuild after a handoff defect must bump prerelease version. This slice uses `0.1.0-alpha.2`.
- Handoff evidence must include the exact URL, process ownership, command help, root/docs/OpenAPI HTTP checks and browser/Playwright checks.

## Implementation

- Bumped package versions from `0.1.0-alpha.1` to `0.1.0-alpha.2` across all package projects.
- Packed `codex.fs`, `codex.fs.ptcs`, `codex.fs.host`, `codex.fs.cli`, and `codex.fs.host.tool` to a local alpha.2 pack directory.
- Installed global tools:
  - `codex.fs.cli`
  - `codex.fs.host.tool` with command name `codex.fs.host`
- Started host with LAN bind/advertise settings and docs enabled.

## Verification

- `dotnet tool list --global` shows `codex.fs.cli` and `codex.fs.host.tool` at `0.1.0-alpha.2`.
- `C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe --help` returned exit code 0.
- `C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe --help` returned exit code 0.
- Host URL checks returned HTTP 200:
  - `http://10.28.112.93:10481/`
  - `http://10.28.112.93:10481/api/codexfs/host/health`
  - `http://10.28.112.93:10481/openapi/v1.json`
  - `http://10.28.112.93:10481/docs/index.html`
- Playwright/browser evidence:
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\summary.json`
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\root.png`
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\docs.png`

## Superseded Command Note

`CLI-005` superseded the alpha.2 CLI command name. `CLI-006` supersedes alpha.3 again: current alpha.4 contract installs canonical command `codex.fs.cli` from package `codex.fs.cli` and short alias `codex.fs` from package `codex.fs.tool`; use `doc/WBS.CLI-006.md` for current tool command evidence.

## Blockers

- None.

## Follow-up

- NuGet publish remains a separate release decision. This slice proves local package/tool handoff from generated nupkg.
