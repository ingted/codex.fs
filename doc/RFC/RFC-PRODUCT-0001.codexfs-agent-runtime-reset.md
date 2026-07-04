# RFC-PRODUCT-0001 codex.fs Agent Runtime Reset

ID：`RFC-PRODUCT-0001`  
狀態：Accepted  
日期：2026-07-05  
關聯：`doc/RFC_Project_Planing.md`, `doc/Requirement.md`, `doc/SA.md`, `doc/SD.md`, `doc/WBS.md`, `doc/Test.md`

## 背景

User clarified that the intended product is closer to an "all-purpose foreman" than a single all-purpose engineer. A `SessionActor` is a specialized `WorkerActor`: it owns a session/foreman participant, may spawn or coordinate other worker participants, and drives iterative work by consuming PTCS chat/mailbox messages, invoking a headless engine, saving transcripts/artifacts/notes, and replying through PTCS.

The alpha implementation proved useful pieces, but the responsibility boundary drifted:

- standalone `codex.fs.host` acquired diagnostics and PoC chat behavior that can be mistaken for the product chat room;
- prompt assembly and bounded engine-cycle logic were described as host behavior, even though they should be reusable by actors, CLI tests and embedded PTCS hosts;
- `PTCS Host` and `codex.fs.host` are different products: PTCS Host owns WebSharper chat/hub/auth profiles, while `codex.fs.host` owns codex.fs runtime composition, control, docs and deployment;
- `codex.fs.cli` must be an interactive participant client, not only an HTTP diagnostics command set.

This RFC resets the product boundary before more code is added.

## 目標

1. Define project/package responsibility so future implementation does not keep growing `codex.fs.host` as a god module.
2. Move prompt assembly, run loop, stdio capture, notes, local compaction and recovery boundaries into a reusable runtime layer.
3. Define `SessionActor` as a specialized `WorkerActor` over PTCS `CommSpaActorFabric` / `CommSpaMessageFabric`.
4. Keep PTCS WebSharper chat as the browser product UI and implement codex.fs Web as a PTCS extension/bundle, not a second chat fabric.
5. Make `codex.fs.cli` a terminal participant client with default Foreman target, participant switching and engine invocation options.
6. Preserve the lightweight wrapper concept: users can reference packages, install dotnet tools, and swap CLI engines such as Codex CLI or Agy CLI.

## 非目標

1. 不重寫 PTCS MessageFabric、ActorFabric、WebSharper chat room 或 auth/profile host。
2. 不把 standalone `codex.fs.host` 的 `/diagnostics/session-send` 當成 production chat UI。
3. 不把 prompt assembly、history splice、local compact 或 headless invocation policy 放進 HTTP route handler。
4. 不引入 OpenAI API-only execution path；ChatGPT subscription / Codex CLI auth and Agy CLI remain first-class assumptions。
5. 不把 fake/mock smoke 當成交付驗收。

## 決策

### D1. Product project roles

| Project / package | Role |
| --- | --- |
| `codex.fs` | Core domain, engine/artifact/compaction contracts and pure behavior vocabulary. |
| `codex.fs.runtime` | Future reusable runtime loop: prompt/history assembly, engine cycle, stdio capture, notes, compact, recovery boundary. Until split, these modules must stay isolated from HTTP route concerns. |
| `codex.fs.actor` | Future PTCS ActorFabric adapter: `WorkerActor`, specialized `SessionActor`, spawn/register/route protocol and durable delivery. |
| `codex.fs.ptcs` | Thin adapter over PTCS MessageFabric/ActorFabric/DurableIngress; no parallel fabric. |
| `codex.fs.host` | Referenceable composition/control package: starts runtime with caller-owned or package-owned PTCS fabric, exposes health/control/OpenAPI/Swagger, but does not own prompt semantics. |
| `codex.fs.host.tool` | Thin dotnet tool wrapper around `codex.fs.host`. |
| `codex.fs.cli` / `codex.fs.tool` | Terminal participant client and short alias over the same command surface. |
| `codex.fs.web` | Future PTCS WebSharper bundle/extension, e.g. `useAIChat(...)`, for participant-perspective chat and controls. |
| `codex.fs.persistence` | Future transcript/note/artifact store provider boundary if the file store outgrows core runtime. |

### D2. Prompt assembly belongs to runtime/actor behavior

Prompt assembly is a session runtime concern, not a host HTTP concern.

```text
PTCS MessageFabric inbox
  -> runtime collects batch and history refs
  -> runtime assembles prompt / local compact
  -> engine adapter invokes headless CLI
  -> runtime persists stdio/artifacts/notes
  -> runtime emits reply intent
  -> actor or host adapter sends through MessageFabric
```

`codex.fs.host` may call this runtime and expose control endpoints, but route handlers must not become the canonical prompt/history implementation.

### D3. SessionActor is a specialized WorkerActor

`WorkerActor` is the common actor capability: register as PTCS participant, receive MessageFabric or actor-routed work, call runtime, persist results, reply through MessageFabric, and optionally coordinate child workers.

`SessionActor` is a specialized `WorkerActor` with a stable session/foreman identity. It is the default top-level target for human prompts. It may spawn workers, join groups and collect other worker replies. Spawned workers must register to PTCS as participants so humans and other workers can communicate with them through the same fabric.

Stateful and external-request actor chains must use `Akka.Delivery` or `Akka.Cluster.Sharding.Delivery` according to the selected sharding profile. Cross-node actor communication must use the PTCS ActorFabric cluster profile with LAN/DNS-reachable bind/advertise addresses; `127.0.0.1` is dev-only.

### D4. PTCS Web is the browser chat surface

The product browser UI is PTCS WebSharper chat plus codex.fs-specific extension controls. It must support:

- public channel broadcast to worker participants;
- direct or group conversation with a selected participant;
- perspective switching, e.g. human operator can view from the Foreman participant's perspective when authorized;
- engine/model/reasoning effort/invocation controls that PTCS Host does not currently provide;
- artifact/run reference rendering without dumping raw transcripts into chat body.

Standalone `codex.fs.host` remains diagnostics/control/docs.

### D5. CLI is the terminal participant client

`codex.fs.cli` should feel like a normal non-headless coding-agent CLI, but its conversation target is a PTCS participant. The default target is Foreman/SessionActor unless the user selects another participant or worker id. CLI must support interactive message loop, participant switching, model/reasoning/engine options and artifact/run queries. HTTP transport errors must be readable and non-crashing.

### D6. Transcript, note and artifact persistence

The runtime must save the prompt, CLI invocation metadata, stdout, stderr, final reply, event stream when available, manifest and note summary for each run. This replaces manual terminal-history copying. The public repo must not receive raw sensitive transcripts by default; note/artifact locations and secret scanning rules must be explicit.

## 影響範圍

- Stock docs must distinguish `PTCS Host` from `codex.fs.host`.
- `SD.md` must treat host as composition/control and runtime/actor as prompt-loop owners.
- WBS/Test must add reset and follow-up leaf work for runtime, actor, CLI, Web and persistence.
- Existing alpha rows remain historical evidence, but new implementation should follow this product reset.

## 驗收

This RFC slice is accepted when:

1. `doc/Requirement.md`, `doc/SA.md`, `doc/SD.md`, `doc/WBS.md`, `doc/Test.md`, `doc/DevLog.md` and `MCP.KM.md` reference the reset.
2. `WBS.md` contains follow-up leaf items for runtime split, actor model, interactive CLI, PTCS Web bundle and transcript/note persistence.
3. `Test.md` contains matching planned tests/verifiers and does not mark future runtime/actor/Web work as passed.
4. No code path is claimed as production-ready only from fake/mock smoke.

## 關聯文件

- `doc/RFC_Project_Planing.md`
- `doc/RFC/RFC-HOST-0002.ptcs-hub-chat-alignment.md`
- `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md`
- `doc/WBS.PRODUCT-001.md`
