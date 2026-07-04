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
- HTTP listener/control endpoint is implemented by `CodexFs.Host.HostControl`; Swagger/OpenAPI route generation remains `DOC-003`.

## 2026-07-04 HOST-003 HTTP Control Endpoint

- `CodexFs.Host.HostControl.tryStartAsync` starts a real Kestrel HTTP listener from `HostConfig.ControlEndpoint.BindAddress` and `Port`.
- The stable health route is `GET /api/codexfs/host/health`; callers should use `HostControlContract.HealthUri`, which is derived from `control.advertiseUri`.
- Clustered profiles must keep `control.allowLoopbackOnly = false`; `HostConfig.validate` rejects localhost/127.* bind or advertise settings before HTTP start.
- `HostControlHealthResponse` is an option-free JSON DTO with camelCase output; it reports non-secret runtime state and redacted diagnostics only.
- Endpoint definitions carry success/failure examples and typed response metadata for future OpenAPI/Swagger generation; the endpoint remains control plane only and never replaces PTCS MessageFabric.

## 2026-07-04 DOC-003 OpenAPI / Swagger

- `codex.fs.host` maps OpenAPI JSON with `Microsoft.AspNetCore.OpenApi [10.0.9]` and `MapOpenApi("/openapi/{documentName}.json")`.
- Direct `Microsoft.OpenApi [2.7.5]` is required because the 2.0.x transitive version is affected by GHSA-v5pm-xwqc-g5wc.
- Swagger UI uses `Swashbuckle.AspNetCore.SwaggerUI [10.2.3]` and is gated by `apiDocs.exposeSwaggerUi`.
- Tests must verify `HostControlContract.OpenApiJsonUri` and `SwaggerUiUri` through an advertised non-loopback URI, not localhost.

## 2026-07-04 CLI-001 Argu Command / Help

- `codex.fs.cli` is a compiled executable project with `FAkka.Argu [10.1.301]`.
- `CodexFs.Cli.Cli` owns parser/help/examples; `Program` only handles entrypoint output and parse errors.
- Parser command groups are `session`, `run`, `host`, and `engine`.
- `CLI-001` deliberately does not call host APIs or MessageFabric; real send/attach/drain execution is deferred to `CLI-002` / `CLI-003`.

## 2026-07-04 CLI-002 Session Send Real Path

- Host route `POST /api/codexfs/session/{sessionId}/messages` accepts `SessionSendRequest` and writes to real PTCS MessageFabric.
- Session participant id is derived as `<ptcs.sessionParticipantPrefix>.<sessionId>`.
- CLI send uses `CodexFs.Cli.CliHttp.sendSessionMessageAsync` and the host advertised URI; it never writes MessageFabric directly.
- `TC-CLI-002` verifies delivery by reading the derived session participant inbox through the host status endpoint after HTTP 202.

## 2026-07-04 CLI-003 Session Inbox Read Commands

- Host routes `GET /api/codexfs/session/{sessionId}/status`, `POST /attach`, and `POST /drain` are host control-plane wrappers over real PTCS MessageFabric.
- `status` and `attach` return transcript JSON without acknowledging messages; `drain` acknowledges through `MessageFabricBinding.drainInboxAsync`.
- CLI `session status|attach|drain` uses `CodexFs.Cli.CliHttp` and the host advertised URI, not direct MessageFabric access.
- `SessionInboxResponse.Transcript` is early terminal output only; durable run artifacts and engine replies are still `E2E-002`.

## 2026-07-04 E2E-002 Single-Cycle Engine Reply

- `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` is the first real closed-loop helper: PTCS inbox -> prompt assembly -> Agy `--print` -> artifacts -> PTCS reply -> ack.
- It is bounded single-cycle code, not the durable sharded actor loop.
- Artifacts are written through `FileArtifactStore` and include prompt, PTCS batch JSONL, request JSON, rendered argv JSON, stdout/stderr, final markdown, result JSON and manifest JSON.
- Agy CLI must render options before `--print`; placing `--print` before `--print-timeout` causes Agy to treat the timeout flag as prompt content.
- `misc/verifyMessageToEngineReply.fsx` is the real E2E verifier and may consume the current user's installed CLI auth/session.

## 2026-07-04 REL-002 CLI Dotnet Tool

- `codex.fs.cli` installs as a local dotnet tool from `codex.fs.cli.0.1.0-alpha.1.nupkg` when local source also contains `codex.fs`, `codex.fs.ptcs`, and `codex.fs.host` packages.
- The tool command is `codex.fs.cli`.
- Root `--help`, `-h`, `help`, `/?`, and empty argv are handled by `CodexFs.Cli.Program.isRootHelp` before Argu command dispatch.
- `codex.fs.host` remains a referenceable library package; standalone host tool entrypoint is tracked separately as `REL-003`.

## 2026-07-04 REL-003 Host Dotnet Tool

- `codex.fs.host` remains the referenceable library package. Do not convert it to `PackAsTool=true`.
- `codex.fs.host.tool` is the thin dotnet tool package; its command name is `codex.fs.host`.
- Host tool commands use `FAkka.Argu` and reuse `HostConfig`, `HostRuntime`, and `HostControl`.
- `codex.fs.host start --run-seconds 0 ...` is the bounded verification path; it starts the real Kestrel host and stops immediately after successful startup.
- Non-dev host tool examples must use LAN/DNS advertise URIs; loopback is only for explicit dev profiles.

## 2026-07-04 UI-001 PTCS Web UI Extension RFC

- codex.fs Web UI should be implemented as a PTCS client extension consumer, not as a new UI/message fabric.
- Prompt/reply truth remains `CommSpaMessageFabric`; UI sends to session participants and renders MessageFabric replies/artifact references.
- Host control status/OpenAPI links may use codex.fs advertised control URI or PTCS same-origin allow-list JSON handler; never generic proxy or localhost-only clustered path.
- Relevant PTCS seams are `RegisterClientExtension`, `RegisterClientExtensionScriptAsset`, registered same-origin JSON POST handler, and page renderer/fallback contracts.
- `RFC-UI-0001` is an RFC/verifier-plan slice only; future UI implementation must add real browser + MessageFabric verifiers.

## 2026-07-04 E2E-003 Non-durable Multi-agent Collaboration

- `PTCS-003` durable handoff is not a blocker for first-slice multi-agent collaboration.
- Non-durable collaboration uses real `CommSpaMessageFabric` group/direct messages: one session-worker sends a group task, another session-worker receives it and replies direct.
- `MessageFabricBinding.upsertGroupAsync` supports groups with multiple participant ids; `tryUpsertConfiguredGroupAsync` remains the single-binding convenience wrapper.
- Durable task admission/retry/restart remains separate under `PTCS-003` / `OPS-002`.

## 2026-07-04 OPS-001 Process Orphan Recovery

- `ProcessRunner.ProcessLease` records pid, process name, observed start time and non-secret marker for codex.fs-owned processes.
- `recoverLeasedProcessAsync` kills only when pid/name/start time match the lease; it does not scan by process name.
- The test fixture uses a real `powershell.exe Start-Sleep` process and verifies recovery terminates it.
- Persisted lease storage and startup recovery sweep remain future host/session integration work.

## 2026-07-04 PTCS-003 Durable Task Handoff

- `CodexFs.Ptcs.DurableMessageFabricBinding` is the durable counterpart to `MessageFabricBinding`; it wraps PTCS `CommSpaDurableMessageFabric` and `DurableIngress` without creating a parallel mailbox.
- `createVolatileDurableFabric` is real PTCS ticketed admission using `CommSpaMessageFabric.createDurable`, but provider proof must fail closed for production sharded crash-durable readiness.
- `submitAgentTaskAsync` maps codex.fs task data to PTCS `MessageFabricAgentTaskEnvelope`; `MessageFabricAgentTaskAccepted` proves admission and inbox delivery, not worker execution or artifact persistence.
- `OPS-002` is now unblocked and should implement the codex.fs session persistence boundary: persist selected inbox cursor/run request before engine execution, persist artifact/reply evidence before ack, then verify recovery/ack ordering.

## 2026-07-04 OPS-002 Session Persistence Boundary

- `ArtifactKind.SessionBoundaryJson` is the ready-to-ack persistence artifact for bounded session cycles.
- `SessionEngineCycle.runSingleCycleAsync` writes `session-boundary.json` after PTCS reply delivery and before `MessageFabricBinding.ackInboxAsync`.
- `SingleCycleResult.PersistenceBoundaryPath` is the verifier/API handle for that boundary artifact.
- `misc/verifyMessageToEngineReply.fsx` now validates the boundary file, reply message id, selected ack cursor and empty inbox after ack on the real PTCS + Agy path.
- This is bounded single-cycle ordering evidence, not crash restart rehydration or sharded provider replay.
