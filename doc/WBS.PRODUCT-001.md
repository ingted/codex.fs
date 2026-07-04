# WBS.PRODUCT-001 Product Responsibility Reset

狀態：Done  
開始時間：2026-07-05 04:05 +08:00  
更新時間：2026-07-05 04:22 +08:00  
關聯 RFC：`doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`  
關聯 Test：`T-PRODUCT-001`

## 目標

建立產品層責任邊界，避免後續開發把 prompt assembly、headless invocation loop、PTCS Web chat 或 actor orchestration 繼續混入 `codex.fs.host` HTTP/diagnostics 層。

## 完成內容

- Accepted `RFC-PRODUCT-0001` as the product reset.
- 明確區分 `PTCS Host`、`codex.fs.host`、runtime、actor、CLI、Web bundle 與 persistence。
- 確認 prompt assembly belongs to runtime/session actor behavior, not host HTTP route handlers.
- 將後續可執行切片拆為 `RUNTIME-001`, `ACTOR-001`, `CLI-010`, `WEB-001`, `PERSIST-001`。

## 驗收

- `doc/Requirement.md` 新增產品責任邊界。
- `doc/SA.md` 新增 reset 後的 runtime/actor/host/Web/CLI responsibility view。
- `doc/SD.md` 新增 host/runtime/actor/prompt boundary。
- `doc/WBS.md` 與 `doc/Test.md` 建立可追蹤工項與測試列。
- `doc/DevLog.md` 與 `MCP.KM.md` append 追溯紀錄。

## 風險與後續

- 目前既有 alpha code 仍有 bounded single-cycle helper under host package；後續 `RUNTIME-001` 需把可重用 run-loop contract 拆出或至少在 SD 中鎖定 module 邊界。
- Actor sharding/durable delivery 尚未實作；`ACTOR-001` 必須先 RFC 化 `SessionActor` / `WorkerActor` protocol，再進 code。
- Web UI 不可用 standalone host `/chat` 替代；`WEB-001` 必須走 PTCS WebSharper extension/reference path。
