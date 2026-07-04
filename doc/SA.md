# System Architecture

版本：`0.1.0-draft`  
狀態：Draft  
對應需求：`doc/Requirement.md`

## 1. 架構摘要

`codex.fs` 採 lightweight wrapper + durable host 架構。系統不把 Codex CLI、Agy CLI 或其他 coding agent CLI 內嵌到 application code 中，而是以 normalized command model 與 engine adapter 隔離 CLI 差異。

Production path：

```text
Human / Actor / CLI Client
  -> codex.fs.host
  -> SessionActor / WorkerActor runtime
  -> Engine Adapter
  -> External CLI process
  -> Artifact Store
  -> Reply / Status / Next turn
```

此設計讓 application actor 只面對 typed contract，不直接知道 `codex exec`、`agy --print`、stdout/stderr layout 或版本差異。

## 2. 系統邊界

### 2.1 Core library

`codex.fs` 定義：

- session id、run id、message id。
- normalized run request/result。
- engine kind、engine version、capability surface。
- artifact manifest。
- policy vocabulary。
- error model。

Core library 不啟動 process、不持有 actor system、不依賴 Web UI。

### 2.2 Host runtime

`codex.fs.host` 定義 production boundary：

- 作為 NuGet package 時：提供 host builder extensions 與 services。
- 作為 dotnet tool 時：啟動 standalone host。
- 管理 actor runtime、engine registry、artifact store、lease、timeout、redaction。

### 2.3 CLI client

`codex.fs.cli` 是 terminal-facing client：

- 連到 host。
- 建立/attach session。
- 發送 prompt。
- 查詢 run status/artifacts。
- 在 PTCS Web UI 完善前提供主要互動介面。

### 2.4 Engine adapters

Engine adapter 只負責將 normalized request 映射到特定 CLI surface：

- `codex.fs.engine.codex`
- `codex.fs.engine.agy`

adapter 不應包含 session orchestration 邏輯。

### 2.5 Actor integration

`codex.fs.akka` 提供 Akka.NET integration：

- `SessionActor`
- `WorkerActor`
- sharding helpers。
- reliable delivery hooks。
- persistence event schema。

actor shell 可使用 Akka class/abstract base，但 domain logic 應保持 F# record/function composition。

## 3. Runtime components

| Component | Responsibility |
| --- | --- |
| Host Service | 啟動 runtime、載入 config、暴露 API/transport endpoint。 |
| Session Directory | 管理 session metadata 與 actor entity id。 |
| SessionActor | 管理單一 session 的 mailbox/history/run loop。 |
| WorkerActor | 執行專門任務或代表子 session。 |
| Engine Registry | 根據 engine kind/version/capability 選 adapter。 |
| Process Runner | 啟動外部 CLI、capture stdout/stderr、處理 timeout/cancel。 |
| Artifact Store | 保存 prompt/output/event/final/result/manifest。 |
| Compactor | 在 history 過大時產生 compacted context。 |
| Transport Adapter | 將 terminal/PTC/Web message 轉成 normalized message。 |

## 4. Session state machine

```text
Idle
  -> PreparingPrompt
  -> RunningEngine
  -> PersistingArtifacts
  -> Replying
  -> Compacting? 
  -> Idle | PreparingPrompt
```

狀態說明：

| State | 說明 |
| --- | --- |
| Idle | session 沒有 active run，可接受新 prompt。 |
| PreparingPrompt | 將 history、pending messages、policy 組成 run prompt。 |
| RunningEngine | host 執行外部 CLI；新 message 進 pending mailbox。 |
| PersistingArtifacts | 保存 stdout/stderr/event/final/result manifest。 |
| Replying | 透過 transport 發送摘要、run id、artifact references。 |
| Compacting | 根據 policy 產生 compacted history。 |

## 5. Message flow

### 5.1 Normal prompt

```text
Client -> Host: SubmitMessage(sessionId, message)
Host -> SessionActor: AppendAndRun
SessionActor -> ArtifactStore: create run directory
SessionActor -> EngineRegistry: select adapter
EngineAdapter -> ProcessRunner: command file + argv + env
ProcessRunner -> ArtifactStore: stdout/stderr/event/final
SessionActor -> Client: reply summary + run reference
```

### 5.2 Message during active run

```text
Transport -> SessionActor: IncomingMessage
SessionActor(RunningEngine): append to pending mailbox
Engine run completes
SessionActor: persist result
SessionActor: append pending batch to history
SessionActor: start next run if policy allows
```

## 6. Engine capability model

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

## 7. Storage architecture

Artifact store should be append-only per run.

```text
artifacts/
  sessions/
    <session-id>/
      history.jsonl
      compacted.md
      runs/
        <run-id>/
          request.json
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

## 8. Reliability architecture

Required recovery points:

- message accepted by transport。
- message appended to session history。
- run request created。
- process started。
- process completed/failed/timed out。
- artifacts persisted。
- reply sent。

Akka integration should use persistence and reliable delivery for stateful or external-request message chains. The core design must not rely on in-memory mailbox only.

## 9. Security architecture

Security defaults:

- no full environment dump。
- redact known secret patterns before writing human-readable output。
- separate private host artifacts from public package repository。
- record CLI path/version/capability, not auth token values。
- message body is data, never shell command。

## 10. Configuration architecture

Host config should include:

- artifact root。
- enabled engines。
- engine executable paths。
- default engine。
- per-engine default args。
- timeout policy。
- compaction policy。
- transport endpoints。
- actor/sharding settings。
- redaction policy。

Config should be expressed as typed records and load from explicit file/env/provider layers without leaking secrets into logs.

## 11. Deployment modes

| Mode | 說明 |
| --- | --- |
| Library-embedded | A .NET app references `codex.fs.host` and starts services in-process. |
| Dotnet tool host | `codex.fs.host` runs standalone and exposes local control endpoint. |
| Terminal client | `codex.fs.cli` connects to a running host. |
| Actor cluster | Akka.NET cluster sharding manages many SessionActor entities. |

## 12. Open architecture decisions

| ID | Decision needed |
| --- | --- |
| SA-TBD-001 | Host control endpoint protocol: HTTP, named pipe, stdin/stdout, or all via plugins. |
| SA-TBD-002 | Artifact storage provider: file-only first or pluggable store first. |
| SA-TBD-003 | Compactor engine: same selected CLI, separate cheap adapter, or pluggable function. |
| SA-TBD-004 | PTCS integration boundary and RFC scope. |
| SA-TBD-005 | Akka persistence provider for first implementation. |
