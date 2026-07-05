# RFC-ACTOR-0002 Actor Runtime Artifact Provider

ID：`RFC-ACTOR-0002`  
狀態：Accepted  
日期：2026-07-05  
關聯 WBS：`ACTOR-003`  
關聯 Test：`T-ACTOR-003`  
前置：`RFC-ACTOR-0001`, `RFC-RUNTIME-0001`, `RFC-PERSIST-0001`, `WEBR-006`

## 背景

WEBR-006 已讓 PTCS classic shell 可以送出 `codex.fs.web.ai-intent.v1` invocation intent，但 WEBR-007 仍缺真正由 worker run 產生的 artifact/note refs。既有 `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` 已證明 real PTCS MessageFabric -> Agy -> artifact -> reply -> ack path，但它位於 `codex.fs.host`，使 WorkerActor 無法在不依賴 host runtime 的情況下呼叫同一條路徑。

這造成三個產品風險：

- actor/runtime alignment 只停在文件與 participant proof，沒有執行 loop；
- Web artifact renderer 可能被迫使用 fake refs；
- host route/helper 重新變成 prompt-loop owner，違反 `RFC-RUNTIME-0001`。

## 目標

1. 把目前 host-only single-cycle interpreter 提升到可由 PTCS worker actor 呼叫的 runtime artifact provider。
2. 保留 real MessageFabric、real installed engine、real file artifact store、real reply/ack ordering。
3. 讓 host wrapper 與 WorkerActor 共用同一個 interpreter，不複製 prompt assembly 或 artifact write ordering。
4. 以 `ACTOR-003` 解除 WEBR-007 對「runtime artifact provider」的模糊 blocker。

## 非目標

1. 不在本 slice 完成 production crash-durable sharded delivery。
2. 不新增平行 MessageFabric、ActorFabric、cursor registry 或 fake mailbox。
3. 不把 browser append intent 當成 engine execution result。
4. 不新增 standalone Web chat product path。

## 決策

### D1. Interpreter placement

新增 `CodexFs.Ptcs.RuntimeMessageFabricCycle`，放在 `codex.fs.ptcs`。

理由：

- 它需要 PTCS `CommSpaMessageFabric`；
- actor package目前實作在 `codex.fs.ptcs`；
- host 可 reference ptcs，但 ptcs 不應 reference host；
- 避免 WorkerActor 為了跑一輪 runtime 而依賴 `HostRuntime`。

### D2. Host wrapper

`CodexFs.Host.SessionEngineCycle` 保留 public host-facing `SingleCycleOptions` / `SingleCycleResult`，但只負責：

- 從 `HostRuntime.Config` 解析 engine、executable、artifact root、timeout、session participant prefix、reply participant；
- 確認 MessageFabric initialized；
- 呼叫 `RuntimeMessageFabricCycle.runSingleCycleAsync`；
- 映射 result。

它不再持有 prompt/message batch/request/rendered argv/stdout/stderr/final/manifest/boundary 的實際 sequencing。

### D3. Actor command

`CodexFs.Ptcs.ActorFabricBinding.CodexWorkerActor` 新增 typed command：

```fsharp
type RunRuntimeCycle =
    { SessionId: string
      SessionParticipantId: string option
      ReplyParticipantId: string option
      Engine: EngineKind option
      ExecutablePath: string option
      WorkingDirectory: string option
      ArtifactRoot: string
      Timeout: TimeSpan option
      SystemInstruction: string option
      AdditionalDirectories: string list }
```

Actor handler 必須先 register/refresh participant，再呼叫 PTCS runtime cycle。回傳 `RuntimeCycleCompleted`，內容至少包含 actor participant id、run id、manifest path、final path、reply message id、ack cursor 與 reply body。

### D4. Acceptance path

`T-ACTOR-003` 必須使用：

- real `CommSpaActorFabric`；
- real shared `CommSpaMessageFabric`；
- real user participant direct message；
- real installed `agy --print`；
- real artifact root under ignored `.codex.fs/`；
- real reply message visible in user inbox；
- real actor `Ask<RuntimeCycleCompleted>` boundary。

不允許 fake/mock mailbox 或只做 source-string smoke。

## 影響範圍

| Area | Change |
| --- | --- |
| `codex.fs.ptcs` | 新增 runtime cycle interpreter，擴充 WorkerActor message protocol。 |
| `codex.fs.host` | 將 single-cycle host helper 改成 wrapper。 |
| `tests/codex.fs.Tests` | 新增 actor-invoked runtime artifact provider test。 |
| `misc/verifyActorRuntimeArtifactProvider.fsx` | 新增 real-path verifier。 |
| WEBR-007 | blocker 從泛稱 runtime artifact provider 改為依賴 `ACTOR-003`。 |

## 驗收

Accepted implementation 需要：

1. `dotnet build .\codex.fs.slnx --no-restore` pass。
2. `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` pass 且包含 `TC-ACTOR-003 actor runtime artifact provider passed`。
3. `dotnet fsi --exec .\misc\verifyActorRuntimeArtifactProvider.fsx -- --no-restore` pass。
4. 測試產出的 manifest/final/boundary artifact 位於 ignored `.codex.fs/`，MessageFabric reply body 包含 manifest ref。
5. WBS/Test/SD/Verification/DevLog/KM 同步。

## 關聯文件

- `doc/RFC/RFC-ACTOR-0001.session-worker-actor-model.md`
- `doc/RFC/RFC-RUNTIME-0001.prompt-loop-package-boundary.md`
- `doc/RFC/RFC-PERSIST-0001.transcript-note-artifact-store.md`
- `doc/RFC/RFC-WEB-0002.ptcs-classic-webshell-rewrite.md`
- `doc/WBS.ACTOR-003.md`
- `doc/Test.ACTOR-003.md`
- `doc/WBS.WEBR-001.md`
- `doc/Test.WEBR-001.md`
