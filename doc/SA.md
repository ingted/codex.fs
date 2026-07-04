# System Architecture

版本：`0.2.0-draft`  
狀態：Draft  
對應需求：`doc/Requirement.md`

## 1. 架構摘要

`codex.fs` 採 lightweight CLI execution wrapper + PTCS fabric consumer 架構。系統不把 Codex CLI、Agy CLI 或其他 coding agent CLI 內嵌到 application code 中，而是以 normalized command model 與 engine adapter 隔離 CLI 差異；同時不自行發明 actor/message fabric，而是使用 PTCS `CommSpaActorFabric`、`CommSpaMessageFabric` 與 durable ingress/task result boundary。

Production path：

```text
Human / PTCS UI / codex.fs.cli / upstream actor
  -> PTCS CommSpaMessageFabric / durable agent task
  -> codex.fs runtime / worker actor hosted by codex.fs.host or PTCS host process
  -> Engine Adapter
  -> External CLI process
  -> Artifact Store
  -> PTCS MessageFabric reply / result-vault reference
```

Actor/sharding path：

```text
PTCS caller-owned or package-owned ActorSystem
  -> CommSpaActorFabric requiredConfig + region/proxy
  -> codex.fs.actor WorkerActor / specialized SessionActor
  -> codex.fs.runtime prompt-loop
  -> MessageFabric / DurableIngress for communication and task identity
```

此設計讓 application actor 只面對 PTCS fabric + `codex.fs` typed contract，不直接知道 `codex exec`、`agy --print`、stdout/stderr layout 或版本差異。

## 2. PTCS alignment

本架構沿用 PTCS current-state docs：

- `CommSpaActorFabric` owns/attaches Akka Cluster Sharding fabric。
- `CommSpaMessageFabric` owns communication send/poll/ack/wait/drain semantics。
- `DurableIngress` owns durable command admission/task ticket first-slice semantics。
- `CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync` is the preferred durable agent task handoff.
- Task/result vault and reality boundary prevent transport retry from becoming duplicated logical work。

`codex.fs` 不提供：

- independent ActorFabric。
- independent MessageFabric。
- independent inbox cursor/ack registry。
- independent durable ingress protocol。

## 3. 系統邊界

### 3.1 Core library

`codex.fs` 定義：

- run id / engine id / artifact id。
- normalized CLI run request/result。
- engine kind、engine version、capability surface。
- artifact manifest。
- compaction request/result。
- policy vocabulary。
- error model。

Core library 不啟動 process、不持有 actor system、不依賴 Web UI、不依賴 PTCS runtime instance。

### 3.2 Host runtime

`codex.fs.host` 定義 production boundary：

- 作為 NuGet package時：提供 host builder extensions 與 services。
- 作為 dotnet tool 時：啟動 standalone host。
- 接入 PTCS `CommHub` / `CommSpaMessageFabric` / `CommSpaActorFabric` / `DurableIngress`。
- 管理 engine registry、artifact store、lease、timeout、redaction、compaction。

### 3.3 CLI client

`codex.fs.cli` 是 terminal-facing client：

- 連到 host。
- 建立/attach session participant。
- 透過 PTCS MessageFabric 發送 prompt。
- wait/poll reply。
- 查詢 run status/artifacts。
- 在 PTCS Web UI 完善前提供主要互動介面。

### 3.4 Engine adapters

Engine adapter 只負責將 normalized request 映射到特定 CLI surface：

- `codex.fs.engine.codex`
- `codex.fs.engine.agy`

adapter 不應包含 session orchestration 或 MessageFabric 邏輯。

### 3.5 PTCS fabric integration

Integration layer 負責：

- 建立或接受 `CommHub`。
- 建立 `CommSpaMessageFabric` 或 `CommSpaMessageFabric.createDurable`。
- attach `CommSpaActorFabric` 到 caller-owned ActorSystem，或使用 package-owned PTCS fabric。
- 把 MessageFabric envelope / durable task envelope 轉成 `codex.fs` session/run request。

### 3.6 Product reset responsibility view

`RFC-PRODUCT-0001` defines the reset boundary for future implementation:

| Boundary | Responsibility | Must not own |
| --- | --- | --- |
| PTCS Host | WebSharper chat room, auth profile, PTCS hub/fabric ownership and browser extension hosting. | Headless engine invocation loop or codex.fs transcript persistence by default. |
| codex.fs runtime | Prompt/history assembly, local compact, headless CLI invocation, stdio capture, notes/artifacts and recovery boundary. | HTTP route concerns or PTCS Web UI layout. |
| codex.fs actor | `WorkerActor` and specialized `SessionActor` protocol, spawn/register/route, sharded delivery and participant lifecycle. | A second message fabric or direct browser chat store. |
| codex.fs host | Composition root, config, control endpoint, OpenAPI/Swagger, process/tool hosting, caller-owned PTCS fabric seam. | Canonical prompt semantics or product chat IA. |
| codex.fs CLI | Terminal participant client, default Foreman target, participant switching, engine/model/reasoning options and artifact queries. | Actor/message truth outside PTCS MessageFabric. |
| codex.fs Web | PTCS WebSharper extension/bundle such as `useAIChat(...)`, participant perspective and AI controls. | Standalone `/chat` replacement for PTCS chat. |

Prompt assembly therefore belongs to runtime/session actor behavior. `codex.fs.host` can invoke that behavior and expose diagnostics, but it is not the owner of the stitched conversation contract.

`ACTOR-001` resolves the first actor RFC slice: Foreman is the default SessionActor participant, workers register as PTCS `agent` participants, and actor shells call runtime while MessageFabric remains the human/agent chat truth.

`CLI-010` resolves the first interactive CLI RFC slice: `codex.fs.cli` is a terminal participant client with Foreman default target, explicit participant/worker/public/group switching, visible sender/target/perspective state and invocation-option handoff to runtime/actor. It does not own prompt assembly, headless process execution or chat history truth.

`PERSIST-001` resolves the transcript/note/artifact policy RFC slice: raw run evidence is private by default, MessageFabric/UI/CLI should render redacted summaries plus manifest/note refs, and compacted history must preserve message ids, run ids and artifact refs without replacing raw artifacts.

## 4. Runtime components

| Component | Responsibility |
| --- | --- |
| Host Service | 啟動 runtime、載入 config、接入 PTCS fabric、暴露 CLI/control endpoint。 |
| Runtime Orchestrator | 組合 MessageFabric batch、history、compaction、engine request、artifact/note persistence 與 reply intent。 |
| PTCS MessageFabric | canonical chat/mailbox fabric，負責 send/poll/ack/wait/drain。 |
| PTCS ActorFabric | actor/sharding runtime，負責 region/proxy/health/reality metadata。 |
| DurableIngress | durable task admission、task ticket、deadline/reality checks。 |
| WorkerActor | 共通 agent actor capability：register participant、consume work、call runtime、reply and coordinate children。 |
| SessionActor | Specialized WorkerActor / Foreman participant，負責 session mailbox、spawn/coordinate workers 與預設人機對話入口。 |
| Session Worker | Runtime-level session behavior；從 MessageFabric inbox 取訊息，管理 run loop、compaction、reply。 |
| Engine Registry | 根據 engine kind/version/capability 選 adapter。 |
| Process Runner | 啟動外部 CLI、capture stdout/stderr、處理 timeout/cancel。 |
| Artifact Store | 保存 prompt/output/event/final/result/manifest。 |
| Compactor | 在 history 過大時產生 compacted context。 |
| CLI Client | terminal interface，透過 host 與 PTCS MessageFabric 互動。 |

## 5. Session state machine

```text
Idle
  -> PollingInbox
  -> PreparingPrompt
  -> RunningEngine
  -> PersistingArtifacts
  -> ReplyingViaMessageFabric
  -> Compacting?
  -> AckingInbox
  -> Idle | PreparingPrompt
```

狀態說明：

| State | 說明 |
| --- | --- |
| Idle | session 沒有 active run，可 wait/poll MessageFabric inbox。 |
| PollingInbox | 透過 `PollInboxAsync` / `WaitInboxAsync` 收集 message batch。 |
| PreparingPrompt | 將 compacted history、message batch、policy 組成 run prompt。 |
| RunningEngine | host 執行外部 CLI；新 message 留在 MessageFabric inbox。 |
| PersistingArtifacts | 保存 stdout/stderr/event/final/result manifest。 |
| ReplyingViaMessageFabric | 透過 `SendAsync` 或 durable result reference 回覆。 |
| Compacting | 根據 policy 產生 compacted history。 |
| AckingInbox | 確認已納入 prompt 的 inbox cursor。 |

## 6. Message flow

### 6.1 Normal prompt

```text
Client/PTCS UI
  -> CommSpaMessageFabric.SendAsync(to=session participant)
  -> Session Worker WaitInboxAsync/PollInboxAsync
  -> ArtifactStore create run directory
  -> EngineRegistry select adapter
  -> ProcessRunner execute CLI
  -> ArtifactStore persist outputs
  -> CommSpaMessageFabric.SendAsync(reply summary + run reference)
  -> AckAsync(processed cursor)
```

### 6.2 Durable agent task

```text
Upstream caller
  -> CommSpaDurableMessageFabric.SubmitAgentTaskDurableAsync
  -> DurableIngress task ticket
  -> MessageFabric direct message to session participant
  -> Session Worker executes run
  -> ArtifactStore persists output
  -> MessageFabric reply or result-vault reference
  -> task status/result query by caller
```

### 6.3 Message during active run

```text
MessageFabric receives more messages
  -> session worker does not lose them
  -> current CLI run completes
  -> session worker persists result and replies
  -> next PollInboxAsync uses cursor to collect pending batch
  -> next prompt includes pending messages and compacted history
```

## 7. Engine capability model

CLI tools differ by command shape. The architecture uses capability surfaces instead of assuming a universal `exec` command.

| Engine | Version family | Surface | Notes |
| --- | --- | --- | --- |
| Codex CLI | `0.142.x` | `codex exec` | Supports `--json`, `--output-last-message`, `--output-schema`, stdin prompt. |
| Agy CLI | `1.0.x` | `agy --print` / `--prompt` | Supports single prompt print mode, timeout, conversation continuation; JSONL stream not assumed. |

Capability dimensions:

- `SingleTurnHeadless`
- `Continuation`
- `StructuredEventStream`
- `FinalMessageFile`
- `WorkspaceDirectories`
- `SandboxMode`
- `ModelSelection`
- `Timeout`
- `LogFile`

## 8. Storage architecture

Artifact store should be append-only per run. `note.md` is a redacted human-readable summary for browsing and compaction; it is not the canonical raw transcript.

```text
artifacts/
  sessions/
    <session-id>/
      history.jsonl
      compacted.md
      messagefabric-cursors.json
      runs/
        <run-id>/
          request.json
          ptcs-message-batch.jsonl
          prompt.md
          argv.json
          stdout.log
          stderr.log
          events.jsonl
          final.md
          result.json
          manifest.json
```

`events.jsonl` is optional and only present when the engine supports structured events.

MessageFabric remains the communication fact source. Artifact store is the execution evidence store. Public exports are redacted summaries/references only and must pass sensitive scanning before entering tracked repo paths.

## 9. Reliability architecture

Required recovery points:

- MessageFabric message accepted。
- MessageFabric cursor selected for prompt。
- run request created。
- process started。
- process completed/failed/timed out。
- artifacts persisted。
- reply sent through MessageFabric。
- cursor acked only after prompt/artifact persistence boundary is satisfied。

Durable path should use PTCS durable ingress/task ticket semantics. `codex.fs` must not claim crash-durable delivery beyond the selected PTCS profile capability.

## 10. Security architecture

Security defaults:

- no full environment dump。
- redact known secret patterns before writing human-readable output。
- separate private host artifacts from public package repository。
- record CLI path/version/capability, not auth token values。
- MessageFabric body is data, never shell command。
- PTCS non-secret reality metadata may be logged; PCSL root path, connection string, OAuth secret, PAT, bearer token must not be logged.

## 11. Configuration architecture

Host config should include:

- artifact root。
- PTCS CommHub / fabric mode。
- PTCS ActorFabric options or caller-owned ActorSystem attachment settings。
- MessageFabric durable mode。
- enabled engines。
- engine executable paths。
- default engine。
- per-engine default args。
- timeout policy。
- compaction policy。
- redaction policy。

Config should be expressed as typed records and load from explicit file/env/provider layers without leaking secrets into logs.

## 12. Deployment modes

| Mode | 說明 |
| --- | --- |
| PTCS-embedded | A PTCS host references `codex.fs.host` and starts services in-process. |
| Dotnet tool host | `codex.fs.host` runs standalone with package-owned or attached PTCS fabric; HTTP control endpoint must publish a LAN/routable advertised URI outside single-node dev. |
| Terminal client | `codex.fs.cli` connects to a running host and uses MessageFabric-backed session interaction. |
| Caller-owned ActorSystem | Host merges PTCS required config before ActorSystem creation and attaches `CommSpaActorFabric`. |

## 13. Open architecture decisions

| ID | Decision needed |
| --- | --- |
| SA-TBD-001 | Resolved: HTTP control endpoint for MVP; MessageFabric stays communication fact source; loopback is dev-only, clustered runtime uses advertised LAN/routable URI. |
| SA-TBD-002 | Resolved for RFC slice by `PERSIST-001`: file provider can be first, but runtime must depend on a provider-shaped persistence boundary for run evidence, notes, history entries, compaction output and ready-to-ack boundary. |
| SA-TBD-003 | Compactor engine: same selected CLI, separate cheap adapter, or pluggable function. |
| SA-TBD-004 | Resolved: first codex.fs durable handoff profile uses PTCS `CommSpaDurableMessageFabric` with volatile durable admission as the package-level ticketed boundary; production sharded crash-durable provider proof remains an OPS-002/future profile gate. |
| SA-TBD-005 | Whether first host starts package-owned PTCS fabric or requires caller-owned attachment. |
