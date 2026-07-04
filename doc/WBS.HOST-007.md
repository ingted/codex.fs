# WBS Detail: HOST-007 PTCS Hub Chat Alignment And Diagnostics Split

WBS ID：`HOST-007`
狀態：Done
Progress：100
Previous：`HOST-006`, `UI-002`
Test：`T-HOST-007`
Test case：`TC-HOST-007 /chat guard + diagnostics send + OpenAPI paths`
SD：`SD §9`, `SD §14.1`
RFC：`RFC-HOST-0002`
動工時間：2026-07-05 03:03 +08:00
更新時間：2026-07-05 03:10 +08:00

## Scope

修正 alpha.5 standalone `/chat` 方向：

- `GET /chat` 改為 guard page，明確說 browser chat 應使用 PTCS WebSharper chat room。
- standalone prompt testing 移到 `GET/POST /diagnostics/session-send`。
- diagnostics `sessionId` 可空白，空白代表 `foreman`。
- OpenAPI exposes `/chat`, `/diagnostics/session-send`, and `/api/codexfs/foreman/messages`。
- health JSON uses `diagnosticsSessionSendUri` instead of `chatUri`。

## Boundary

產品 chat room 由 PTCS WebSharper 提供。codex.fs workers/session-workers 必須透過 caller-owned `CommSpaMessageFabric` / `CommSpaActorFabric` 成為 PTCS participants，不能用 standalone package-owned host 建立平行 UI truth。

## Evidence

- `dotnet build .\codex.fs.slnx --no-restore`: passed, 0 warnings, 0 errors。
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`: passed。
- Tests assert `/chat` guard text, `/diagnostics/session-send` form and post, `diagnosticsSessionSendUri`, and OpenAPI paths for legacy guard, diagnostics, and foreman route。
- Installed alpha.6 host on `http://10.28.112.93:10481` verified root/chat guard/diagnostics/health/OpenAPI/Swagger HTTP 200。
- Playwright/browser evidence: `G:\codex.fs\.codex.fs\ptcs-hub-align-20260705030317-alpha6\summary.json`, with screenshots `root.png`, `chat-guard.png`, `diagnostics.png`, `diagnostics-mobile.png`, and `swagger.png`。

## Blocker

None.
