# RFC-RUNTIME-0002 Foreman control plane and AI intent bridge

狀態：Accepted for implementation  
建立時間：2026-07-05 20:20 +08:00  
關聯：REQ R-010、SA §14、SD §14.4、WBS FOREMAN-001/FOREMAN-002/WEBR-009/E2E-005、Test T-FOREMAN-001/T-FOREMAN-002/T-WEBR-009/T-E2E-005

## 背景

`ptcs-webshell` 已能啟動 PTCS classic `/chat`、註冊 `codex.fs.web` WebSharper bundle、顯示 Foreman participant，並讓 `/chat` prompt 進入 `agent.codexfs.foreman` 的 MessageFabric inbox。

但 `codexfs-ai-chat` append page 目前只 append `codex.fs.web.ai-intent.v1` JSON 到 PTCS append-page set stream。這提供了可觀察的 UI intent，卻沒有把 intent 投遞給 Foreman actor runtime。因此使用者在 `http://10.28.112.93:18488/page/webr006-ai8` 對 Foreman 說「hi 請用 powershell 取日期時間」時，Foreman 不會必然收到 MessageFabric prompt，也不會產生 headless execution artifact/reply。

同時，Agy `--print` 的參數順序有實際限制：`--print` 之後的 tokens 會被視為 prompt text。需要把 execution policy 與 argv order 明確固定，避免 `--print-timeout` 或 permission flag 被誤塞進 prompt。

## 共識

本 RFC 固定以下分層：

| Layer | 責任 | 不負責 |
| --- | --- | --- |
| MessageFabric | observable logical chat、UI thread、participant visibility、public/direct/group delivery | runtime ownership、worker lifecycle、physical transcript truth |
| ActorFabric | runtime ownership、sharded worker lifecycle、Foreman/Worker spawn/register | browser UI state、append page storage |
| Worker journal / wpcs | codex physical session truth、prompt/stdout/stderr/final/manifest/note、compaction input | participant discovery、chat delivery |
| SA MCP tools | Foreman 控制 worker 的主要介面，例如 spawn worker、指定 profile、分派 worker prompt | 人類 UI chat 的唯一傳輸層 |
| Codex/Agy exec | 每個 actor 實際做事的 bounded execution | 多 participant visibility、durable chat projection |

## 目標

1. Foreman 對基本 prompt `hi 請用 powershell 取日期時間` 走 real path：PTCS MessageFabric -> ActorFabric Foreman runtime loop -> Agy headless execution -> artifacts/note -> MessageFabric reply。
2. `codexfs-ai-chat` append-page intent 不再停在 UI stream；host 必須橋接 `codex.fs.web.ai-intent.v1` 到 MessageFabric target。
3. Agy headless invocation 對 Foreman 可執行 shell/tool 類需求時，必須明確使用支援的 permission policy，且 argv order 保證 permission/timeout flags 在 `--print` 之前。
4. 文件與驗證不可再宣稱 append intent 等同 runtime response。

## 非目標

- 本 RFC 不完成 crash-durable sharded replay。
- 本 RFC 不實作完整 SA MCP server toolset；只固定它是 Foreman 控制 worker 的主要未來介面。
- 本 RFC 不把 append page 改造成 worker journal；append page 只提供 observable UI intent/history。
- 本 RFC 不要求支援 Gemini CLI；目前實作重點是 Agy `1.0.x`。

## 情境

### `/chat` direct Foreman

```text
human participant
  -> PTCS /chat composer
  -> MessageFabric direct message to agent.codexfs.foreman
  -> Foreman actor RunRuntimeCycle
  -> Agy --dangerously-skip-permissions --print-timeout=... --print <assembled prompt>
  -> artifacts + note + final.md
  -> MessageFabric direct reply containing artifact refs
```

### `codexfs-ai-chat` append page

```text
browser AI controls
  -> PTCS append-page set value: codex.fs.web.ai-intent.v1
  -> host-side AI intent bridge scans same CommHub projection
  -> bridge sends MessageFabric direct/public/group message according to intent.target
  -> Foreman/worker runtime consumes MessageFabric inbox
```

Bridge delivery uses a service participant such as `user.codexfs.web.ai-intent` as the sender until authenticated PTCS browser identity can be supplied by the page payload.

## 方案取捨

| Option | Decision | Reason |
| --- | --- | --- |
| Browser calls host runtime endpoint directly | Rejected | Bypasses MessageFabric participant visibility and repeats the standalone `/chat` mistake. |
| Host bridge reads append-page HTTP APIs | Deferred | Works, but unnecessary in-process when `HostWebShell` already owns the same `CommHub`. |
| Host bridge reads `CommHub.SetsSnapshot` and sends MessageFabric | Accepted | Uses PTCS storage/read model and MessageFabric delivery without new fabric. |
| Always enable Agy dangerous permission for all runtime calls | Rejected | Too broad; only Foreman product webshell command uses explicit opt-in initially. |
| Per-command execution policy in `RunRuntimeCycle` | Accepted | Keeps default safe while Foreman can handle shell/tool prompts. |

## 決策

1. `ActorFabricBinding.RunRuntimeCycle`、`RuntimeMessageFabricCycle.RuntimeCycleOptions` 與 `RuntimePromptLoop.AgyPrintExecutionInput` 增加 `AgyDangerouslySkipPermissions` / `DangerouslySkipPermissions` option。
2. `HostWebShell.foremanRuntimeCommand` 對 product Foreman loop 設定 `AgyDangerouslySkipPermissions = Some true`。
3. `RuntimePromptLoop.buildAgyPrintCommand` 渲染 `--dangerously-skip-permissions` 且此 flag 必須在 `--print` 前。
4. `HostWebShell` 啟動 `AI intent bridge` background loop：
   - 掃描同一個 `CommHub` 的 append pages；
   - 選取 `Shape = codexfs-ai-chat` 的 page；
   - 讀取 page set projection 中尚未處理的 value；
   - 解析 `codex.fs.web.ai-intent.v1`；
   - 依 target `foreman` / `participant` / `public` / `group` 送入 MessageFabric；
   - 使用 `valueId` 作 in-process dedupe。
5. host 啟動時註冊預設 AI Chat append page 與 Foreman key，避免第一次使用者不知道 page id/key。

## 影響範圍

| Area | Change |
| --- | --- |
| `codex.fs` | Agy print command planning gains permission policy field and argv order coverage. |
| `codex.fs.ptcs` | Runtime cycle options and actor command carry Agy execution policy. |
| `codex.fs.host` | Product webshell starts Foreman with shell-enabled Agy policy and starts AI intent bridge. |
| `codex.fs.web` | Existing intent schema remains; behavior changes because host now consumes that intent. |
| Docs/tests | WEBR-006/E2E-004 caveat is corrected by WEBR-009/E2E-005. |

## 驗收

1. Unit/compiled tests assert Agy rendered argv contains `--dangerously-skip-permissions` before `--print` when Foreman policy requests it。
2. Real browser `/chat` verifier sends `hi 請用 powershell 取日期時間` to Foreman and observes completed artifact reply plus final/note files。
3. AI Chat append-page verifier appends `codex.fs.web.ai-intent.v1` and observes that Foreman receives a MessageFabric prompt and produces an artifact reply。
4. Docs state that MessageFabric is logical chat/visibility, ActorFabric is runtime ownership, and worker journal/wpcs is physical execution truth。

## 關聯文件

- `doc/Requirement.md`
- `doc/SA.md`
- `doc/SD.md`
- `doc/WBS.md`
- `doc/WBS.FOREMAN-001.md`
- `doc/Test.md`
- `doc/Test.FOREMAN-001.md`
- `doc/RFC/RFC-WEB-0002.ptcs-classic-webshell-rewrite.md`
- `doc/RFC/RFC-ACTOR-0002.actor-runtime-artifact-provider.md`
