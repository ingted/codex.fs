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
  -> runtime selects engine/model/approval from intent tags
  -> bounded Agy or Codex exec
  -> artifacts + note + final.md
  -> MessageFabric direct reply containing final text and artifact refs
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
| Ignore `engine/model/approval` in AI intent | Rejected | Causes the UI to look like JSON echo and prevents user-selected Codex execution. |
| Codex exec prompt as positional argument on Windows/non-TTY | Rejected | `codex exec` can treat stdin as non-terminal unless prompt is provided through redirected stdin with `-`. |
| Pass `gpt-5-codex` directly for ChatGPT subscription CLI | Rejected | Local `codex-cli 0.142.4` rejects that model with a ChatGPT subscription; use Codex default unless a compatible explicit model is supplied. |

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
6. `HostWebShell.tryParseAiIntent` 必須把 `engine`、`model`、`reasoning`、`invocation`、`approval` 轉成 MessageFabric tags，讓 runtime cycle 能依每次 intent 選擇 Codex 或 Agy。
7. `RuntimeMessageFabricCycle` 以最新 message tags 決定 effective engine；`engine:codex` 走 `RuntimePromptLoop.planCodexExecExecution`，`engine:agy` 走既有 Agy print planner。
8. Windows Codex CLI 若由 npm 安裝，runtime 優先解析 native `codex.exe`，避免 `ProcessStartInfo` 無法直接啟動 PowerShell/npm shim。
9. Codex exec 使用 `exec --dangerously-bypass-approvals-and-sandbox --cd <workingDir> --color never --output-last-message <path> -`，prompt 由 UTF-8 stdin 寫入並關閉 stdin。
10. `model=default` 與 `model=gpt-5-codex` 在 ChatGPT subscription profile 下正規化為不傳 `--model`；這保留 UI 意圖但避免 CLI 拒絕執行。
11. Foreman runtime cycle 必須忽略 self-authored MessageFabric replies，只 ack cursor，不把自己的 artifact reply 當成下一輪 user prompt。

## 影響範圍

| Area | Change |
| --- | --- |
| `codex.fs` | Agy print command planning gains permission policy field; Codex exec planning gains stdin, model normalization and output-last-message artifact support. |
| `codex.fs.ptcs` | Runtime cycle options and actor command carry Agy/Codex execution policy, engine overrides and self-reply filtering. |
| `codex.fs.host` | Product webshell starts Foreman with shell-enabled policy, forwards AI intent tags and starts AI intent bridge. |
| `codex.fs.web` | Existing intent schema remains; Codex model UI defaults to `default` for ChatGPT subscription compatibility. |
| Docs/tests | WEBR-006/E2E-004 caveat is corrected by WEBR-009/E2E-005 and Codex regression coverage. |

## 驗收

1. Unit/compiled tests assert Agy rendered argv contains `--dangerously-skip-permissions` before `--print` when Foreman policy requests it。
2. Real browser `/chat` verifier sends `hi 請用 powershell 取日期時間` to Foreman and observes completed artifact reply plus final/note files。
3. AI Chat append-page verifier appends `codex.fs.web.ai-intent.v1` and observes that Foreman receives a MessageFabric prompt and produces an artifact reply。
4. Codex AI intent verifier appends `engine=codex` / `model=gpt-5-codex` / `approval=never`, then verifies rendered argv uses `codex.exe exec`, includes `--output-last-message`, omits unsupported `--model gpt-5-codex`, and final output contains the requested date/token。
5. Runtime must preserve prompt/reply stdio artifacts and reply body must contain final message text plus artifact refs instead of raw JSON echo only。
6. Docs state that MessageFabric is logical chat/visibility, ActorFabric is runtime ownership, and worker journal/wpcs is physical execution truth。

## 2026-07-05 Correction: Codex intent must execute Codex

使用者回報以下 intent 仍像 echo，不像真實 `codex exec`：

```json
{"schema":"codex.fs.web.ai-intent.v1","target":{"mode":"foreman","scope":"direct","participantId":"agent.codexfs.foreman","groupId":""},"engine":{"engine":"codex","model":"gpt-5-codex","reasoning":"medium"},"invocation":{"mode":"exec","approval":"never"},"body":"hi 請用 powershell 執行 Get-Date"}
```

Root cause:

- `HostWebShell.tryParseAiIntent` 只送出 `body`，沒有把 `engine/model/reasoning/invocation/approval` 帶入 MessageFabric tags。
- `RuntimeMessageFabricCycle` 固定 Agy profile，沒有 per-message effective engine selection。
- `ProcessRunner` 不能啟動 Windows npm shim `codex`，且缺少 UTF-8 stdin 支援。
- Codex CLI 在非 TTY headless 情境必須使用 `-` 搭配 stdin；直接用 positional prompt 會失敗。
- `gpt-5-codex` 對目前 ChatGPT subscription 的 local Codex CLI 不可用，必須轉成 default model。
- Foreman 自己回自己的 MessageFabric artifact reply 會被下一輪 runtime 當 prompt 消費，造成自我迴圈與假回應感。

Correction acceptance evidence:

- `misc/verifyAiIntentBridge.fsx` with Codex intent passed on 2026-07-05 21:58 +08:00.
- Final artifact: `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-c5a847a2698c\sessions\foreman\runs\run-20260705135834033-f0d6b35d\final.md`.
- Rendered argv artifact: `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-c5a847a2698c\sessions\foreman\runs\run-20260705135834033-f0d6b35d\rendered-argv.json`.

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
