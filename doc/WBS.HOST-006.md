# WBS Detail: HOST-006 Standalone Host `/chat` PoC Form

WBS ID：`HOST-006`
狀態：Done; superseded by `HOST-007`
Progress：100
Previous：`HOST-004`, `CLI-007`
Test：`T-HOST-006`
Test case：`TC-HOST-006 /chat form send through MessageFabric`
SD：`SD §9`, `SD §10`
RFC：`RFC-HOST-0001`
動工時間：2026-07-05 02:37 +08:00
更新時間：2026-07-05 02:48 +08:00

## Scope

新增 standalone `codex.fs.host` 的 `/chat` operator PoC form：

- `GET /chat` 回傳 HTTP 200 HTML form。
- `POST /chat` 接受 `sessionId`, `workerId`, `prompt` form fields。
- blank `workerId` 送到 derived SessionWorker / 包工頭 participant。
- nonblank `workerId` 送到指定 worker participant。
- 送訊邏輯共用 `acceptSessionMessageAsync`，不建立另一套 chat store。

## Boundary

`/chat` 不是 production PTCS participant-perspective Web UI。production PTCS Web 仍需 PTCS Host 或同 cluster 節點 reference `codex.fs.host`，並傳入 caller-owned PTCS `CommSpaMessageFabric`，讓 browser-visible participants 與 worker/session participants 分享同一個 truth。

## Evidence

- `dotnet build .\codex.fs.slnx`: passed, 0 warnings, 0 errors。
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`: passed。
- Test assertions cover `GET /chat`, `POST /chat`, `chatUri` in health JSON, OpenAPI `/chat` path, and session status containing the submitted chat prompt。

## Blocker

Superseded by `HOST-007`: current standalone `/chat` is a PTCS chat guard page, not a prompt form.
