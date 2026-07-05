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
- Superseded by CLI-005: the package id is `codex.fs.cli`, but the installed command is `codex.fs`.
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

## 2026-07-05 CLI-004 Terminal Self-Use

- `codex.fs host status --host <advertiseUri>` is now an executed command and calls host `GET /api/codexfs/host/health`.
- `--prompt @file` is resolved in the CLI process before HTTP submission; the host receives prompt text and never reads caller filesystem paths.
- Manual self-use evidence used LAN URI `http://10.28.112.93:10481` and real PTCS MessageFabric: host status running, session send accepted, pendingCount 1 before drain, drained, and pendingCount 0 after drain.
- Evidence summary path: `G:\codex.fs\src\codex.fs\.codex.fs\cli004-selfuse\summary.json`.

## 2026-07-05 HOST-004/DOC-004/REL-004 Host Usability Handoff

- Host health is not a sufficient product gate. User-facing handoff must verify the advertised root URL, visible docs UI, OpenAPI JSON, and the actual installed tool path.
- `GET /` is the codex.fs host operator landing page. It links to health, OpenAPI JSON, Swagger UI, and the CLI `host status` command.
- OpenAPI metadata now applies endpoint tag/summary/description from `HostControl.endpointDefinitions`, so Swagger UI shows `Host Control` with readable operation summaries instead of handler-generated names.
- Global tool handoff uses `codex.fs.cli` and `codex.fs.host.tool` installed under `C:\Users\Administrator\.dotnet\tools`; `dotnet run --project` is only a bounded dev/internal verification path.
- Final alpha.2 evidence paths:
  - host run summary: `G:\codex.fs\.codex.fs\host-run\20260705004149-alpha2\summary.json`
  - browser summary/screenshots: `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\summary.json`, `root.png`, `docs.png`
  - packages: `G:\codex.fs\bin\host-usability-packs-20260705004149-alpha2`

## 2026-07-05 CLI-005 / HOST-005 / UI-002 Usability Correction

- `codex.fs.cli` remains the dotnet tool package id, but the installed command is `codex.fs`. Handoff must verify `C:\Users\Administrator\.dotnet\tools\codex.fs.exe --help`, `codex.fs --help`, and `codex.fs host status --host <advertiseUri>`.
- Package family version `0.1.0-alpha.3` carries the command-name correction.
- `HostRuntime.startWithMessageFabric` lets a PTCS Host or peer cluster node pass caller-owned `CommSpaMessageFabric`; this is the seam required for PTCS Web and worker participants to share the same chat/participant truth.
- Standalone `codex.fs.host` uses package-owned in-process MessageFabric. It is valid for CLI/API/docs verification but does not make workers appear in an already running PTCS Web process.
- Current PTCS Host profile facts: `http://127.0.0.1:82/chat` is the local PTCS.Login chat path; `https://my-ai.co.in:81/chat` is the public GitHub OAuth path and may redirect to GitHub by design.
- Browser evidence for local82 login/send is under `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82-send`.

## 2026-07-05 CLI-006/CLI-007 Explicit CLI Alias And Worker Routing

- CLI-005 alpha.3 command-name decision is superseded. Current alpha.4 contract:
  - package `codex.fs.cli` installs explicit PoC command `codex.fs.cli`;
  - package `codex.fs.tool` installs short alias command `codex.fs`;
  - both commands run through `CodexFs.Cli.ProgramCore.run`.
- `session send` default target is the derived session worker / foreman participant `<ptcs.sessionParticipantPrefix>.<sessionId>`.
- `session send --worker-id <participantId>` overrides the direct target and treats the supplied value as the exact PTCS worker participant id.
- `SessionSendResponse.TargetParticipantId` exposes the effective target for verifiers and operator evidence.

## 2026-07-05 HOST-006/CLI-008 Standalone Chat PoC And CLI Transport Errors

- `codex.fs.host` alpha.5 exposes `/chat` as a standalone operator PoC form. It is for early usability against the host's current MessageFabric, not the production PTCS participant-perspective Web UI.
- `POST /chat` accepts `sessionId`, `workerId`, and `prompt`; blank `workerId` targets the derived SessionWorker / foreman participant, matching CLI default routing.
- Production PTCS Web integration remains caller-owned PTCS MessageFabric via PTCS Host or peer cluster node; do not treat standalone package-owned `/chat` as canonical shared UI truth.
- `CodexFs.Cli.CliHttp` catches HTTP transport failures and returns `CliHttpResult` with `StatusCode = 0` and readable guidance. CLI should not print raw `.NET HttpRequestException` stack traces for connection refused endpoints.
- Operator guidance must distinguish process PID from HTTP port; e.g. previous PID `14724` was not the host port, while the advertised URI was `http://10.28.112.93:10481`.

## 2026-07-05 HOST-007/CLI-009 PTCS Hub Chat Alignment

- RFC-HOST-0002 supersedes the alpha.5 `/chat` PoC. Current standalone `GET /chat` is a guard page that points to PTCS WebSharper chat, not a prompt composer.
- Browser chat truth belongs to PTCS Web over caller-owned `CommSpaMessageFabric` / `CommSpaActorFabric`; codex.fs workers/session-workers must appear as PTCS participants instead of using a separate package-owned UI.
- Standalone prompt testing moved to `GET/POST /diagnostics/session-send`; blank diagnostics `sessionId` defaults to `foreman`.
- CLI first-use send no longer requires `--session`. `SessionSendOptions.SessionId = None` posts to `/api/codexfs/foreman/messages` and targets `<ptcs.sessionParticipantPrefix>.foreman`.
- Host health JSON exposes `diagnosticsSessionSendUri`; `chatUri` is no longer the current contract field.

## 2026-07-05 PRODUCT-001 Product Responsibility Reset

- `PTCS Host` and `codex.fs.host` are different products: PTCS Host owns WebSharper chat/hub/auth profile; `codex.fs.host` owns codex.fs composition/control/docs/deployment.
- Prompt assembly is not a host HTTP responsibility. It belongs to runtime/session worker behavior together with local compact, headless CLI invocation, stdio capture, notes/artifacts and recovery boundary.
- `SessionActor` is a specialized `WorkerActor` / Foreman participant. It may spawn/register other worker participants, but all communication must remain on PTCS MessageFabric/ActorFabric.
- `codex.fs.cli` should be an interactive terminal participant client with Foreman default target, participant switching and engine/model/reasoning options.
- Future Web UI should be a PTCS WebSharper extension/bundle such as `useAIChat(...)`, not standalone host `/chat`.

## 2026-07-05 RUNTIME-001 Prompt-loop Package Boundary

- Runtime owns orchestration and side-effect ordering; adapters own transport/UI/delivery. Host route handlers should validate DTOs and call runtime/PTCS services, not assemble prompts.
- Preferred runtime shape is deterministic `decideCycle` plus side-effect `interpretCycleAsync` over explicit ports/effects.
- Required ordering: persist consumed cursor/message ids, persist prompt/request, invoke engine, persist artifacts/result/manifest, write note, emit reply, persist ready-to-ack boundary, then ack.
- `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` is bounded real E2E evidence and a migration candidate, not the final durable sharded runtime loop.

## 2026-07-05 ACTOR-001 Session / Worker Actor Model

- `WorkerActor` is the common capability; `SessionActor` is a specialized WorkerActor / Foreman participant and may call runtime itself or spawn workers.
- Default Foreman participant is `<ptcs.sessionParticipantPrefix>.foreman`, e.g. `agent.codexfs.foreman`; child workers must register as PTCS `agent` participants.
- Actor shell uses PTCS ActorFabric and calls runtime; MessageFabric remains participant/direct/public/group chat truth.
- Delivery confirm and MessageFabric ack occur after runtime ready-to-ack evidence and reply/result reference. Volatile provider proof must not be treated as production sharded durability.

## 2026-07-05 CLI-010 Interactive Participant CLI Client

- `codex.fs.cli` is a terminal participant client, not a prompt-loop or headless-engine owner. It sends user intent through host/PTCS APIs and renders MessageFabric replies/artifact references.
- Current sender baseline is `user.codexfs.cli`; future explicit identity uses `--participant-id <user.*>` or a local profile. First-use target remains Foreman `<ptcs.sessionParticipantPrefix>.foreman`.
- Target switching vocabulary covers Foreman, explicit session, exact participant/worker, public channel and MessageFabric group. Perspective switching is authorized read/render only and must not silently forge `agent.*` sender ids.
- CLI may collect engine/model/reasoning/invocation options, but runtime/actor validates policy and engine adapters render versioned argv.
- Future verifier `misc/verifyCliParticipantChat.fsx` must use installed `codex.fs.cli` / `codex.fs` against real host/PTCS fabric; fake mailbox smoke is not acceptance.

## 2026-07-05 PERSIST-001 Transcript / Note / Artifact Store

- Minimum run evidence includes PTCS refs/cursor, prompt, normalized request, rendered argv metadata, stdout/stderr, final/events/result, manifest, redacted run note and ready-to-ack boundary.
- Raw run artifacts are private by default and should live under configured artifact roots, often gitignored `.codex.fs/` for local verification. Public repo `notes/` is not the default artifact root.
- MessageFabric replies, CLI/Web views and public exports should use redacted summaries plus manifest/note refs, not raw transcript bodies.
- `note.md` feeds human browsing and `CompactionEntry` values; local compact must preserve message ids, run ids and artifact refs and never overwrite raw artifacts.
- Future verifier `misc/verifyTranscriptStore.fsx` must prove real runtime/host writes prompt/stdout/stderr/final/manifest/note/session-boundary and blocks high-risk public export.

## 2026-07-05 WEB-001 PTCS AI Chat Bundle

- `codex.fs.web` is a PTCS WebSharper extension/bundle such as `useAIChat(...)`; it must plug into PTCS Host/CommHub using `RegisterClientExtension`, script asset registration and fixed JSON POST handlers.
- Browser prompt/reply truth remains PTCS `CommSpaMessageFabric`; standalone `codex.fs.host` `/chat`, browser-local stores, fake mailboxes and mock UI smoke are not product acceptance.
- Web target vocabulary matches CLI/actor vocabulary: Foreman default, exact worker participant, public channel and group id. Perspective switching is authorized read/render only and must not forge `agent.*` sender identity.
- Engine/model/reasoning/invocation controls emit normalized intent metadata; runtime/actor validates policy and engine adapter capabilities before rendering versioned Codex/Agy argv.
- Web rendering should show redacted final summary, run id, manifest ref and note ref. Raw prompt/stdout/stderr stay governed by the persistence policy.

## 2026-07-05 WEBR-001 PTCS Classic Webshell Rewrite

- `RFC-WEB-0002` resets Web implementation: product Web must be PTCS classic `/chat` shell plus codex.fs WebSharper Bundle, not `codex.fs.host` control-only routes.
- `PulseTrade.Comm.Spa.Dynamic` is the baseline package shape for codex.fs Web: WebSharper Bundle, generated `wwwroot/js`, exact PTCS package reference and server/client split.
- `codex.fs.host` must distinguish `control-only` from product `ptcs-webshell`; only `ptcs-webshell` may claim browser chat usability.
- Existing `GET /chat` guard and `/diagnostics/session-send` are cut from product acceptance and may remain only as legacy/control diagnostics.
- AI behavior belongs to PTCS ActorFabric SessionActor/WorkerActor plus runtime prompt loop; browser sends intent through PTCS MessageFabric and renders refs.

## 2026-07-05 WEBR-002 PTCS Classic Shell Inventory

- PTCS classic shell lives in `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`: `/chat` is served by `Server.fs`, and `Client.fs` mounts `.page.chat-grid`, `chat-participant`, `chat-work`, `thread-list`, `chat-composer`, `chat-draft`, and `chat-send`.
- `/chat/api/agents` lists `CommHub.ListParticipants(Some "agent", Some true)` plus `channel.public`; codex.fs workers must register as PTCS `agent` participants to be visible.
- Browser chat sends `chat-send` through `/sync/ws` first and uses `/chat/api/send` as a fallback; acceptance must not bypass MessageFabric with a standalone textarea route.
- PTCS extension seam is `CommHub.RegisterClientExtension`, `RegisterClientExtensionScriptAsset`, `RegisterClientExtensionJsonPostHandler`, `ClientExtensionRegistration`, and `ClientExtensionScriptAsset`.
- `PulseTrade.Comm.Spa.Dynamic` is the direct pattern for `codex.fs.web`: `WebSharperProject=Bundle`, generated `wwwroot/js`, `PulseTrade.Comm.Spa [0.2.5-beta71]`, and server `useDynamicSdui(...)`.

## 2026-07-05 WEBR-003 codex.fs.web Bundle Scaffold

- `src/codex.fs.web` now exists as the PTCS WebSharper Bundle package for codex.fs Web; it references `PulseTrade.Comm.Spa [0.2.5-beta71]` and `WebSharper.FSharp 10.1.5.674`.
- `CodexFs.Web.Server.CommHubExtensions.useAIChat(...)` is the first server registration seam: generated script asset registration, fixed metadata JSON handler, extension manifest, and append-page shape template.
- WebSharper on Windows can leave `websharper.log` locked through `wsfscservice`; `wsconfig.json` sets `buildService=false` and `buildServiceLogging=false`, and generated `websharper.log` is ignored from Git.
- `src/codex.fs.web/wwwroot/js/` is tracked package content, matching `PulseTrade.Comm.Spa.Dynamic`; nupkg assets live under `content/wwwroot/js`.
- `misc/verifyCodexFsWebBundle.fsx` is the real verifier for this slice; it uses FAkka.Argu, runs `dotnet build`, checks generated bundle files, checks nupkg content assets and forbids hand-written JavaScript outside generated/build output.

## 2026-07-05 WEBR-004 useAIChat Registration Contract

- `tests/codex.fs.Tests` now references `src/codex.fs.web`; the product test runner exercises `CommHub.useAIChat()` against a real PTCS `CommHub`.
- The verified contract covers extension manifest `codex-fs-ai-chat`, generated WebSharper script assets, runtime script asset, metadata JSON handler `/client-extensions/codexfs-ai-chat/metadata`, and append-page template `codexfs-ai-chat`.
- `misc/verifyUseAIChatRegistration.fsx` is the WBS verifier; it uses `FAkka.Argu` and `ParseLine.fsx`, then runs real `dotnet build` and `dotnet run` for `codex.fs.Tests`.

## 2026-07-05 WEBR-005 PTCS Webshell Host Profile

- `HostConfig.WebShell` makes product Web explicit: default `web.profile=control-only`, product path `web.profile=ptcs-webshell` with separate bind/advertise URI and loopback policy.
- `CodexFs.Host.HostWebShell` starts PTCS classic `/chat` by creating one `CommHub`, registering `useAIChat()`, creating `CommSpaMessageFabric` from that same hub, and calling PTCS `Server.start`.
- `codex.fs.host start` now routes to PTCS webshell only when `web.profile=ptcs-webshell`; otherwise it remains the ASP.NET control host. This prevents the legacy guard `/chat` from being mistaken for product Web.
- `misc/verifyHostPtcsWebProfile.fsx` verifies the real path through `codex.fs.Tests`: LAN bind, `/chat` manifest, `codex-fs-ai-chat`, generated script fetch, `/healthz`, non-guard HTML and host tool bounded start.

## 2026-07-05 WEBR-008 No Standalone Product Chat

- Control-only root, legacy `/chat` and diagnostics pages now point product browser chat to `web.profile=ptcs-webshell`.
- Regression tests assert the control-only `/chat` guard has no composer, no form and no PTCS extension manifest; diagnostics has no PTCS extension manifest.
- `misc/verifyNoStandaloneChatProductPath.fsx` is the WBS verifier and runs the full `codex.fs.Tests` path.

## 2026-07-05 RUNTIME-002 Runtime Prompt Loop Extraction

- `CodexFs.RuntimePromptLoop` is the current reusable prompt-loop planning boundary. It is pure/deterministic for prompt planning, request JSON, rendered argv JSON, reply intent and ready-to-ack boundary text.
- `SessionEngineCycle.runSingleCycleAsync` now acts as a host adapter/interpreter over PTCS MessageFabric, artifact writes, `ProcessRunner`, reply send and ack. New ActorFabric shells should call `RuntimePromptLoop` instead of copying host prompt/request logic.
- `RuntimeReadyToAckBoundary.SelectedCursor` is `string option`, matching PTCS `MessageFabricCursor`; adapters should preserve `None` instead of converting missing cursors to empty strings.
- `misc/verifyRuntimeLoopExtraction.fsx` is the RUNTIME-002 verifier. It uses FAkka.Argu, checks source contracts, builds/runs `codex.fs.Tests`, then delegates to `misc/verifyMessageToEngineReply.fsx` for the real MessageFabric -> Agy -> artifact -> reply path.
- The ignored verifier artifact root is `G:\codex.fs\src\codex.fs\.codex.fs\runtime002-artifacts`. Do not commit raw prompt/stdout/stderr artifacts from that path.

## 2026-07-05 ACTOR-002 PTCS ActorFabric Foreman/Worker Proof

- `CodexFs.Ptcs.ActorFabricBinding` is the current actor shell seam: `WorkerParticipantSpec`, `EnsureParticipantRegistered`, `SpawnWorkerParticipant`, `WorkerParticipantRegistered`, `WorkerParticipantSpawned`, `CodexWorkerActor`, `props` and `spawnWorker`.
- `CodexWorkerActor` registers participants through the existing `MessageFabricBinding.registerParticipantAsync`; Foreman and child Worker become PTCS `agent` participants with codex.fs labels.
- The proof uses a real `CommSpaActorFabric` with LAN `ClusterHost` and the PTCS-owned `ActorSystem`; it is not an in-memory fake mailbox or standalone HTTP endpoint.
- `misc/verifyPtcsActorFabricForeman.fsx` is the ACTOR-002 verifier. It checks source contracts, builds/runs `codex.fs.Tests`, spawns Foreman/Worker and verifies both through `CommSpaMessageFabric.ListParticipantsAsync`.
- This slice unblocks WEBR-006 controls, but does not yet provide production durable sharded delivery or actor-invoked runtime prompt loop execution.

## 2026-07-05 WEBR-006 PTCS AI Intent Controls

- `CodexFs.Web.Client.AIChatClient` registers the PTCS append-input renderer `codexfs-ai-chat-append-input` for shape `codexfs-ai-chat`; controls are generated from F# WebSharper and use only minimal `JS.Inline` for the PTCS global renderer hook.
- Browser controls emit `codex.fs.web.ai-intent.v1` JSON into PTCS append values. Default target is Foreman `agent.codexfs.foreman`, default engine is `agy`, reasoning `high`, invocation `exec`, approval `never`.
- PTCS append-page `add-key` expects `keyJson` as a JSON literal, for example `"agent.codexfs.foreman"`. Sending plain `agent.codexfs.foreman` produces HTTP 400 because the server parses it as invalid JSON.
- Product webshell deployments need `PulseTrade.Comm.Spa [0.2.5-beta71]` package `build/**` assets copied into host outputs; otherwise `/build/PulseTrade.Comm.Spa.js` is 404 even when `/chat` HTML is served.
- Use `web.pcslRoot=<dedicated service data path>` for product PTCS webshell state. PTCS `Server` static initialization can still touch default AppContext `pcsl` before explicit hub injection, so corrupted build-output `pcsl` can fail startup before codex.fs config runs.
- WEBR-006 browser evidence shows `/sync/ws` returning 503 in the current host composition while HTTP fallback APIs succeed for register-page, add-key and append. Production should fix WebSocket routing before high-volume usage.
- Responsive renderer rule: controls use a white, scrollable, auto-fit grid. Desktop evidence has all controls visible; mobile evidence has no horizontal overflow and the controls panel scrolls within PTCS' constrained append region.

## 2026-07-05 ACTOR-003 WorkerActor Runtime Artifact Provider

- `CodexFs.Ptcs.RuntimeMessageFabricCycle` is now the shared concrete PTCS runtime adapter for bounded single-cycle work. It owns real MessageFabric poll, `RuntimePromptLoop`, Agy process execution, artifact writes, MessageFabric reply, `session-boundary.json`, and ack ordering.
- `CodexFs.Host.SessionEngineCycle` is a wrapper over `RuntimeMessageFabricCycle`; do not reintroduce prompt assembly, `ProcessRunner.runAsync`, manifest write, reply or ack sequencing there.
- `CodexWorkerActor` handles `RunRuntimeCycle` and replies with `RuntimeCycleCompleted`. The actor registers its PTCS participant first, then calls the shared runtime adapter against the same `CommSpaMessageFabric`.
- A WorkerActor self-reply can appear in the actor participant inbox after ack; tests should verify the consumed user prompt id is not replayed, not require the inbox to be globally empty.
- `misc/verifyActorRuntimeArtifactProvider.fsx` is the real ACTOR-003 verifier. It uses FAkka.Argu, checks source/docs, builds/runs `codex.fs.Tests`, and verifies manifest/boundary/final artifact paths under ignored `.codex.fs/actor003-artifacts`.
- This slice unblocks WEBR-007 artifact/note ref rendering. It does not satisfy production sharded crash-durable delivery replay.

## 2026-07-05 WEBR-007 PTCS Artifact Ref Rendering

- `RunNoteMarkdown` is the current artifact kind for the human-readable run note. Runtime adapters should write it as `note.md`, include it in `manifest.json`, store it in `session-boundary.json` as `runNotePath`, and include `note=<path>` in the PTCS reply intent.
- `CodexFs.RuntimePromptLoop.RuntimeReadyToAckBoundary.RunNotePath` is the contract that links the durable ack boundary to the note artifact. Tests should assert the note file exists and the boundary JSON preserves the exact path.
- `CodexFs.Web.Client.AIChatClient` owns the PTCS reply renderer for codex.fs artifact replies. It registers `codexfs-artifact-reply` rendering and bridges existing PTCS `pre.message-body` content so classic `/chat` can display artifact cards without replacing the PTCS hub.
- Stable artifact-card selectors for UI verification are `codexfs-artifact-reply`, `codexfs-artifact-run`, `codexfs-artifact-outcome`, `codexfs-artifact-manifest`, `codexfs-artifact-final`, `codexfs-artifact-note`, and `codexfs-artifact-summary`.
- `codex.fs.host` should register default Foreman `agent.codexfs.foreman` into MessageFabric before serving `/chat`; this gives first-use users a visible top-level participant without asking them to invent a session id.
- `misc/verifyArtifactRefsInPtcsShell.fsx` is the WEBR-007 real-path verifier. It builds the solution, runs compiled tests to produce real ACTOR-003 artifacts, starts the host on LAN IP, sends the artifact reply through PTCS `/chat/api/send`, opens `/chat` with Playwright, selects Foreman and validates artifact-card selectors plus screenshot.
- Browser evidence for the completed slice is `G:\codex.fs\src\codex.fs\.playwright-mcp\webr007\webr007-artifact-refs.png`. Runtime artifact evidence remains under ignored `.codex.fs/actor003-artifacts` and must not be committed.
