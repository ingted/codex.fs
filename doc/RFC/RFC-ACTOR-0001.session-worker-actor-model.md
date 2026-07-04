# RFC-ACTOR-0001 Session / Worker Actor Model

ID：`RFC-ACTOR-0001`  
狀態：Accepted  
日期：2026-07-05  
關聯 WBS：`ACTOR-001`  
關聯 Test：`T-ACTOR-001`  
前置：`RFC-PRODUCT-0001`, `RFC-RUNTIME-0001`, `PTCS-003`

## 背景

`codex.fs` needs a multi-agent actor model where each session is handled by a Foreman/SessionActor and additional workers can be spawned or coordinated. User/agent communication must remain PTCS-based:

- PTCS `CommSpaMessageFabric` is the canonical participant/direct/public/group mailbox.
- PTCS `CommSpaActorFabric` owns ActorSystem attachment, Cluster Sharding region/proxy and fabric health.
- Runtime owns prompt-loop orchestration; actors call runtime instead of duplicating prompt assembly.
- Durable task admission/result identity follows PTCS DurableIngress / MessageFabric durable task vocabulary.

PTCS source docs that constrain this RFC:

- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0001.shared-sharded-message-fabric-contract.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0002.external-actor-system-attachment.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0003.shared-durable-ingress-fabric-contract.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0004.task-centric-durable-result-vault.zh-Hant.md`

## 目標

1. Define `WorkerActor` as the common codex.fs actor capability.
2. Define `SessionActor` as a specialized `WorkerActor` and default Foreman participant.
3. Define entity id / participant id mapping without requiring users to know internal session ids.
4. Define how actors use PTCS MessageFabric scopes and runtime prompt-loop contract.
5. Define delivery/ack/retry boundaries for volatile and durable profiles.
6. Define future verifier requirements over real PTCS ActorFabric/MessageFabric.

## 非目標

1. 不在本 RFC 實作 actor code。
2. 不新增 `codex.fs.akka` 或平行 ActorFabric。
3. 不把 actor delivery 當成 MessageFabric replacement；MessageFabric 仍是 human/agent chat truth。
4. 不宣稱 volatile durable admission 已滿足 production sharded crash-durable provider。
5. 不定義 Web UI bundle；那是 `WEB-001`。

## 決策

### D1. Package boundary

Future `codex.fs.actor` is a PTCS ActorFabric adapter package:

```text
PTCS CommSpaActorFabric region/proxy
  -> codex.fs.actor WorkerActor / SessionActor shell
  -> codex.fs.runtime prompt-loop
  -> codex.fs.ptcs MessageFabric/DurableMessageFabric binding
```

It may depend on `codex.fs.runtime`, `codex.fs.ptcs` and PTCS packages. It must not own a separate message bus, inbox cursor registry or durable ingress protocol.

### D2. Actor roles

| Actor | Role |
| --- | --- |
| `WorkerActor` | Common capability: register/refresh PTCS participant, consume assigned work, call runtime, persist artifacts/notes, send reply/result reference, coordinate children if policy allows. |
| `SessionActor` | Specialized `WorkerActor` with stable Foreman/session identity, default target for human prompts, session history/compaction policy and worker spawn coordination. |
| Child worker | Worker spawned by a SessionActor for subtask execution. It must register as a PTCS participant so humans and other workers can message it through MessageFabric. |

`SessionActor` is not a passive router. It may call runtime itself, and it may also delegate work to child workers.

### D3. Entity id and participant id

Default mapping:

| Concept | Default |
| --- | --- |
| Foreman entity id | `foreman` |
| Foreman participant id | `<ptcs.sessionParticipantPrefix>.foreman`, e.g. `agent.codexfs.foreman` |
| Session entity id | stable sanitized session id |
| Session participant id | `<ptcs.sessionParticipantPrefix>.<sessionEntityId>` |
| Child worker participant id | configurable prefix, default `<ptcs.sessionParticipantPrefix>.worker.<sessionEntityId>.<workerId>` |

Rules:

- CLI/Web first-use prompt without session id targets Foreman.
- Explicit session id targets that session's SessionActor participant.
- Explicit worker id targets the exact worker participant id.
- Entity id mapping must be deterministic and safe for Akka Cluster Sharding entity ids.
- Participant display name is not identity; MessageFabric participant id is identity.

### D4. MessageFabric scope policy

Each actor registers/refreshes a PTCS participant with `Kind = Some "agent"` where supported.

Default consume scopes:

| Actor | Direct | Public | Group |
| --- | --- | --- | --- |
| Foreman SessionActor | yes | yes, policy-controlled | yes, session/control groups |
| SessionActor | yes | optional by policy | yes, session group |
| Child worker | yes | optional by policy | yes, assigned task/session groups |

Message bodies are prompt/data, never shell commands. Public broadcast can be used for "公頻" prompts, but runtime policy decides whether a worker consumes, ignores or summarizes them.

### D5. Actor command model

Preferred command vocabulary:

```fsharp
module CodexFs.Actor

type WorkerActorCommand =
    | EnsureParticipantRegistered
    | PollInbox
    | InboxBatchReceived of PtcsMessageRef list
    | RunRuntimeCycle of Runtime.RuntimeCycleInput
    | RuntimeCycleCompleted of Runtime.RuntimeCycleResult
    | SpawnWorker of WorkerSpawnRequest
    | WorkerMessageReceived of PtcsMessageRef
    | CancelRun of RunId
    | StopWorker

type WorkerActorEvent =
    | ParticipantRegistered of participantId: string
    | RuntimeCycleStarted of RunId
    | RuntimeCycleFinished of RunId
    | WorkerSpawned of participantId: string
    | ReplySent of messageId: string
    | InboxAcked of cursor: string option
```

Concrete Akka message names may differ, but they must preserve the semantics above.

### D6. Delivery and ack ordering

Stateful or external-request actor chains must use Akka.Delivery or Akka.Cluster.Sharding.Delivery according to profile.

Minimum durable ordering:

1. Receive actor/durable task envelope.
2. Register/refresh participant if needed.
3. Poll/wait MessageFabric inbox or accept direct task payload.
4. Call runtime and persist prompt/request boundary.
5. Runtime persists artifacts, note and ready-to-ack boundary.
6. Send MessageFabric reply/result reference.
7. Confirm actor delivery only after the runtime/result boundary is durable for the selected profile.
8. Ack MessageFabric cursor only after reply/result evidence exists.

Volatile/local profiles may be useful for development, but production-ready sharded delivery requires a provider proof that satisfies PTCS sharded durable delivery requirements. Volatile provider proof must fail closed.

### D7. ActorSystem and cluster profile

ActorSystem ownership follows PTCS external attachment rules:

- Caller-owned cluster ActorSystem must merge PTCS `CommSpaActorFabric.requiredConfig` before `ActorSystem.Create`.
- `validateActorSystem` / `ensureActorSystem` must fail fast before attach/start.
- Use `attachRegionToSystem` for region hosts and `attachProxyToSystem` for proxy/client nodes.
- `Stop()` of caller-owned attached fabric must not terminate caller ActorSystem.
- Clustered profiles must use LAN/DNS-routable bind/canonical advertise addresses. `127.0.0.1` is dev-only.

### D8. Task/result identity

Actor task metadata should carry:

- operation id / task id / idempotency key when available;
- session id / entity id / participant id;
- run id once runtime creates a run;
- PTCS message refs and durable ticket/result handles;
- artifact manifest reference.

Completed result lookup must use task/result identity where durable profile supports it; it must not re-run backend work because a transport caller retried.

## 驗收

This RFC slice is accepted when:

1. SD records actor role/entity/participant/delivery/runtime boundaries.
2. WBS row `ACTOR-001` is complete as RFC slice and does not claim actor code exists.
3. Test row `T-ACTOR-001` is `Pass` for docs and keeps `misc/verifyActorFabricSessionWorker.fsx` as future implementation gate.
4. DevLog/KM capture the actor model.

Future implementation acceptance requires:

- real PTCS ActorFabric region/proxy startup through caller-owned ActorSystem or package-owned profile;
- actor participant registration visible through PTCS MessageFabric/participant list;
- Foreman first-use prompt consumes MessageFabric inbox and calls runtime;
- child worker spawn registers a participant and can receive direct/group messages;
- delivery confirmation and MessageFabric ack happen after runtime ready-to-ack boundary;
- no fake/mock mailbox or standalone HTTP chat substitute is used as acceptance.

## 關聯文件

- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/RFC/RFC-RUNTIME-0001.prompt-loop-package-boundary.md`
- `doc/WBS.ACTOR-001.md`
- `doc/WBS.md`
- `doc/Test.md`
- `doc/SD.md`
