# RFC-WEB-0003 PTCS Artifact Ref Rendering

狀態：Accepted  
日期：2026-07-05  
關聯 WBS：`WEBR-007`  
關聯 Test：`T-WEBR-007`  
關聯 verifier：`misc/verifyArtifactRefsInPtcsShell.fsx`

## 背景

`ACTOR-003` 已讓 WorkerActor 透過 shared PTCS runtime cycle 呼叫 installed Agy，並產生 manifest/final/boundary artifacts。`WEBR-007` 的產品缺口是：PTCS classic `/chat` 裡的人只能看到 plain text reply，無法穩定掃描 run id、manifest、final、note refs。

PTCS classic chat 已有 tabs/nav、participant list、thread/composer 與 client extension registry。codex.fs 不應重寫 chat shell。

## 目標

1. 在 real PTCS classic `/chat` thread 中呈現 codex.fs worker reply artifact refs。
2. MessageFabric reply body 仍只包含 redacted summary + refs，不放 raw prompt/stdout/stderr。
3. Runtime first file provider 需寫出 `note.md`，並在 manifest、reply、ready-to-ack boundary 中保留 note ref。
4. Host webshell 預設註冊 `agent.codexfs.foreman`，讓使用者不需要先知道 session id。
5. 用 Playwright 驗證 real host + PTCS chat + real actor artifact refs。

## 非目標

- 不修改 PTCS 上游 chat IA。
- 不新增 standalone browser chat。
- 不把 artifact raw content 直接塞進 MessageFabric body。
- 不宣稱 durable sharded crash recovery 已完成。

## 決策

1. `CodexFs.Ptcs.RuntimeMessageFabricCycle` 寫出 `note.md` (`RunNoteMarkdown`)。
2. `RuntimePromptLoop.replyIntent` 回覆格式包含 `manifest=...; final=...; note=...; summary=...`。
3. `RuntimeReadyToAckBoundary` 增加 `RunNotePath`，確保 reply evidence 後、ack 前可復盤。
4. `codex.fs.web` WebSharper bundle 註冊 `PulseTradeRegisterRenderer` 的 artifact reply renderer。
5. 因 PTCS classic chat 目前 fallback path 直接建立 `pre.message-body`，codex.fs bundle 同時掃描 PTCS 既有 `pre.message-body` 節點並套用同一 renderer。這是最小 bridge，不重造 chat。
6. Artifact card 必須顯示 run、outcome、manifest、final、note 與 redacted summary，並提供穩定 `data-testid`。

## 驗收

`misc/verifyArtifactRefsInPtcsShell.fsx` 必須：

- build solution 並產生 WebSharper bundle；
- 跑 compiled tests 產生真實 ACTOR-003 artifact refs；
- 啟動 real `codex.fs.host` PTCS webshell；
- 用 PTCS `/chat/api/send` 送出含 real refs 的 MessageFabric chat message；
- 用 Playwright 打開 `/chat`，選擇 `agent.codexfs.foreman`；
- 驗證 `codexfs-artifact-reply`、manifest/final/note refs 與 summary 出現在 real chat thread；
- 產生 browser screenshot evidence。

## 影響範圍

- `src/codex.fs`：新增 `RunNoteMarkdown` 與 run note/reply/boundary contract。
- `src/codex.fs.ptcs`：runtime cycle 寫出 note artifact 並回傳 `RunNotePath`。
- `src/codex.fs.host`：PTCS webshell 預設註冊 Foreman participant。
- `src/codex.fs.web`：artifact reply renderer 與 PTCS fallback DOM bridge。
- `tests/codex.fs.Tests` 與 `misc/verifyArtifactRefsInPtcsShell.fsx`：real path verifier。

## 關聯文件

- `doc/Requirement.md`
- `doc/BA.md`
- `doc/SA.md`
- `doc/SD.md`
- `doc/WBS.md`
- `doc/WBS.WEBR-001.md`
- `doc/Test.md`
- `doc/Test.WEBR-001.md`
- `doc/Verification.md`
- `doc/DevLog.md`
