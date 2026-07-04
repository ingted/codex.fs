# Requirement

版本：`0.1.0-draft`  
狀態：Draft  
語言：繁體中文為 canonical；專有名詞保留英文。  

## 1. 背景

`codex.fs` 是一個 F#/.NET 輕量 agent control-plane wrapper，用於把人與 agents 的長生命週期協作，收斂成可追溯、可重啟、可替換 CLI engine 的 production workflow。

本專案不重寫 Codex、Agy 或其他 coding agent 的模型能力。它提供 typed API、host runtime、CLI client、adapter contract 與 artifact/audit boundary，讓 actor/session runtime 不需要直接散落呼叫外部 CLI。

核心原則：

- `codex.fs` 是 library/contract/policy vocabulary。
- `codex.fs.host` 是 production runtime boundary，可作 NuGet package，也可作 dotnet tool。
- `codex.fs.cli` 是 terminal-facing client，用於 Web UI 完成前操作 `codex.fs.host` 與 `SessionActor`。
- CLI engine 可替換，初期支援 Codex CLI 與 Agy CLI。
- Production workflow 可使用 actor system 管理 session/mailbox/history/compaction/run artifacts。

## 2. 目標

1. 提供一個可嵌入、可獨立執行的 agent host runtime。
2. 支援 `SessionActor` 以 session 為單位處理 chat/message/history/run loop。
3. 支援 headless CLI engine：
   - Codex CLI：以 `codex exec` 為主要 single-turn headless surface。
   - Agy CLI：以 `agy --print` / `agy --prompt` 為 single-turn headless surface。
4. 將每次 CLI run 的 prompt、stdout、stderr、JSONL/event、final message、exit code、artifact metadata 自動保存。
5. 支援 mailbox 增量收集：CLI run 期間到達的 user/agent messages 不丟失，進入下一輪 prompt。
6. 支援 local compaction，避免長 session history 直接 append 到 context window 爆量。
7. 支援 terminal client 先行驗證 multi-agent workflow，未來再接 PTCS/Web UI。
8. 支援不同 CLI 版本與不同 capability surface 的 argv render/parse/validate。
9. 避免 application code 直接手刻 `Process.Start("codex", ...)` 或 `Process.Start("agy", ...)`。

## 3. 非目標

1. 不提供模型 API provider 的通用 LLM SDK。
2. 不假設使用 OpenAI API key；Codex CLI 可使用既有 CLI auth。
3. 不把 CLI access token、refresh token、API key、shell secret 寫入 log 或 artifact。
4. 不把所有 CLI 都硬套成相同 `exec` 命令；抽象以 capability/run surface 為準。
5. 不在初版交付完整 PTCS Web UI；Web UI 需另以 RFC 定義。
6. 不在初版承諾跨機密資料邊界的自動同步。

## 4. 使用者與角色

| 角色 | 說明 |
| --- | --- |
| Human Operator | 透過 terminal 或 Web UI 發送任務、查看回覆與 artifacts。 |
| SessionActor | 管理單一 session 的 mailbox、history、run loop、compaction 與回訊。 |
| WorkerActor | 可被 session 派工的工作 actor，例如 repo worker、review worker、test worker。 |
| Host Operator | 部署與維護 `codex.fs.host`，設定 workspace、engine、storage、policy。 |
| Engine Adapter | 將 normalized run request 轉成特定 CLI 版本的 argv 與 artifact mapping。 |

## 5. 核心情境

### 5.1 Terminal-driven session

1. 使用者透過 `codex.fs.cli` 建立或連線到 session。
2. CLI client 將 prompt 傳給 `codex.fs.host`。
3. `SessionActor` 將 prompt append 到 durable history。
4. `SessionActor` 選擇 engine adapter，建立 run request。
5. `codex.fs.host` 執行 headless CLI。
6. run artifacts 保存後，host 回傳摘要與 artifact references。
7. 若 run 期間 mailbox 有新訊息，`SessionActor` 進入下一輪。

### 5.2 Actor-to-agent production workflow

1. 外部 actor 或 application 送出 typed task request。
2. `codex.fs.host` 負責 lease/workdir/policy/timeout。
3. `SessionActor` 執行 run loop。
4. 完成後將 structured result 發回 requester。

### 5.3 Multi-agent collaboration

1. 多個 session/worker 針對不同 project/worktree/task 平行工作。
2. actor 間透過 message protocol 交換 task/result/reference。
3. 每個 session 的 CLI run 只取得該 session 需要的 compacted history。
4. host 保存完整 artifact，chat 只傳摘要與 reference。

### 5.4 CLI engine replacement

1. 使用者設定 session 使用 `codex` 或 `agy` engine。
2. host probe installed CLI version 與 capability。
3. adapter registry 選擇對應 surface module。
4. normalized request 轉成 CLI-specific argv。

## 6. 功能需求

### R-001 Host runtime

`codex.fs.host` 必須可作為：

- NuGet package：可由既有 .NET host app reference。
- dotnet tool：可直接安裝、啟動、操作。

### R-002 CLI client

`codex.fs.cli` 必須能在 terminal 中：

- 建立 session。
- 送出 prompt。
- attach / tail session status。
- 查詢 run artifacts。
- drain pending mailbox。
- 執行 minimal admin operation，例如 list sessions、cancel run。

### R-003 Session run loop

`SessionActor` 必須支援：

- Idle / Running / Persisting / Replying / Compacting 狀態。
- run 期間收集 pending messages。
- run 完成後將 pending messages append 到下一輪 prompt。
- run artifacts 與 chat history 分離保存。

### R-004 Artifact capture

每次 run 至少保存：

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
- compacted history 必須保留未完成事項、決策、open blockers、artifact references。

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
- 在 public output 中顯示 secret value。

### R-009 Transport abstraction

chat/message transport 必須透過 abstraction：

- terminal client 為初版必備 transport。
- PTC/PTCS integration 為 optional package 或後續 RFC 擴充。
- transport message body 必須視為資料，不得當 shell command 執行。

## 7. 非功能需求

| 類別 | 需求 |
| --- | --- |
| Reliability | session state 與 artifact manifest 可重啟恢復。 |
| Traceability | 每次 run 可由 run id 找到 prompt、output、result 與 reply。 |
| Replaceability | CLI engine 可新增/替換，不改 SessionActor 核心流程。 |
| Security | secret redaction 與最小必要 artifact capture 是預設行為。 |
| Portability | 初期支援 Windows；設計不阻礙 Linux/macOS。 |
| Observability | host 提供 structured status/event，CLI 可查詢。 |
| Lightweight | core package 不依賴 Web UI、不強制特定 transport、不強制特定 engine。 |

## 8. Package 初始邊界

| Package / Tool | 用途 |
| --- | --- |
| `codex.fs` | Core contracts、domain models、policy vocabulary。 |
| `codex.fs.host` | Host runtime package 與 dotnet tool。 |
| `codex.fs.cli` | Terminal client dotnet tool。 |
| `codex.fs.engine.codex` | Codex CLI adapter。 |
| `codex.fs.engine.agy` | Agy CLI adapter。 |
| `codex.fs.akka` | Akka.NET actor integration。 |
| `codex.fs.ptc` | Optional PTC transport integration。 |

## 9. 驗收標準

初版文件與後續實作需能回答：

1. 如何建立 session 並送出一次 prompt。
2. 如何選擇 Codex/Agy engine。
3. 如何保存 CLI output，不再靠人工複製 terminal history。
4. 如何在 run 期間收集 pending messages。
5. 如何查詢 artifacts 與 final reply。
6. 如何新增一個 CLI surface version。
7. 如何避免 secret 被寫入 public artifact。

## 10. 後續文件

本文件之後應補：

- `doc/WBS.md`
- `doc/Test.md`
- PTCS/Web UI RFC
- PTC transport RFC
- Host deployment/runbook
