# Requirement

版本：`0.2.0-draft`  
狀態：Draft  
語言：繁體中文為 canonical；專有名詞保留英文。  

## 1. 參考基準

本文件依據 PTCS current-state 文件修正，不重新發明 actor/message fabric。

PTCS 來源基準：

- local worktree：`G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`
- 文件：`doc/Requirement.md`、`doc/SA.md`、`doc/SD.md`
- 關鍵 RFC：`RFC-SPA-UPSTREAM-0001` shared MessageFabric、`RFC-SPA-UPSTREAM-0002` external ActorSystem attachment、`RFC-SPA-UPSTREAM-0003` shared durable ingress、`RFC-SPA-UPSTREAM-0005` task/result vault integration、`RFC-PTC-SPA-0005` ACP / MessageFabric / static hosting boundary。

PTCS 已定義：

- `CommSpaActorFabric`：package-provided ActorFabric，支援 package-owned 與 caller-owned ActorSystem、Cluster Sharding region/proxy、required HOCON config、health/reality metadata。
- `CommSpaMessageFabric`：framework-neutral communication facade，支援 participant register、direct/public/group send、poll、ack、wait、drain、group、correlation idempotency。
- `CommSpaDurableMessageFabric` 與 `DurableIngress`：mutation/task admission、task ticket、result vault/reality boundary 的 first-slice contract。

`codex.fs` 必須消費這些 fabric，而不是新增平行的 actor/message/transport framework。

## 2. 背景

`codex.fs` 是一個 F#/.NET 輕量 agent execution wrapper，用於把 PTCS ActorFabric/MessageFabric 中的人與 agents 協作，接到可替換的 headless CLI engine，例如 Codex CLI 與 Agy CLI。

本專案不重寫 Codex、Agy 或其他 coding agent 的模型能力，也不重寫 PTCS 的 fabric。它提供：

- typed engine request/result model；
- CLI adapter/version surface；
- process runner；
- artifact capture；
- compaction policy；
- PTCS MessageFabric/ActorFabric integration；
- `codex.fs.host` production boundary；
- `codex.fs.cli` terminal client package；installed command is `codex.fs.cli`。
- `codex.fs.tool` short alias tool package；installed command is `codex.fs`。

核心原則：

- `codex.fs` 是 library/contract/policy vocabulary。
- `codex.fs.host` 是 PTCS fabric consumer，可作 NuGet package，也可作 dotnet tool。
- `codex.fs.cli` 是 terminal-facing client package；安裝後命令為 `codex.fs.cli`，在 PTCS Web UI 完善前與 `codex.fs.host` / PTCS MessageFabric 互動。
- `codex.fs.tool` 只提供 short alias command `codex.fs`，不得分叉另一套 CLI 行為。
- CLI engine 可替換，初期支援 Codex CLI 與 Agy CLI。
- Production workflow 必須透過 PTCS `MessageFabric`、`ActorFabric`、必要時 `DurableIngress` / task result vault，不直接建立另一套 message bus 或 cluster fabric。

### 2.1 產品責任邊界

`RFC-PRODUCT-0001` 將目前產品責任重設如下：

- `PTCS Host` 是 WebSharper chat/hub/auth profile 宿主；它不是 headless Codex invocation runtime，也不會自動保存 CLI stdio/notes。
- `codex.fs.host` 是 codex.fs runtime composition/control/docs/deployment boundary；它可以 reference PTCS fabric，也可以作 dotnet tool，但不擁有 prompt assembly semantics。
- prompt/history 拼接、local compact、headless CLI invocation、stdio capture、note/artifact persistence 與 recovery boundary 屬於 runtime/session worker 行為；在專案拆出 `codex.fs.runtime` 前，相關 module 必須與 HTTP route handler 保持清楚邊界。
- `SessionActor` 是 specialized `WorkerActor`，預設是 Foreman/包工頭 participant；它可 spawn/register 其他 worker participants，並透過 PTCS `MessageFabric` / `ActorFabric` 與人或其他 agents 溝通。
- `codex.fs.cli` 是 terminal participant client，預設與 Foreman/SessionActor 溝通；只有明確指定 participant/worker 時才切換目標。
- codex.fs Web UI 應作為 PTCS WebSharper extension/bundle，例如 `useAIChat(...)` 類型的客製 bundle，提供 participant perspective、engine/model/reasoning/invocation controls；不得用 standalone host `/chat` 取代 PTCS chat room。

## 3. 目標

1. 提供可嵌入、可獨立執行的 agent execution host。
2. 以 PTCS `CommSpaMessageFabric` 作為人與 agents、agents 之間的 canonical chat/mailbox fabric。
3. 以 PTCS `CommSpaActorFabric` / caller-owned ActorSystem attachment 作為 actor/sharding integration boundary。
4. 對需要 durable task admission 的工作使用 PTCS `DurableIngress`、`CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync` 與 result vault 語意。
5. 支援 headless CLI engine：
   - Codex CLI：以 `codex exec` 為主要 single-turn headless surface。
   - Agy CLI：以 `agy --print` / `agy --prompt` 為 single-turn headless surface。
6. 將每次 CLI run 的 prompt、stdout、stderr、JSONL/event、final message、exit code、artifact metadata 自動保存。
7. 支援 mailbox 增量收集：CLI run 期間到達的 user/agent messages 由 MessageFabric inbox 保留，進入下一輪 prompt。
8. 支援 local compaction，避免長 session history 直接 append 到 context window 爆量。
9. 支援 terminal client 先行驗證 multi-agent workflow，後續 PTCS Web UI 只需接同一套 fabric。
10. 支援不同 CLI 版本與不同 capability surface 的 argv render/parse/validate。
11. 避免 application code 直接手刻 `Process.Start("codex", ...)` 或 `Process.Start("agy", ...)`。

## 4. 非目標

1. 不提供模型 API provider 的通用 LLM SDK。
2. 不假設使用 OpenAI API key；Codex CLI 可使用既有 CLI auth。
3. 不把 CLI access token、refresh token、API key、shell secret 寫入 log 或 artifact。
4. 不把所有 CLI 都硬套成相同 `exec` 命令；抽象以 capability/run surface 為準。
5. 不實作另一套 MessageFabric、ActorFabric、transport inbox、ack cursor 或 durable ingress。
6. 不在初版交付完整 PTCS Web UI；Web UI 應使用 PTCS extension/fabric，而不是另做 UI fabric。
7. 不把 HTTP request、WebSocket frame、MCP call 或 terminal command 當成 logical work identity；task identity 需沿用 PTCS result-vault/reality boundary。

## 5. 使用者與角色

| 角色 | 說明 |
| --- | --- |
| Human Operator | 透過 `codex.fs` terminal command 或 PTCS UI 發送任務、查看回覆與 artifacts。 |
| PTCS MessageFabric Participant | user/agent identity，透過 MessageFabric direct/public/group inbox 溝通。 |
| CodexFs Session Worker | 以 MessageFabric inbox 為 mailbox，負責 run loop、compaction、artifact capture、reply。 |
| PTCS ActorFabric Host | 擁有或 attach Akka ActorSystem / sharding region / proxy 的 runtime。 |
| Host Operator | 部署與維護 `codex.fs.host`，設定 PTCS fabric、workspace、engine、storage、policy。 |
| Engine Adapter | 將 normalized run request 轉成特定 CLI 版本的 argv 與 artifact mapping。 |

## 6. 核心情境

### 6.1 Terminal-driven session

1. 使用者透過 `codex.fs.cli` 或 `codex.fs` 建立或 attach session。
2. CLI client 將 prompt 送入 `codex.fs.host`。
3. host 透過 `CommSpaMessageFabric.SendAsync` 或 durable agent-task handoff 將 prompt 送到 session worker / 包工頭 participant；只有 CLI 明確指定 worker id 時才送到指定 worker participant。
4. session worker poll/wait inbox，組合 history 與 pending messages。
5. session worker 選擇 engine adapter，建立 run request。
6. `codex.fs.host` 執行 headless CLI。
7. run artifacts 保存後，host 透過 MessageFabric 回傳摘要與 artifact references。
8. 若 run 期間 MessageFabric inbox 有新訊息，session worker 進入下一輪。

### 6.2 Production actor workflow

1. 上游 actor/application 以 PTCS MessageFabric agent task envelope 或 ActorFabric route 送出 typed task。
2. `codex.fs.host` 在 PTCS fabric 內接受 task，取得 operation/task identity。
3. session worker 執行 CLI run loop。
4. 完成後保存 artifacts，並透過 MessageFabric/result vault 回覆 status/result reference。

### 6.3 Multi-agent collaboration

1. 多個 session/worker 以 MessageFabric participant 或 group 互相溝通。
2. host 不建立私有 message bus；跨 agent message 走 PTCS MessageFabric。
3. 對需要 durable admission 的 agent task，使用 `CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync`。
4. 每個 session 的 CLI run 只取得該 session 需要的 compacted history。
5. host 保存完整 artifact，MessageFabric 只傳摘要與 reference。

### 6.4 CLI engine replacement

1. 使用者設定 session 使用 `codex` 或 `agy` engine。
2. host probe installed CLI version 與 capability。
3. adapter registry 選擇對應 surface module。
4. normalized request 轉成 CLI-specific argv。

## 7. 功能需求

### R-001 Host runtime

`codex.fs.host` 必須可作為：

- NuGet package：可由既有 .NET/PTCS host app reference。
- dotnet tool：可直接安裝、啟動、操作。

host 必須能接入 PTCS：

- package-owned PTCS fabric；
- caller-owned ActorSystem + `CommSpaActorFabric.attachRegionToSystem` / `attachProxyToSystem`；
- `CommHub` + `CommSpaMessageFabric`；
- optional `DurableIngress` / durable MessageFabric。

standalone host tool 必須提供基本 operator usability route：

- `/` landing page；
- `/chat` legacy guard page，明確告知 browser chat 應使用 PTCS WebSharper chat room；
- `/diagnostics/session-send` diagnostics form，能送 prompt 到 default Foreman/SessionWorker，並可選填 worker override；
- `POST /api/codexfs/foreman/messages`，供 CLI 在使用者不知道 session id 時送到包工頭；
- `/api/codexfs/host/health`；
- `/openapi/v1.json` 與 `/docs/index.html` when API docs enabled。

`/chat` 不是 package-owned standalone host 的操作 PoC，也不是 production PTCS participant-perspective Web UI。production browser chat 必須使用既有 PTCS Host WebSharper chat room 與 caller-owned PTCS MessageFabric / ActorFabric，讓 worker/session participants 以相同 hub/fabric 出現。

### R-002 CLI client

`codex.fs.cli` package 安裝後提供 `codex.fs.cli` command，`codex.fs.tool` package 提供相同 CLI surface 的 `codex.fs` short alias。兩者必須能在 terminal 中：

- 建立/attach session。
- 送出 prompt。
- 未指定 session 時預設把 prompt 送給 default Foreman/SessionWorker / 包工頭，不要求使用者先知道 session id。
- 指定 session 時把 prompt 送給該 session 的 SessionWorker；只有指定 worker id 時改送指定 worker。
- wait/poll session reply。
- 查詢 run artifacts。
- drain pending inbox。
- 執行 minimal admin operation，例如 list sessions、cancel run、engine probe。
- host 不可連線或使用者誤把 process PID 當 port 時，回 readable non-zero error，不得吐出 unhandled .NET stack trace。

CLI client 不得繞過 PTCS MessageFabric 建立平行 chat store。

### R-003 Session worker run loop

Session worker / runtime 必須支援：

- Idle / PreparingPrompt / RunningEngine / PersistingArtifacts / Replying / Compacting 狀態。
- run 期間以 MessageFabric inbox/cursor 收集 pending messages。
- run 完成後將 pending batch append 到下一輪 prompt。
- run artifacts 與 MessageFabric chat history 分離保存。
- reply 以 MessageFabric envelope 或 result-vault reference 傳回。

Prompt assembly 不應由 `codex.fs.host` HTTP route handler 實作；host 只能呼叫 runtime/session worker contract 並暴露 control/docs/health。

### R-004 Artifact capture

每次 run 至少保存：

- PTCS task/message identity。
- normalized request。
- rendered CLI argv metadata。
- prompt input。
- stdout。
- stderr。
- final message。
- exit code。
- started/completed timestamps。
- selected engine/version/capability。
- artifact manifest。

若 engine 支援 event stream，例如 Codex CLI `--json`，必須保存 JSONL event stream。

### R-005 Compaction

host 必須支援 local compaction policy：

- 根據 byte/token estimate 或 message count 觸發。
- summary 不覆蓋原始 artifacts。
- compacted history 必須保留未完成事項、決策、open blockers、PTCS message ids、run ids、artifact references。

### R-006 Version-aware CLI surface

系統必須支援不同 CLI kind / version / capability surface：

- Codex CLI `0.142.x`：`codex exec` surface。
- Agy CLI `1.0.x`：`agy --print` / `--prompt` surface。

新增 CLI surface 時不得破壞既有 surface module。

### R-007 Argument parsing policy

所有 F# CLI argument parsing 必須使用 `FAkka.Argu`。不得手刻 positional/key-value parser。

### R-008 Secret handling

系統不得：

- 將 API key、OAuth secret、PAT、SSH private key、access token、refresh token 寫入 artifact/log。
- 將 shell environment 全量 dump 到 artifact。
- 在 MessageFabric body、public output 或 diagnostics 中顯示 secret value。

### R-009 PTCS fabric integration

`codex.fs` 必須使用 PTCS fabric：

- chat/mailbox：`CommSpaMessageFabric`。
- durable task admission：`CommSpaDurableMessageFabric` / `DurableIngress`。
- actor/sharding runtime：`CommSpaActorFabric`。
- actor system attachment：`CommSpaActorFabric.requiredConfig` + caller-owned attach API。

不得新增與 PTCS fabric 平行的 send/poll/ack/wait/drain API 作為 production path。

## 8. 非功能需求

| 類別 | 需求 |
| --- | --- |
| Reliability | session state、PTCS task identity 與 artifact manifest 可重啟恢復。 |
| Traceability | 每次 run 可由 PTCS message/task id 與 run id 找到 prompt、output、result 與 reply。 |
| Replaceability | CLI engine 可新增/替換，不改 PTCS fabric integration。 |
| Security | secret redaction 與最小必要 artifact capture 是預設行為。 |
| Portability | 初期支援 Windows；設計不阻礙 Linux/macOS。 |
| Observability | host 提供 structured status/event，CLI 可查詢，PTCS reality metadata 不外洩 secret。 |
| Lightweight | core package 不依賴 Web UI、不重寫 PTCS fabric、不強制特定 engine。 |

## 9. Package 初始邊界

| Package / Tool | 用途 |
| --- | --- |
| `codex.fs` | Core engine contracts、domain models、policy vocabulary。 |
| `codex.fs.runtime` | Planned reusable prompt loop、headless invocation、stdio/notes/artifacts、local compact 與 recovery boundary；拆出前以清楚 module 邊界暫留 core/host。 |
| `codex.fs.actor` | Planned PTCS ActorFabric adapter，包含 `WorkerActor` / specialized `SessionActor` protocol、spawn/register/route 與 durable delivery。 |
| `codex.fs.host` | PTCS fabric consumer host package；composition/control/OpenAPI/Swagger/deployment boundary，不直接擁有 prompt semantics。 |
| `codex.fs.host.tool` | Standalone dotnet tool command `codex.fs.host`，包裝 host package。 |
| `codex.fs.cli` | Terminal client dotnet tool package；installed command `codex.fs.cli`。 |
| `codex.fs.tool` | Short alias dotnet tool package；installed command `codex.fs`。 |
| `codex.fs.web` | Planned PTCS WebSharper AI chat extension/bundle，提供 participant perspective 與 engine controls。 |
| `codex.fs.persistence` | Planned transcript/note/artifact provider boundary。 |
| `codex.fs.engine.codex` | Codex CLI adapter。 |
| `codex.fs.engine.agy` | Agy CLI adapter。 |

不新增 `codex.fs.akka` 作為獨立 ActorFabric；actor runtime 走 PTCS `CommSpaActorFabric`。若未來需要 extension package，命名應明確為 PTCS integration，而不是替代 fabric。

## 10. 驗收標準

初版文件與後續實作需能回答：

1. 如何建立 MessageFabric participant/session 並送出一次 prompt。
2. 如何選擇 Codex/Agy engine。
3. 如何保存 CLI output，不再靠人工複製 terminal history。
4. 如何在 run 期間透過 MessageFabric inbox 收集 pending messages。
5. 如何查詢 artifacts 與 final reply。
6. 如何新增一個 CLI surface version。
7. 如何避免 secret 被寫入 public artifact 或 MessageFabric body。
8. 如何接入 existing PTCS ActorFabric/MessageFabric，而不是另起 fabric。

## 11. 後續文件

本文件之後應補：

- `doc/WBS.md`
- `doc/Test.md`
- PTCS fabric integration RFC 或 design note
- Host deployment/runbook
