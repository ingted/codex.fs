# WBS Detail: WEBR-001 PTCS classic webshell rewrite

WBS ID：`WEBR-001`  
狀態：Done for RFC/reset slice  
Progress：100  
StartTime：2026-07-05 10:30 +08:00  
UpdatedAt：2026-07-05 15:20 +08:00
Previous：`WEB-001`, `ACTOR-001`, `PERSIST-001`  
SD：`SD §9`, `SD §14.1`, `SD §14.3`  
Test：`T-WEBR-001`

## Scope

本 detail 是「大型打掉重寫」的 side-by-side WBS。它不宣稱 implementation 已完成；它把現有不能作為產品 UI 的 standalone web/diagnostics 路線切掉，並把後續工作重新排回 PTCS classic chat shell + WebSharper Bundle + ActorFabric AI worker 主線。

## Reset Decision

- `codex.fs.host` control-only web is not product UI.
- Product Web must show PTCS classic `/chat`: tabs/nav shell, participant list, chat thread/session area and composer.
- `codex.fs.web` must be a WebSharper Bundle project modeled after `PulseTrade.Comm.Spa.Dynamic`.
- PTCS Host missing AI controls are codex.fs extension work; PTCS existing chat shell and fabric must be reused.
- AI execution belongs to ActorFabric/SessionActor/WorkerActor/runtime, not browser code or diagnostics route.

## Rewrite Leaf WBS

| ID | Work item | Previous | Progress | Status | Blocker | SD item | Test item | Verifier |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- |
| WEBR-002 | PTCS classic shell and Dynamic bundle baseline inventory | WEBR-001 | 100 | Done | None | SD §14.3 | T-WEBR-002 | `misc/verifyPtcsClassicShellInventory.fsx`; [inventory](WEBR-002.PTCS-classic-shell-inventory.md) |
| WEBR-003 | Create `codex.fs.web` WebSharper Bundle project | WEBR-002 | 100 | Done | None | SD §14.3 | T-WEBR-003 | `misc/verifyCodexFsWebBundle.fsx` |
| WEBR-004 | Implement `useAIChat(...)` CommHub registration/server extension | WEBR-003 | 100 | Done | None | SD §14.3 | T-WEBR-004 | `misc/verifyUseAIChatRegistration.fsx` |
| WEBR-005 | Add product `ptcs-webshell` host mode or PTCS Host composition path | WEBR-004 | 100 | Done | None | SD §9, §14.3 | T-WEBR-005 | `misc/verifyHostPtcsWebProfile.fsx` |
| RUNTIME-002 | Extract/complete reusable runtime prompt-loop modules | RUNTIME-001;PERSIST-001 | 100 | Done | None | SD §11.3, §12 | T-RUNTIME-002 | `misc/verifyRuntimeLoopExtraction.fsx` |
| ACTOR-002 | Implement PTCS ActorFabric Foreman/Worker proof | ACTOR-001;RUNTIME-002 | 100 | Done | None | SD §11.2, §14.3 | T-ACTOR-002 | `misc/verifyPtcsActorFabricForeman.fsx` |
| ACTOR-003 | WorkerActor invokes PTCS runtime artifact provider | ACTOR-002;RUNTIME-002;WEBR-006 | 100 | Done | None | SD §11.2, §11.3, §12, §14.3 | T-ACTOR-003 | `misc/verifyActorRuntimeArtifactProvider.fsx` |
| WEBR-006 | Add AI target/perspective/invocation controls in PTCS shell | WEBR-004;ACTOR-002 | 100 | Done | None | SD §14.2, §14.3 | T-WEBR-006 | `misc/verifyAiIntentControls.fsx`; Playwright PTCS webshell evidence |
| WEBR-007 | Render artifact/note refs in PTCS shell | WEBR-006;ACTOR-003;PERSIST-001 | 100 | Done | None | SD §12, §14.3 | T-WEBR-007 | `misc/verifyArtifactRefsInPtcsShell.fsx` |
| WEBR-008 | Remove/deprecate standalone web-chat product path | WEBR-005 | 100 | Done | None | SD §9, §14.3 | T-WEBR-008 | `misc/verifyNoStandaloneChatProductPath.fsx` |
| E2E-004 | Real PTCS classic browser AI chat E2E | WEBR-006;ACTOR-003;WEBR-007 | 0 | Planned | None | SD §14.3 | T-E2E-004 | `misc/verifyPtcsAiChatE2E.fsx` |

## Cut / Rewrite Notes

- `HOST-006` remains historical alpha evidence only.
- `HOST-007` is insufficient for product Web and is superseded by `WEBR-001` for implementation planning.
- `/diagnostics/session-send` remains allowed only as ops/debug control.
- Any implementation that does not render PTCS classic chat shell fails `WEBR-005` and `E2E-004`.

## Definition Of Done

`WEBR-001` is done when:

- `RFC-WEB-0002` exists and is accepted for reset slice.
- The rewrite WBS above exists with corresponding Test items.
- Requirement/SA/SD explicitly mark product Web as PTCS classic shell + codex.fs extension.
- DevLog/KM capture the reset.

Each implementation leaf item remains Planned until its real verifier exists and passes.

## WEBR-002 Closeout

UpdatedAt：2026-07-05 10:47 +08:00

- Source/API inventory is recorded in `doc/WEBR-002.PTCS-classic-shell-inventory.md`.
- Verifier `dotnet fsi --exec .\misc\verifyPtcsClassicShellInventory.fsx` passed on 2026-07-05 10:48 +08:00 and checks real PTCS package, PTCS Host, Dynamic bundle and codex.fs host cut-list source paths.
- `WEBR-003` is unblocked: create `codex.fs.web` as a WebSharper Bundle project with exact PTCS `[0.2.5-beta71]` reference and generated assets under `wwwroot/js`.

## WEBR-003 Closeout

UpdatedAt：2026-07-05 11:07 +08:00

- Added `src/codex.fs.web` as a WebSharper Bundle package with `PackageId=codex.fs.web`, `AssemblyName=CodexFs.Web`, exact `PulseTrade.Comm.Spa [0.2.5-beta71]` reference, `WebSharper.FSharp 10.1.5.674`, generated bundle output under `wwwroot/js`, and no hand-written JavaScript.
- Added server-side `CommHub.useAIChat(...)` baseline registration over PTCS extension APIs: script asset registration, fixed metadata JSON POST handler, extension manifest, and append-page shape template.
- Added `wsconfig.json` with `buildService=false` and `buildServiceLogging=false` because WebSharper build service can leave `websharper.log` locked/inaccessible; generated WebSharper logs are ignored from source control.
- Generated `wwwroot/js` is tracked as bundle package content, matching `PulseTrade.Comm.Spa.Dynamic`; the nupkg contains `content/wwwroot/js/CodexFs.Web.js` and `content/wwwroot/js/CodexFs.Web.head.js`.
- Verifier `dotnet fsi --exec .\misc\verifyCodexFsWebBundle.fsx` passed on 2026-07-05 11:07 +08:00 and ran a real `dotnet build` for `codex.fs.web`, producing 4 generated JavaScript files and verifying nupkg content assets.
- Regression evidence: `dotnet build .\codex.fs.slnx` passed, and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed.
- `WEBR-004` is unblocked: next step is to verify `useAIChat(...)` registration through a real `CommHub` extension manifest/asset/handler path.

## WEBR-004 Closeout

UpdatedAt：2026-07-05 11:40 +08:00

- Added compiled test coverage in `tests/codex.fs.Tests/Program.fs` for `CommHub.useAIChat()` over a real PTCS `CommHub`.
- Covered extension manifest id/display name/metadata, generated WebSharper head/main script assets, runtime script asset, fixed metadata JSON POST handler and append-page shape template.
- Added `misc/verifyUseAIChatRegistration.fsx` using `FAkka.Argu` plus `ParseLine.fsx`; the verifier builds `tests/codex.fs.Tests` and runs the full test runner.
- Verifier `dotnet fsi --exec .\misc\verifyUseAIChatRegistration.fsx` passed on 2026-07-05 11:37 +08:00.
- `WEBR-005` is unblocked: next step is product host composition that serves PTCS classic `/chat` with this extension registered.

## WEBR-005 Closeout

UpdatedAt：2026-07-05 12:35 +08:00

- Added explicit `HostConfig.WebShell` settings: `web.profile`, `web.bindAddress`, `web.port`, `web.advertiseUri`, `web.allowLoopbackOnly`, `web.actorFabric`.
- Added `CodexFs.Host.HostWebShell` product composition path: creates one PTCS `CommHub`, registers `useAIChat()`, creates `CommSpaMessageFabric` from the same hub, and starts PTCS classic `/chat`.
- Updated `codex.fs.host.tool start` so `web.profile=ptcs-webshell` starts the product PTCS webshell; default `control-only` behavior remains unchanged.
- Verifier `dotnet fsi --exec .\misc\verifyHostPtcsWebProfile.fsx` passed on 2026-07-05 12:32 +08:00. It builds/runs `codex.fs.Tests`, binds to the LAN IP, verifies `/chat` PTCS manifest plus `codex-fs-ai-chat`, fetches generated script asset and verifies host tool bounded start.
- `WEBR-008` is unblocked for removing/deprecating the legacy standalone `/chat` product path claim. `WEBR-006` still waits for ActorFabric-visible participants from `ACTOR-002`.

## WEBR-008 Closeout

UpdatedAt：2026-07-05 13:05 +08:00

- Updated control-only root, legacy `/chat` guard and diagnostics page text to explicitly point product browser chat at `web.profile=ptcs-webshell`.
- Added regression assertions that control-only `/chat` has no composer/form/PTCS extension manifest and diagnostics has no PTCS manifest.
- Added `misc/verifyNoStandaloneChatProductPath.fsx` using `FAkka.Argu` plus `ParseLine.fsx`; the verifier builds tests and runs the full test runner.
- Verifier `dotnet fsi --exec .\misc\verifyNoStandaloneChatProductPath.fsx` passed on 2026-07-05 13:03 +08:00.
- At WEBR-008 closeout time, remaining product work was `RUNTIME-002`, `ACTOR-002`, `WEBR-006`, `WEBR-007` and `E2E-004`.

## RUNTIME-002 Closeout

UpdatedAt：2026-07-05 13:59 +08:00

- Added `src/codex.fs/RuntimePromptLoop.fs` as the reusable runtime prompt-loop planning module. It owns deterministic prompt planning, consumed-message JSONL shaping, Agy print execution plan, normalized request/rendered argv JSON, process outcome mapping, redacted reply intent and ready-to-ack boundary JSON.
- Refactored `src/codex.fs.host/SessionEngineCycle.fs` so host keeps PTCS registration/poll/send/ack and artifact write sequencing, while prompt/request/argv/reply/boundary planning comes from `RuntimePromptLoop`.
- Added compiled test coverage in `tests/codex.fs.Tests/Program.fs` for `TC-RUNTIME-002 runtime prompt-loop plan passed`.
- Added verifier `misc/verifyRuntimeLoopExtraction.fsx` using `FAkka.Argu` plus `ParseLine.fsx`. It checks source contracts, builds/runs `codex.fs.Tests`, then delegates to `misc/verifyMessageToEngineReply.fsx` for the real MessageFabric -> Agy -> artifact -> reply path.
- Verifier `dotnet fsi --exec .\misc\verifyRuntimeLoopExtraction.fsx` passed on 2026-07-05 13:59 +08:00 and wrote ignored real-path artifacts under `G:\codex.fs\src\codex.fs\.codex.fs\runtime002-artifacts`.
- `ACTOR-002` is unblocked for a PTCS ActorFabric Foreman/Worker proof. Durable sharded persistence remains outside this slice.

## ACTOR-002 Closeout

UpdatedAt：2026-07-05 14:25 +08:00

- Added `src/codex.fs.ptcs/ActorFabricBinding.fs` as the PTCS ActorFabric-backed codex.fs worker shell boundary. It defines `WorkerParticipantSpec`, `EnsureParticipantRegistered`, `SpawnWorkerParticipant`, `WorkerParticipantRegistered`, `WorkerParticipantSpawned`, `CodexWorkerActor`, `props` and `spawnWorker`.
- The proof starts a real `CommSpaActorFabric` with LAN `ClusterHost`, spawns a Foreman actor on the PTCS-owned `ActorSystem`, has the Foreman spawn/register a child worker actor, and registers both as PTCS `agent` participants through the shared `CommSpaMessageFabric`.
- Added compiled test coverage in `tests/codex.fs.Tests/Program.fs` for `TC-ACTOR-002 PTCS ActorFabric Foreman/Worker participants passed`.
- Added verifier `misc/verifyPtcsActorFabricForeman.fsx` using `FAkka.Argu` plus `ParseLine.fsx`; it checks source contracts, builds tests and runs the full test runner.
- Verifier `dotnet fsi --exec .\misc\verifyPtcsActorFabricForeman.fsx -- --no-restore` passed on 2026-07-05 14:25 +08:00.
- This slice proves participant visibility over real PTCS ActorFabric/MessageFabric. It does not yet turn WorkerActor into a durable sharded entity or execute the runtime loop from actor delivery handlers; that remains for later hardening/E2E.
- `WEBR-006` is unblocked for AI target/perspective/invocation controls because Foreman/Worker `agent` participants can now be produced and listed.

## WEBR-006 Closeout

UpdatedAt：2026-07-05 15:05 +08:00

- Added WEBR-006 AI intent metadata under `CodexFs.Web.Server.AIChatExtensionOptions.defaultMetadataJson`: intent schema, defaults, target/perspective modes, engine options and invocation options.
- Added `CodexFs.Web.Client.AIChatClient` PTCS append-input renderer for shape `codexfs-ai-chat`. It renders Foreman/Worker/Public/Group target controls, perspective controls, Agy/Codex engine selection, model, reasoning, invocation, approval, prompt and send controls.
- The renderer emits `codex.fs.web.ai-intent.v1` JSON into PTCS append values and keeps CLI argv out of the browser. Default target is `agent.codexfs.foreman`, default engine is `agy`, and default invocation is `exec`.
- Added explicit `web.pcslRoot` host config and health reporting; host webshell now creates the local hub with `CommHub.createEmptyWithPcslRoot` when the setting is present.
- Added PTCS package `build/**` asset copy for host, host.tool and tests so product `/chat` can serve `/build/PulseTrade.Comm.Spa.js` from package outputs.
- Browser evidence used real PTCS webshell on LAN IP `http://10.28.112.93:18488/page/webr006-ai8`; desktop/mobile screenshots, geometry, console and network request bodies are under `G:\codex.fs\log\20260705\webr006-host8-*`.
- Verification passed: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyAiIntentControls.fsx -- --no-restore`.
- Known upstream/package caveats recorded in SD/DEVOP: PTCS `Server` static initialization can touch default AppContext `pcsl` before explicit hub injection, and `/sync/ws` returned 503 in this host composition while HTTP fallback APIs remained functional.
- `WEBR-007` remains blocked until runtime artifact/note refs are produced by the worker execution path; `E2E-004` still waits for artifact refs plus actor-invoked runtime execution.

## ACTOR-003 Start

UpdatedAt：2026-07-05 15:18 +08:00

- Added `RFC-ACTOR-0002` to remove the host-only runtime-cycle mismatch. The concrete interpreter will move into `CodexFs.Ptcs.RuntimeMessageFabricCycle`; `CodexFs.Host.SessionEngineCycle` becomes a wrapper over that shared path.
- `CodexWorkerActor` will gain a `RunRuntimeCycle` command and return `RuntimeCycleCompleted` after real MessageFabric -> Agy -> artifact -> reply -> ack work.
- `WEBR-007` blocker is now the concrete `ACTOR-003` implementation rather than a vague runtime artifact provider.

## ACTOR-003 Closeout

UpdatedAt：2026-07-05 15:20 +08:00

- Added `CodexFs.Ptcs.RuntimeMessageFabricCycle` as the shared PTCS runtime cycle adapter. It registers participants, polls MessageFabric, calls `RuntimePromptLoop`, writes prompt/batch/request/rendered argv/stdout/stderr/final/result/manifest artifacts, sends a PTCS reply, writes `session-boundary.json`, then acks the cursor.
- Refactored `CodexFs.Host.SessionEngineCycle` into a host config wrapper over `RuntimeMessageFabricCycle`; host no longer owns prompt-loop sequencing.
- Extended `CodexFs.Ptcs.ActorFabricBinding.CodexWorkerActor` with `RunRuntimeCycle` and `RuntimeCycleCompleted`.
- Added compiled coverage in `tests/codex.fs.Tests/Program.fs` for `TC-ACTOR-003 actor runtime artifact provider passed`.
- Added verifier `misc/verifyActorRuntimeArtifactProvider.fsx` using `FAkka.Argu` and `ParseLine.fsx`.
- Verification passed: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyActorRuntimeArtifactProvider.fsx -- --no-restore`.
- Verifier evidence:
  - manifest: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\manifest.json`
  - boundary: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\session-boundary.json`
  - final: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\final.md`
- `WEBR-007` is unblocked for rendering the real worker reply/run refs in the PTCS shell. Production sharded crash-durable delivery remains future hardening.

## WEBR-007 Closeout

UpdatedAt：2026-07-05 14:18 +08:00

- Added `RFC-WEB-0003` for PTCS artifact ref rendering.
- Runtime now writes `note.md` as `RunNoteMarkdown`; manifest, reply body, `session-boundary.json` and `RuntimeCycleResult` include the note ref.
- `HostWebShell.registeredHub` registers default Foreman participant `agent.codexfs.foreman` so PTCS `/chat` has a first-use包工頭 target.
- `codex.fs.web` registers an artifact reply renderer and a minimal bridge for PTCS `pre.message-body` fallback nodes.
- Added verifier `misc/verifyArtifactRefsInPtcsShell.fsx`; it builds, runs compiled tests, starts real PTCS webshell, sends real actor artifact refs through `/chat/api/send`, and verifies the rendered card with Playwright.
- Evidence:
  - manifest: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\manifest.json`
  - final: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\final.md`
  - note: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\note.md`
  - screenshot: `G:\codex.fs\src\codex.fs\.playwright-mcp\webr007\webr007-artifact-refs.png`
- `E2E-004` is unblocked for full browser prompt -> Foreman actor -> runtime -> artifact reply loop.
