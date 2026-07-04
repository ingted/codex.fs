# WBS.RUNTIME-001 Runtime Prompt-loop Package Boundary RFC

狀態：Done  
開始時間：2026-07-05 04:13 +08:00  
更新時間：2026-07-05 04:25 +08:00  
關聯 RFC：`doc/RFC/RFC-RUNTIME-0001.prompt-loop-package-boundary.md`  
關聯 Test：`T-RUNTIME-001`

## 目標

定義 runtime prompt-loop package/namespace boundary，讓 host、actor、CLI/verifier 與未來 Web integration 共用同一套 prompt/history/engine/artifact/note/recovery contract。

## 完成內容

- Accepted `RFC-RUNTIME-0001`.
- 定義 runtime owns orchestration, adapters own transport。
- 定義 deterministic `decideCycle` + side-effect `interpretCycleAsync` 的 preferred F# contract shape。
- 定義 cursor/prompt/request/engine/artifact/note/reply/ready-to-ack/ack ordering。
- 將 `SessionEngineCycle.runSingleCycleAsync` 標成 bounded host-era evidence and migration candidate。

## 非完成內容

- 尚未新增 `codex.fs.runtime` project。
- 尚未搬移 `SessionEngineCycle` implementation。
- 尚未新增或執行 `misc/verifyRuntimePromptLoop.fsx`。

## 後續

- `ACTOR-001` 可依此 contract 定義 `WorkerActor` / `SessionActor` protocol。
- 未來 DEV slice 應新增 runtime modules/tests/verifier，再更新此 detail 或新增 implementation WBS row。
