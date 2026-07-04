# WBS.CLI-010 Interactive Participant CLI Client RFC

狀態：Done  
開始時間：2026-07-05 04:21 +08:00  
更新時間：2026-07-05 04:22 +08:00  
關聯 RFC：`doc/RFC/RFC-CLI-0002.interactive-participant-client.md`  
關聯 Test：`T-CLI-010`  
前置：`PRODUCT-001`, `ACTOR-001`, `CLI-009`

## 目標

定義 `codex.fs.cli` 作為 terminal participant client 的 UX/contract：預設與 Foreman `SessionActor` 溝通，可切換 target participant / worker / public / group，並可輸入 engine/model/reasoning/invocation options；CLI 不擁有 prompt loop、chat history 或 MessageFabric truth。

## 完成內容

- Accepted `RFC-CLI-0002`.
- 定義 CLI sender identity baseline `user.codexfs.cli` 與未來 `--participant-id` / profile 支援。
- 定義 first-use prompt default target Foreman / `agent.codexfs.foreman`。
- 定義 one-shot commands 與未來 `chat` interactive loop 的相容方向。
- 定義 `/target`、`/participants`、`/model`、`/engine`、`/runs`、`/artifacts`、`/notes` 等 terminal meta command contract。
- 定義 target 與 perspective 的差異：perspective 是 authorized read/render，不等於任意 impersonation。
- 定義 invocation options 由 CLI 收集、runtime/actor 解讀與 engine adapter render。
- 定義未來 `misc/verifyCliParticipantChat.fsx` 必須使用 installed tool + real host/PTCS fabric。

## 非完成內容

- 尚未實作 interactive `chat` command。
- 尚未新增 participant/profile Argu options。
- 尚未新增或執行 `misc/verifyCliParticipantChat.fsx`。
- 尚未改變現有 one-shot CLI behavior；既有 `session send/status/attach/drain` contract 保持。

## 後續

- Implementation WBS 應先擴充 CLI parser/DTO，再共用既有 HTTP/PTCS client code 實作 terminal loop。
- `PERSIST-001` 需定義 `/notes <run-id>` 與 transcript/note exposure policy。
- `WEB-001` 可沿用本 RFC 的 target/perspective vocabulary，避免 Web/CLI 出現兩套語意。
