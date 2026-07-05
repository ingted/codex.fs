# WBS Detail: WEBR-001 PTCS classic webshell rewrite

WBS ID：`WEBR-001`  
狀態：Done for RFC/reset slice  
Progress：100  
StartTime：2026-07-05 10:30 +08:00  
UpdatedAt：2026-07-05 14:25 +08:00
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
| WEBR-006 | Add AI target/perspective/invocation controls in PTCS shell | WEBR-004;ACTOR-002 | 0 | Planned | None | SD §14.2, §14.3 | T-WEBR-006 | `misc/verifyAiIntentControls.fsx` |
| WEBR-007 | Render artifact/note refs in PTCS shell | WEBR-006;PERSIST-001 | 0 | Planned | runtime artifact provider | SD §12, §14.3 | T-WEBR-007 | `misc/verifyArtifactRefsInPtcsShell.fsx` |
| WEBR-008 | Remove/deprecate standalone web-chat product path | WEBR-005 | 100 | Done | None | SD §9, §14.3 | T-WEBR-008 | `misc/verifyNoStandaloneChatProductPath.fsx` |
| E2E-004 | Real PTCS classic browser AI chat E2E | WEBR-006;WEBR-007;ACTOR-002 | 0 | Planned | all implementation slices | SD §14.3 | T-E2E-004 | `misc/verifyPtcsAiChatE2E.fsx` |

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
