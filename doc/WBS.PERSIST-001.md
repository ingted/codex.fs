# WBS.PERSIST-001 Transcript / Note / Artifact Store RFC

狀態：Done  
開始時間：2026-07-05 04:27 +08:00  
更新時間：2026-07-05 04:27 +08:00  
關聯 RFC：`doc/RFC/RFC-PERSIST-0001.transcript-note-artifact-store.md`  
關聯 Test：`T-PERSIST-001`  
前置：`PRODUCT-001`, `RUNTIME-001`, `OPS-002`, `CLI-010`

## 目標

定義 transcript/note/artifact persistence policy，讓 runtime/actor 後續實作能自動保存 headless CLI run 的 prompt、stdio、final reply、manifest、note 與 ready-to-ack boundary，取代人工複製 terminal history。

## 完成內容

- Accepted `RFC-PERSIST-0001`.
- 定義 private raw artifacts 與 public redacted export 邊界。
- 定義每次 run 的最小 evidence：PTCS refs、cursor、prompt、request、argv metadata、stdout/stderr、final/events/result、manifest、note、session-boundary。
- 定義 `note.md` 作為 redacted human-readable run summary，不取代 raw artifacts。
- 定義 MessageFabric reply 只傳 redacted summary 與 manifest/note refs，不塞 raw transcript。
- 定義 local compact 只能消費 notes/refs/summary，且必須保留 message ids、run ids、artifact refs。
- 定義未來 `misc/verifyTranscriptStore.fsx` 必須使用 real runtime/host path 與 private artifact root。

## 非完成內容

- 尚未新增 `codex.fs.persistence` project。
- 尚未實作 provider port 或新 artifact kinds。
- 尚未新增或執行 `misc/verifyTranscriptStore.fsx`。
- 尚未改變現有 `SessionEngineCycle` artifact layout；既有 E2E/OPS 證據仍是 bounded helper evidence。

## 後續

- Implementation WBS 應先抽出 persistence port/provider，再把 `SessionEngineCycle` 或 runtime interpreter 改為透過該 port 寫入 evidence。
- `WEB-001` 應沿用 manifest/note refs 進行 artifact rendering，不直接顯示 raw transcript。
- Durable provider work 應把 manifest/result identity 接到 PTCS result vault，避免 retry 重跑已完成 work。
