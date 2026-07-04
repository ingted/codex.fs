# WBS Detail: HOST-004 Host operator landing page / usability gate

WBS ID：`HOST-004`
狀態：Done
Progress：100
StartTime：2026-07-05 00:41 +08:00
UpdatedAt：2026-07-05 01:00 +08:00
Previous：`HOST-003`, `DOC-003`
RFC：`RFC-OPS-0001`
SD：`SD §9`, `SD §10`
Test：`T-HOST-004`

## Scope

修正 host 啟動後 advertised root URL 回 HTTP 404 的產品可用性缺陷。使用者打開 `http://10.28.112.93:10481/` 時應看到可操作入口，而不是只讓工程師知道 `/api/codexfs/host/health`。

## Decision

- `GET /` 是 operator landing page。
- Landing page 必須顯示 `codex.fs host`、running 狀態、advertised URI、health/OpenAPI/Swagger links 與 CLI status command 範例。
- Root page 使用 `HostControlContract` 產生 link，不硬編 localhost。
- Host usability gate 必須包含 browser/Playwright 視角；只驗 health endpoint 不足以宣稱 host 可用。

## Implementation

- Added `HostControl.Routes.Root = "/"` and endpoint definition metadata.
- Added `rootPageHtml` with HTML encoding for values sourced from config/contract.
- `HostControl.mapEndpoints` maps `GET /` before health/session endpoints.
- `tests/codex.fs.Tests` now asserts root HTTP 200 and required page links.

## Verification

- `dotnet build .\codex.fs.slnx --no-restore` passed.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and covered root endpoint assertions.
- Final global-tool host runs at `http://10.28.112.93:10481`.
- Playwright evidence:
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\summary.json`
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\root.png`

## Blockers

- None.

## Follow-up

- Full PTCS Web UI remains `UI-001` follow-up implementation scope. This slice only provides the minimal host operator entry.
