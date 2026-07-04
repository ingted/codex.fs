# MCP.KM

## 2026-07-04 SESS-002 PromptAssembly

- `CodexFs.PromptAssembly.assemble` is intentionally pure: PTCS polling, ack, persistence and engine process execution remain host/actor responsibilities.
- Prompt assembly input carries `SessionId`, `RunId`, participant id, engine selection, working directory, policy, and an ordered message batch.
- Message body rendering must use a markdown fence longer than any backtick run in the body, so user/agent content cannot prematurely close the fenced block.
- `PromptAssemblyResult.LastCursor` is derived from the last available message cursor and is the value a host can persist before later ack behavior.

## 2026-07-04 SESS-003 Compaction

- MVP compaction is deterministic and rule-based in `CodexFs.Compaction`; it does not consume Codex/Agy tokens and does not start engine processes.
- Retention-sensitive entries are default-preserved when kind is `Decision`, `Blocker`, `OpenItem`, `Run`, or `Artifact`, or when the entry carries PTCS message refs, run ids, or artifact refs.
- `MaxSummaryChars` is a soft budget for non-critical recent entries. Mandatory retained content may exceed the budget and sets `OverBudget = true`.
- Future LLM/engine compaction should be an adapter over the same `CompactionEntry` / `CompactionResult` contract, not a replacement for durable history or artifact storage.

## 2026-07-04 PTCS-001 Reference Range

- First supported PTCS dependency is `PulseTrade.Comm.Spa [0.2.5-beta71]`.
- `codex.fs.ptcs` owns the PTCS reference and compile-time boundary; `codex.fs` core remains independent from PTCS runtime packages.
- PTCS beta71 depends on `FAkka.Argu 10.1.301`, so `codex.fs` aligns its direct FAkka.Argu reference to exact `[10.1.301]`.
- Compile proof uses concrete PTCS types `PulseTrade.Comm.Spa.CommSpaMessageFabric` and `PulseTrade.Comm.Spa.CommSpaActorFabricOptions`.

## 2026-07-04 PTCS-002 MessageFabric Binding

- `CodexFs.Ptcs.MessageFabricBinding` is a thin wrapper over `PulseTrade.Comm.Spa.CommSpaMessageFabric`; it must not create a separate message store or cursor registry.
- The real in-process test profile uses `CommHub.createEmpty()` + `CommSpaMessageFabric.create`, which is PTCS package runtime, not a codex.fs fake mailbox.
- `DrainInboxAsync` both returns the current batch and lets PTCS ack the returned cursor.
- `MessageFabricBinding.batchToMessageRefs` maps each PTCS envelope to core `PtcsMessageRef` with `Cursor = Some message.MessageId`.
- `tryUpsertConfiguredGroupAsync` returns `None` when a binding has no `GroupId`; do not synthesize empty PTCS group views.

## 2026-07-04 HOST-001 Host Config Loading

- `CodexFs.HostConfig.loadFromMap` is the pure config boundary for the future host runtime; `HOST-002` should consume `HostConfig` instead of parsing settings again.
- Setting keys are case-insensitive but diagnostics store normalized lowercase keys.
- Diagnostics are redacted with core `Redaction.redactHighRisk`; redacted diagnostics may show `[REDACTED]`, but must not echo token-like raw values.
- `control.allowLoopbackOnly = false` rejects loopback bind/advertise config; clustered hosts must advertise a routable address rather than `localhost`.

## 2026-07-04 HOST-002 Minimal Host Runtime

- `codex.fs.host` now owns `CodexFs.Host.HostRuntime`; do not add host runtime startup logic to core `codex.fs`.
- `HostRuntime.startInProcessMessageFabric` uses `MessageFabricBinding.createInProcessFabric()` and therefore initializes real PTCS package runtime, not a fake mailbox.
- The in-process MessageFabric slice does not create an ActorSystem; production `CommSpaActorFabric` / sharded cluster binding must use LAN/routable bind/advertise addresses, never `127.0.0.1`.
- `HostRuntime.health` omits executable override values and reports only `EngineOverrideKeys`; use `healthSummary` for redacted text output.
- HTTP listener/control endpoint work remains `HOST-003`; Swagger/OpenAPI remains `DOC-003`.
