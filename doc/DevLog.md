# DevLog

## 2026-07-04 20:04 +08:00 SESS-002 prompt assembly

- Scope: implemented `CodexFs.PromptAssembly` as a pure markdown prompt assembler for one session run.
- Behavior: renders run metadata, optional system instruction, history/summary references, additional context, MessageFabric message metadata and ordered message bodies.
- Safety: prompt body rendering uses dynamic markdown fences based on the longest backtick run, and optional per-message truncation.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-SESS-002 prompt batch assembly passed`.
- Traceability: WBS `SESS-002` and Test `T-SESS-002` updated to `Done` / `Pass`.

## 2026-07-04 20:12 +08:00 SESS-003 local compaction

- Scope: implemented `CodexFs.Compaction` as deterministic rule-based local compaction for persisted session history entries.
- Decision: resolved `SD-TBD-003` for MVP; compaction does not call selected engine or a dedicated LLM adapter. Future LLM-backed compaction can reuse the same core input/output contract.
- Behavior: retains decisions, blockers, open items, run entries, artifact entries, and any entry carrying PTCS message refs, run ids, or artifact refs; recent non-critical context is retained by policy.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-SESS-002 prompt batch assembly passed` and `TC-SESS-003 compact preserves blockers passed`.
- Traceability: WBS `SESS-003` and Test `T-SESS-003` updated to `Done` / `Pass`; SD §11.1 and §17 updated.

## 2026-07-04 20:44 +08:00 PTCS-001 reference range

- Scope: added `codex.fs.ptcs` as the thin PTCS integration package and kept `codex.fs` core PTCS-independent.
- Decision: resolved `SD-TBD-004`; first supported PTCS package is exact `PulseTrade.Comm.Spa [0.2.5-beta71]`.
- Dependency alignment: updated core `FAkka.Argu` reference to exact `[10.1.301]` to align with PTCS beta71 dependency graph.
- Tests: `dotnet restore .\codex.fs.slnx`, `dotnet build .\codex.fs.slnx --no-restore`, and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; test output included `TC-PTCS-001 PTCS restore/reference passed`.
- Traceability: WBS `PTCS-001` and Test `T-PTCS-001` updated to `Done` / `Pass`; downstream blockers for `PTCS-002` and `HOST-001` cleared.

## 2026-07-04 20:45 +08:00 PTCS-002 MessageFabric binding

- Scope: implemented `CodexFs.Ptcs.MessageFabricBinding` as a thin wrapper over concrete PTCS `CommSpaMessageFabric`.
- Behavior: covers participant registration, direct/group send, poll, bounded wait, ack, drain, group upsert, and conversion from PTCS envelopes/batches to core `PtcsMessageRef`.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-PTCS-002 MessageFabric binding passed`.
- Traceability: WBS `PTCS-002`, detail `doc/WBS.PTCS-002.md`, and Test `T-PTCS-002` updated to `Done` / `Pass`; `HOST-002` blocker reduced to `HOST-001`.

## 2026-07-04 21:03 +08:00 HOST-001 host config loading

- Scope: implemented `CodexFs.HostConfig` with defaults, case-insensitive `loadFromMap`, validation issues, redacted diagnostics, and effective host/PTCS/API-docs/compaction settings.
- Behavior: config validation rejects production/cluster profile loopback bind or advertised URI when `control.allowLoopbackOnly = false`; development defaults still allow loopback.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-HOST-001 config parse/redaction passed`.
- Traceability: WBS `HOST-001` and Test `T-HOST-001` updated to `Done` / `Pass`; `HOST-002` and `REL-002` blockers from `HOST-001` cleared.

## 2026-07-04 21:14 +08:00 HOST-002 minimal host runtime

- Scope: added `codex.fs.host` project and `CodexFs.Host.HostRuntime` minimal runtime state/health boundary over core `HostConfig` and PTCS `MessageFabricBinding`.
- Behavior: `startInProcessMessageFabric` initializes a real in-process PTCS `CommSpaMessageFabric` without creating an ActorSystem; production ActorSystem/sharded cluster binding remains a routable-address PTCS ActorFabric concern.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-HOST-002 host runtime/health passed`.
- Traceability: WBS `HOST-002` and Test `T-HOST-002` updated to `Done` / `Pass`; `HOST-003` and `OPS-001` blockers from `HOST-002` cleared.

## 2026-07-04 21:27 +08:00 HOST-003 host control endpoint

- Scope: added `CodexFs.Host.HostControl` with a real Kestrel HTTP control endpoint and typed JSON health DTOs.
- Behavior: `GET /api/codexfs/host/health` returns non-secret host health over `control.advertiseUri`; clustered profiles remain non-loopback and HTTP remains control plane only, not a MessageFabric or ActorSystem transport.
- Tests: `dotnet restore .\codex.fs.slnx` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-HOST-003 endpoint contract passed` after a real HTTP 200 through a non-loopback advertised URI.
- Traceability: WBS `HOST-003` and Test `T-HOST-003` updated to `Done` / `Pass`; blockers for `DOC-003` and `CLI-001` cleared.

## 2026-07-04 21:39 +08:00 DOC-003 OpenAPI / Swagger

- Scope: added OpenAPI JSON and Swagger UI route mapping to `CodexFs.Host.HostControl`.
- Dependency: added `Microsoft.AspNetCore.OpenApi [10.0.9]`, `Microsoft.OpenApi [2.7.5]`, and `Swashbuckle.AspNetCore.SwaggerUI [10.2.3]`; the direct `Microsoft.OpenApi` reference avoids GHSA-v5pm-xwqc-g5wc affected transitive versions.
- Tests: `dotnet restore .\codex.fs.slnx` passed without NU1903; `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-DOC-003 OpenAPI available passed`.
- Traceability: WBS `DOC-003` and Test `T-DOC-003` updated to `Done` / `Pass`.

## 2026-07-04 21:48 +08:00 CLI-001 Argu command/help

- Scope: added `codex.fs.cli` executable project and compiled `CodexFs.Cli.Cli` FAkka.Argu command surface.
- Behavior: parser covers session/run/host/engine command groups, renders generated help plus stable examples, and returns Argu parse errors for invalid args; runtime host calls are deferred to `CLI-002` / `CLI-003`.
- Tests: `dotnet restore .\codex.fs.slnx`, `dotnet build .\codex.fs.slnx --no-restore`, and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; output included `TC-CLI-001 Argu parser/help passed`.
- Traceability: WBS `CLI-001` and Test `T-CLI-001` updated to `Done` / `Pass`; blocker for `CLI-002` cleared.

## 2026-07-04 21:56 +08:00 CLI-002 session send real path

- Scope: added host `POST /api/codexfs/session/{sessionId}/messages` and CLI HTTP send client.
- Behavior: CLI sends prompt to host advertised URI; host registers sender/session participants and appends a direct PTCS MessageFabric message to the derived session participant inbox.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-CLI-002 CLI send through MessageFabric passed`.
- Traceability: WBS `CLI-002` and Test `T-CLI-002` updated to `Done` / `Pass`; blockers for `CLI-003` and `E2E-002` cleared.

## 2026-07-04 22:09 +08:00 CLI-003 attach/drain/status

- Scope: added host session inbox status/attach/drain endpoints and CLI dispatch/client calls for `session status|attach|drain`.
- Behavior: status/attach read the derived session participant inbox through real PTCS MessageFabric without ack; drain returns the transcript and acknowledges the inbox cursor. Host/CLI paths use the advertised non-loopback URI.
- SD correction: clarified that `HostRuntime` local/in-process wording does not mean a `127.0.0.1` ActorSystem contract; future PTCS ActorFabric/sharded cluster binding must advertise LAN/DNS-reachable addresses.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-CLI-003 attach/drain/status passed`.
- Traceability: WBS `CLI-003`, detail `doc/WBS.CLI-003.md`, and Test `T-CLI-003` updated to `Done` / `Pass`.

## 2026-07-04 22:27 +08:00 E2E-002 message to engine reply

- Scope: added `CodexFs.Host.SessionEngineCycle` bounded single-cycle runner and `misc/verifyMessageToEngineReply.fsx`.
- Behavior: runner polls a real PTCS session inbox, assembles prompt, invokes real Agy `--print`, persists prompt/batch/request/rendered-argv/stdout/stderr/final/result/manifest artifacts, sends a PTCS direct reply with artifact reference, then acknowledges the cursor.
- Fix: Agy argv rendering must put flags before `--print`; otherwise Agy treats `--print-timeout` as prompt content.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyMessageToEngineReply.fsx` passed. The verifier printed `TC-E2E-002 message to engine reply passed`.
- Traceability: WBS `E2E-002`, detail `doc/WBS.E2E-002.md`, Test `T-E2E-002`, and `doc/Verification.md` updated to `Done` / `Pass`; durable/crash recovery remains `PTCS-003` / `OPS-002`.

## 2026-07-04 22:41 +08:00 REL-002 CLI dotnet tool package

- Scope: validated `codex.fs.cli` as a local dotnet tool package from generated nupkg.
- Fix: root `--help` initially exited 1 because Argu expects `help`; added `CodexFs.Cli.Program.isRootHelp` so `--help`, `-h`, `help`, `/?`, and empty argv return help with exit code 0.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed after the fix.
- Packaging: packed `codex.fs`, `codex.fs.ptcs`, `codex.fs.host`, and `codex.fs.cli` to `G:\codex.fs\bin\rel002-packs-202607042243`; installed `codex.fs.cli` to `G:\codex.fs\bin\rel002-tool-202607042243`; `codex.fs.cli.exe --help` returned exit code 0.
- Traceability: WBS `REL-002`, detail `doc/WBS.REL-002.md`, and Test `T-REL-002` updated to `Done` / `Pass`; standalone `codex.fs.host` dotnet tool remains `REL-003`.

## 2026-07-04 22:49 +08:00 OPS-001 process orphan recovery

- Scope: added codex.fs-owned process lease and orphan recovery helper in `ProcessRunner`.
- Behavior: recovery only terminates a process when pid, process name and start time match the saved lease; it does not kill by process name scan.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; output included `TC-OPS-001 orphan process recovery passed`.
- Traceability: WBS `OPS-001`, detail `doc/WBS.OPS-001.md`, and Test `T-OPS-001` updated to `Done` / `Pass`; durable recovery/ack ordering remains `OPS-002`.

## 2026-07-04 23:06 +08:00 REL-003 host dotnet tool

- Scope: added `src/codex.fs.host.tool` as a thin standalone dotnet tool wrapper.
- Decision: keep `codex.fs.host` as a referenceable host library package; publish/install the tool through package id `codex.fs.host.tool`, command name `codex.fs.host`.
- Behavior: host tool uses `FAkka.Argu`, supports `status` and bounded/unbounded `start`, and delegates startup to existing `HostControl.tryStartAsync`.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; output included `TC-REL-003 host tool start/status passed`.
- Packaging: packed `codex.fs.host.tool.0.1.0-alpha.1.nupkg` to `G:\codex.fs\bin\rel003-packs-202607042303`; installed to `G:\codex.fs\bin\rel003-host-tool-202607042303`; `codex.fs.host.exe --help`, `status`, and `start --run-seconds 0` passed with LAN advertised URIs.
- Traceability: WBS `REL-003`, detail `doc/WBS.REL-003.md`, and Test `T-REL-003` updated to `Done` / `Pass`.

## 2026-07-04 23:23 +08:00 UI-001 PTCS Web UI extension RFC

- Scope: added `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md` and `doc/WBS.UI-001.md`.
- Decision: codex.fs Web UI should be a PTCS extension consumer over `CommSpaMessageFabric`, client extension manifest/script/JSON handler seams, and host advertised control URI; it must not introduce a new UI fabric or parallel chat store.
- Inputs: read PTCS Requirement/SA/SD plus RFC-PTC-SPA-0006, RFC-PTC-SPA-0008, RFC-PTC-SPA-0010, and RFC-SPA-UPSTREAM-0001/0002/0003 from `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc`.
- Tests: `T-UI-001` marks the RFC/verifier-plan slice as pass and explicitly defers real browser implementation/verifier to a future UI implementation WBS.
- Traceability: WBS `UI-001`, detail `doc/WBS.UI-001.md`, RFC `RFC-UI-0001`, and Test `T-UI-001` updated to `Done` / `Pass`.

## 2026-07-04 23:38 +08:00 E2E-003 multi-agent MessageFabric group

- Scope: completed the non-durable multi-agent collaboration slice before entering durability refactor.
- Fix: `E2E-003` had been incorrectly treated as blocked by `PTCS-003`; this slice proves the group/direct collaboration path can run over real PTCS MessageFabric without durable ingress.
- Behavior: two session-worker participants join a PTCS MessageFabric group, alpha sends a group task, beta receives it and replies direct to alpha, and alpha acknowledges the reply cursor.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings / 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and output included `TC-E2E-003 multi-agent MessageFabric group passed`.
- Traceability: WBS `E2E-003`, detail `doc/WBS.E2E-003.md`, and Test `T-E2E-003` updated to `Done` / `Pass`.

## 2026-07-04 23:39 +08:00 PTCS-003 durable task handoff

- Scope: added `CodexFs.Ptcs.DurableMessageFabricBinding` as a thin wrapper over PTCS `CommSpaDurableMessageFabric` and `DurableIngress`.
- Behavior: package code can create a volatile durable PTCS admission profile, register participants through durable admission, submit an agent task with `SubmitAgentTaskDurableAsync`, query the accepted ticket, read the delivered worker inbox message, and ack through durable admission.
- Boundary: volatile durable admission is real PTCS ticketed handoff but does not satisfy production sharded crash-durable provider proof; `OPS-002` now owns session artifact persistence and ack/recovery ordering.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings / 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and output included `TC-PTCS-003 durable handoff passed`.
- Traceability: WBS `PTCS-003`, detail `doc/WBS.PTCS-003.md`, Test `T-PTCS-003`, SA `SA-TBD-004`, and SD §8 updated.

## 2026-07-04 23:46 +08:00 OPS-002 session persistence boundary

- Scope: added `ArtifactKind.SessionBoundaryJson` and `SingleCycleResult.PersistenceBoundaryPath`.
- Behavior: `SessionEngineCycle.runSingleCycleAsync` now sends the PTCS reply, writes `session-boundary.json` with `ready-to-ack` phase, reply message id and selected ack cursor, then acknowledges the session inbox cursor.
- Boundary: this proves bounded single-cycle ack-after-artifact-and-reply-boundary ordering; crash restart rehydration and sharded provider replay remain future worker-loop/provider scope.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyMessageToEngineReply.fsx` passed; verifier output included `TC-OPS-002 recovery/ack ordering passed`.
- Evidence: generated boundary artifact `G:\codex.fs\src\codex.fs\.codex.fs\e2e002-artifacts\sessions\e2e002-default\runs\run-20260704154722249-1de4b023\session-boundary.json`.
- Traceability: WBS `OPS-002`, detail `doc/WBS.OPS-002.md`, Test `T-OPS-002`, and SD §11 updated.

## 2026-07-05 00:16 +08:00 CLI-004 terminal self-use

- Scope: continued under endurance rules after all prior WBS rows were complete; added `CLI-004` for real terminal self-use hardening.
- Behavior: `codex.fs.cli host status --host <advertiseUri>` now calls the host health endpoint, and `session send --prompt @file` resolves file content in the CLI client before sending to host.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-CLI-004 host status and @file prompt resolver passed`.
- Manual CLI self-use: started `codex.fs.host` on LAN URI `http://10.28.112.93:10481`, ran compiled CLI `host status`, `session send --prompt @G:\codex.fs\src\codex.fs\.codex.fs\cli004-selfuse\prompt.md`, `session status`, `session drain`, and final `session status`; results were running, accepted, pendingCount 1, drained, and pendingCount 0.
- Evidence: `G:\codex.fs\src\codex.fs\.codex.fs\cli004-selfuse\summary.json`.
- Traceability: WBS `CLI-004`, detail `doc/WBS.CLI-004.md`, Test `T-CLI-004`, SD §14, and KM updated.

## 2026-07-05 01:00 +08:00 HOST-004/DOC-004/REL-004 host usability handoff

- Scope: fixed the reported usability defect where `http://10.28.112.93:10481/` returned 404 and global tools were not present under `C:\Users\Administrator\.dotnet\tools`.
- Behavior: `GET /` now returns a human-facing host landing page with health/OpenAPI/Swagger links; OpenAPI metadata applies endpoint summaries/descriptions; global handoff uses installed `codex.fs.cli` and `codex.fs.host` commands from `0.1.0-alpha.2`.
- Packaging: bumped package versions to `0.1.0-alpha.2`, packed all packages to `G:\codex.fs\bin\host-usability-packs-20260705004149-alpha2`, and installed global `codex.fs.cli` / `codex.fs.host.tool`.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; HTTP checks for `/`, health, `/openapi/v1.json`, and `/docs/index.html` returned 200; `codex.fs.cli host status --host http://10.28.112.93:10481` returned running host JSON.
- Browser/API docs evidence: Playwright verified root and Swagger UI at `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\summary.json`; screenshots are `root.png` and `docs.png`; OpenAPI includes root summary and expected host/session paths.
- SDK docs evidence: alpha.2 nupkg files contain generated XML docs under `lib/net10.0/*.xml` or tool package `tools/net10.0/any/*.xml`.
- Traceability: RFC `doc/RFC/RFC-OPS-0001.host-tool-usability.md`, `DEVOP.md`, WBS `HOST-004` / `DOC-004` / `REL-004`, Test `T-HOST-004` / `T-DOC-004` / `T-REL-004`, SD §9-§10, and KM updated.

## 2026-07-05 01:02 +08:00 Correction: RFC ID traceability

- Correction for the previous `HOST-004/DOC-004/REL-004 host usability handoff` entry: the RFC traceability target is `RFC-OPS-0001` at `doc/RFC/RFC-OPS-0001.host-tool-usability.md`.

## 2026-07-05 01:22 +08:00 CLI-005/HOST-005/UI-002 usability correction

- Scope: corrected the installed terminal command from `codex.fs.cli` to `codex.fs` while keeping package id `codex.fs.cli`; bumped package family version to `0.1.0-alpha.3`.
- Host seam: added `HostRuntime.startWithMessageFabric` so a PTCS Host or peer cluster node can run codex.fs host runtime over caller-owned `CommSpaMessageFabric` instead of an isolated package-owned fabric.
- PTCS Web profile: verified the correct local browser path is `http://127.0.0.1:82/chat`; `https://my-ai.co.in:81/chat` GitHub OAuth redirect is expected for the public profile. Real local82 browser/send evidence is under `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82-send`.
- Packaging/tests: `dotnet build .\codex.fs.slnx --no-restore` and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; packed alpha.3 to `G:\codex.fs\bin\cli-command-packs-20260705012257-alpha3`; installed global tools show `codex.fs.cli 0.1.0-alpha.3` command `codex.fs` and `codex.fs.host.tool 0.1.0-alpha.3` command `codex.fs.host`.
- Handoff evidence: `codex.fs --help`, `codex.fs host status --host http://10.28.112.93:10481`, HTTP checks for `/`, health, OpenAPI and docs returned 200; Playwright evidence is `G:\codex.fs\.codex.fs\host-usability-playwright-20260705012257-alpha3\summary.json`.
- Boundary: standalone `codex.fs.host` remains valid for CLI/API/docs verification but does not make codex.fs workers visible in an already running PTCS Web host. Production PTCS Web integration must share PTCS fabric via caller-owned MessageFabric/ActorFabric.
- Traceability: added `RFC-UI-0002`, WBS `HOST-005`, `CLI-005`, `UI-002`, Test `T-HOST-005`, `T-CLI-005`, `T-UI-002`, and updated SD/Requirement/README/DEVOP/KM.

## 2026-07-05 02:05 +08:00 CLI-006/CLI-007 explicit CLI alias and worker routing

- Scope: restored explicit `codex.fs.cli` executable as the primary PoC CLI and added `codex.fs.tool` short alias package for `codex.fs`.
- Behavior: both executables share `CodexFs.Cli.ProgramCore.run`; canonical help uses `USAGE: codex.fs.cli`, alias help uses `USAGE: codex.fs`.
- Routing: `session send` defaults to the derived SessionWorker / foreman participant `<ptcs.sessionParticipantPrefix>.<sessionId>`; `--worker-id <participantId>` overrides the direct target.
- API: `SessionSendRequest` now includes `WorkerId`; `SessionSendResponse` now includes `TargetParticipantId`.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed after restore; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and covered default foreman target plus explicit worker inbox delivery through real PTCS MessageFabric.
- Traceability: added `RFC-CLI-0001`, WBS `CLI-006` / `CLI-007`, Test `T-CLI-006` / `T-CLI-007`, and updated Requirement/SD/README/DEVOP/KM.

## 2026-07-05 02:48 +08:00 HOST-006/CLI-008 standalone chat and CLI transport errors

- Scope: fixed user-reported CLI crash on refused host endpoint and added standalone host `/chat` operator PoC form.
- Root cause: the user command used `14724`, which was the prior host process PID rather than the HTTP port, but CLI still failed incorrectly by surfacing an unhandled `HttpRequestException` stack trace. Standalone host also had no `/chat` route; it only exposed root/docs/API.
- Behavior: `CodexFs.Cli.CliHttp` now returns readable non-success `CliHttpResult` for connection failures; `/chat` GET returns a form and POST sends through the same `acceptSessionMessageAsync` MessageFabric path used by CLI.
- Boundary: `/chat` is an operator PoC over standalone host MessageFabric, not the production PTCS participant-perspective Web UI. PTCS Web integration still requires caller-owned PTCS MessageFabric from the PTCS Host process.
- Packaging: package family bumped to `0.1.0-alpha.5`.
- Tests: `dotnet build .\codex.fs.slnx` passed with 0 warnings/errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and covered `/chat`, OpenAPI `/chat`, health `chatUri`, default chat target, and readable refused-host CLI error.
- Traceability: added `RFC-HOST-0001`, WBS `HOST-006` / `CLI-008`, Test `T-HOST-006` / `T-CLI-008`, and updated Requirement/SD/README/DEVOP/KM.

## 2026-07-05 03:10 +08:00 HOST-007/CLI-009 PTCS hub chat alignment

- Scope: corrected alpha.5 `/chat` PoC direction after user clarified that product chat must use the existing PTCS WebSharper chat room/hub.
- Behavior: standalone `GET /chat` is now a guard page pointing to PTCS chat; standalone prompt testing moved to `GET/POST /diagnostics/session-send`; health JSON exposes `diagnosticsSessionSendUri`; OpenAPI includes the guard, diagnostics, and default foreman send route.
- CLI UX: `session send` no longer requires a user-known session id. No-session send posts to `/api/codexfs/foreman/messages` and targets `agent.codexfs.foreman` by default.
- Packaging: package family bumped to `0.1.0-alpha.6`.
- Tests so far: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings/errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and covered PTCS chat guard, diagnostics send, OpenAPI paths, and no-session foreman send.
- Traceability: added `RFC-HOST-0002`, WBS `HOST-007` / `CLI-009`, Test `T-HOST-007` / `T-CLI-009`, and updated Requirement/SD/README/DEVOP/KM.

## 2026-07-05 03:18 +08:00 HOST-007/CLI-009 installed alpha.6 verification

- Packaging/install: packed six alpha.6 packages under `G:\codex.fs\bin\ptcs-hub-align-packs-20260705030317-alpha6` and installed global `codex.fs.cli`, `codex.fs.tool`, and `codex.fs.host.tool` version `0.1.0-alpha.6` under `C:\Users\Administrator\.dotnet\tools`.
- Host: stopped old alpha.5 PID `21516`; started alpha.6 host PID `22548` at `http://10.28.112.93:10481`; host stdout is `G:\codex.fs\.codex.fs\host-run\20260705030317-alpha6\stdout.log`.
- Availability: root, `/chat`, `/diagnostics/session-send`, health, OpenAPI, and Swagger returned HTTP 200; health reports `diagnosticsSessionSendUri` and no `chatUri`; OpenAPI includes `/chat`, `/diagnostics/session-send`, and `/api/codexfs/foreman/messages`.
- CLI installed-tool evidence: `codex.fs.cli session send --host http://10.28.112.93:10481 --prompt ...` and `codex.fs session send --host http://10.28.112.93:10481 --prompt ...` both returned `sessionId = foreman` and `targetParticipantId = agent.codexfs.foreman`; `session status --session foreman` showed both prompts in the real MessageFabric inbox.
- Browser evidence: Playwright summary `G:\codex.fs\.codex.fs\ptcs-hub-align-20260705030317-alpha6\summary.json` passed root PTCS note, `/chat` guard without prompt composer, diagnostics foreman default, mobile diagnostics geometry, and Swagger UI checks.

## 2026-07-05 04:22 +08:00 PRODUCT-001 product responsibility reset

- Scope: accepted `RFC-PRODUCT-0001` to reset product responsibility before more runtime/actor/Web code is added.
- Boundary: `PTCS Host` remains the WebSharper chat/hub/auth profile host; `codex.fs.host` is composition/control/docs/deployment and must not own canonical prompt assembly.
- Runtime rule: prompt/history splice, local compact, headless CLI invocation, stdio capture, note/artifact persistence and recovery boundary belong to runtime/session worker behavior.
- Actor rule: `SessionActor` is a specialized `WorkerActor` / Foreman participant over PTCS ActorFabric/MessageFabric and future sharded delivery; spawned workers must register as PTCS participants.
- Follow-up WBS/Test: added `RUNTIME-001`, `ACTOR-001`, `CLI-010`, `WEB-001`, and `PERSIST-001` as planned RFC/test slices.
- Traceability: updated `Requirement.md`, `SA.md`, `SD.md`, `WBS.md`, `Test.md`, `RFC_Project_Planing.md`, `WBS.PRODUCT-001.md`, and `MCP.KM.md`.

## 2026-07-05 04:25 +08:00 RUNTIME-001 prompt-loop package boundary

- Scope: accepted `RFC-RUNTIME-0001` as a contract/RFC slice; no code split was claimed.
- Boundary: runtime owns prompt-loop orchestration and side-effect ordering; host/PTCS/actor/CLI/Web own transport, HTTP, delivery and UI concerns.
- Contract: SD §11.3 now defines `RuntimeCycleInput`, `RuntimeEffect`, `decideCycle`, `interpretCycleAsync`, and ports/effects direction.
- Migration: `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync` remains real bounded E2E evidence but is marked as a migration candidate for runtime extraction.
- Traceability: updated WBS `RUNTIME-001`, detail `doc/WBS.RUNTIME-001.md`, Test `T-RUNTIME-001`, SD §11.3, and KM.

## 2026-07-05 04:34 +08:00 ACTOR-001 session/worker actor model

- Scope: accepted `RFC-ACTOR-0001` as a contract/RFC slice; no actor code was claimed.
- Boundary: future `codex.fs.actor` is a PTCS ActorFabric adapter over runtime and MessageFabric, not a parallel actor/message fabric.
- Model: `WorkerActor` is the common capability; `SessionActor` is a specialized WorkerActor / Foreman participant and may call runtime or spawn workers.
- Delivery: actor delivery confirm and MessageFabric ack happen only after runtime ready-to-ack evidence and reply/result reference exist; production sharded durability still requires selected provider proof.
- Traceability: updated WBS `ACTOR-001`, detail `doc/WBS.ACTOR-001.md`, Test `T-ACTOR-001`, SA actor path, SD §11.2, and KM.

## 2026-07-05 04:22 +08:00 CLI-010 interactive participant CLI client

- Scope: accepted `RFC-CLI-0002` as a contract/RFC slice; no interactive CLI implementation was claimed.
- Boundary: `codex.fs.cli` is a terminal participant client. It sends user intent through host/PTCS APIs and never owns prompt assembly, headless invocation, chat truth or artifact/note persistence.
- UX: first-use prompt targets Foreman by default; future interactive mode supports target switching across Foreman, session, exact participant/worker, public and group scopes, with visible sender/target/perspective state.
- Invocation: CLI may collect engine/model/reasoning/invocation options, but runtime/actor validates policy and engine adapters render versioned argv.
- Traceability: updated WBS `CLI-010`, detail `doc/WBS.CLI-010.md`, Test `T-CLI-010`, Requirement R-002, SA §3.6, SD §14.2, `RFC_Project_Planing.md` and KM.

## 2026-07-05 04:27 +08:00 PERSIST-001 transcript/note/artifact policy

- Scope: accepted `RFC-PERSIST-0001` as a contract/RFC slice; no new persistence provider implementation was claimed.
- Boundary: runtime owns run evidence write ordering; host/actor construct the concrete provider; CLI/Web render redacted summaries and manifest/note refs.
- Policy: raw prompt/stdout/stderr/final artifacts are private by default, public exports are redacted-only and require sensitive scanning.
- Note/compact: `note.md` is a redacted human-readable run summary for browsing and compact input; compact summaries must preserve PTCS message ids, run ids and artifact refs without replacing raw artifacts.
- Traceability: updated WBS `PERSIST-001`, detail `doc/WBS.PERSIST-001.md`, Test `T-PERSIST-001`, Requirement R-004/R-005/R-008, SA storage/security, SD §12/§13, `RFC_Project_Planing.md` and KM.

## 2026-07-05 04:34 +08:00 WEB-001 PTCS AI chat bundle

- Scope: accepted `RFC-WEB-0001` as a Web bundle contract/RFC slice; no WebSharper implementation was claimed.
- Boundary: `codex.fs.web` is a PTCS WebSharper extension/bundle such as `useAIChat(...)`, registered through PTCS `CommHub`, not a standalone `codex.fs.host` `/chat` replacement.
- UX: Web target vocabulary covers Foreman, exact worker participant, public channel and group id; perspective switching is authorized read/render only.
- Invocation/artifacts: browser controls emit normalized intent metadata while runtime/actor validates policy and renders versioned CLI argv; Web renders redacted summary plus manifest/note refs, not raw stdout/stderr.
- Traceability: updated `RFC-WEB-0001`, WBS `WEB-001`, detail `doc/WBS.WEB-001.md`, Test `T-WEB-001`, Requirement, SA, SD, `RFC_Project_Planing.md` and KM.

## 2026-07-05 10:30 +08:00 WEBR-001 PTCS classic webshell rewrite reset

- Scope: accepted `RFC-WEB-0002` as a corrective reset/RFC slice after confirming current `codex.fs.host` web is only control/diagnostics and not PTCS classic chat shell.
- Cut list: standalone `/chat` guard, `/diagnostics/session-send`, HOST-006 PoC and HOST-007 guard alignment are not product Web acceptance paths.
- Target: product Web must reuse PTCS classic `/chat` shell with tabs/nav, participant list, thread/session/composer, plus `codex.fs.web` WebSharper Bundle modeled after `PulseTrade.Comm.Spa.Dynamic`.
- Actor boundary: AI execution must be PTCS ActorFabric SessionActor/WorkerActor plus runtime prompt loop; browser only sends MessageFabric intent and renders redacted refs.
- Traceability: added `RFC-WEB-0002`, `WBS.WEBR-001.md`, `Test.WEBR-001.md`, WBS rows `WEBR-001..WEBR-008`, `RUNTIME-002`, `ACTOR-002`, `E2E-004`, and matching Test rows/verifier contracts.

## 2026-07-05 10:47 +08:00 WEBR-002 PTCS classic shell inventory

- Scope: completed source/API inventory before adding any new Web code.
- Evidence: `doc/WEBR-002.PTCS-classic-shell-inventory.md` maps real PTCS package/Host/Dynamic/codex.fs host source paths.
- PTCS shell: confirmed `/chat`, `/sets`, `/actors`, `/chat/api/agents`, `/chat/api/thread`, `/chat/api/send`, `/sync/ws`, classic chat DOM selectors and existing browser verifier expectations.
- Extension seam: confirmed `CommHub.RegisterClientExtension`, script asset registration, fixed JSON POST handlers, `ClientExtensionRegistration`, `ClientExtensionScriptAsset`, and Dynamic `useDynamicSdui(...)` as the model for `useAIChat(...)`.
- Cut-list: reconfirmed standalone `codex.fs.host` `/chat` and `/diagnostics/session-send` are control/diagnostics only, not product Web acceptance.
- Verification: `dotnet fsi --exec .\misc\verifyPtcsClassicShellInventory.fsx` passed.
- Traceability: updated WBS/Test detail and stock rows; added verifier `misc/verifyPtcsClassicShellInventory.fsx`.

## 2026-07-05 11:07 +08:00 WEBR-003 codex.fs.web WebSharper Bundle scaffold

- Scope: added `src/codex.fs.web` as the first real PTCS WebSharper Bundle project for codex.fs Web, modeled after `PulseTrade.Comm.Spa.Dynamic`.
- Package shape: `codex.fs.web` targets `net10.0`, uses `WebSharperProject=Bundle`, exact `PulseTrade.Comm.Spa [0.2.5-beta71]`, `WebSharper.FSharp 10.1.5.674`, generated package on build, and no hand-written JavaScript.
- Server seam: `CommHub.useAIChat(...)` registers generated script assets, a fixed metadata JSON POST handler, extension manifest and append-page shape template through PTCS extension APIs.
- Build stability: added `wsconfig.json` to disable WebSharper build service/logging after local `websharper.log` access-denied failures were traced to `wsfscservice`; only generated WebSharper logs are ignored from Git.
- Package content: generated `wwwroot/js` is tracked and packed as `content/wwwroot/js`, matching `PulseTrade.Comm.Spa.Dynamic`.
- Verification: `dotnet fsi --exec .\misc\verifyCodexFsWebBundle.fsx`, `dotnet build .\codex.fs.slnx`, and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed.
- Traceability: updated WBS/Test detail and stock rows, SD §14.3, `RFC_Project_Planing.md`, README and KM.

## 2026-07-05 11:40 +08:00 WEBR-004 useAIChat registration contract

- Scope: moved `useAIChat(...)` from bundle scaffold seam into compiled integration coverage against real PTCS `CommHub` APIs.
- Implementation: `tests/codex.fs.Tests` now references `src/codex.fs.web`; `Program.fs` asserts `CommHub.useAIChat()` extension manifest, generated script assets, WebSharper runtime asset, metadata JSON handler and append-page shape template.
- Verifier: added `misc/verifyUseAIChatRegistration.fsx` using `FAkka.Argu` and `ParseLine.fsx`; command `dotnet fsi --exec .\misc\verifyUseAIChatRegistration.fsx` passed on 2026-07-05 11:37 +08:00.
- Traceability: updated WBS/Test detail and stock rows, SD §14.3, README/project planning and KM. `WEBR-005` is now unblocked for product PTCS classic `/chat` host composition.

## 2026-07-05 12:35 +08:00 WEBR-005 PTCS webshell host profile

- Scope: added an explicit product web profile instead of repurposing the ASP.NET `/chat` guard page.
- Implementation: `HostConfig.WebShell` adds `web.profile` and web bind/advertise settings; `HostRuntime` reports webshell profile; `HostWebShell.tryStartAsync` starts PTCS classic `/chat` with a shared `CommHub`, `CommSpaMessageFabric` and `useAIChat()` registration.
- Host tool: `codex.fs.host start --setting web.profile=ptcs-webshell ...` starts the PTCS webshell; default start remains control-only.
- Verifier: added `misc/verifyHostPtcsWebProfile.fsx`; command `dotnet fsi --exec .\misc\verifyHostPtcsWebProfile.fsx` passed on 2026-07-05 12:32 +08:00.
- Traceability: updated WBS/Test detail and stock rows, SD §14.3, README/project planning and KM. `WEBR-006` still waits for ActorFabric-visible participants; `WEBR-008` is unblocked.

## 2026-07-05 13:05 +08:00 WEBR-008 no standalone product chat

- Scope: added regression coverage so ASP.NET control `/chat` and diagnostics cannot be mistaken for product chat.
- Implementation: control root, legacy `/chat` and diagnostics text now point product browser chat to `web.profile=ptcs-webshell`.
- Verifier: added `misc/verifyNoStandaloneChatProductPath.fsx`; command `dotnet fsi --exec .\misc\verifyNoStandaloneChatProductPath.fsx` passed on 2026-07-05 13:03 +08:00.
- Traceability: updated WBS/Test detail and stock rows, SD §14.3 and KM. Remaining work is runtime extraction, ActorFabric participant visibility, AI controls/artifact refs and full E2E.

## 2026-07-05 13:59 +08:00 RUNTIME-002 runtime prompt-loop extraction

- Scope: extracted host-era prompt/request/argv/reply/boundary planning into reusable runtime code for future ActorFabric workers.
- Implementation: added `src/codex.fs/RuntimePromptLoop.fs` with `RuntimePromptInput`, `RuntimePromptPlan`, `AgyPrintExecutionInput`, `RuntimeExecutionPlan`, `RuntimeReplyIntent` and `RuntimeReadyToAckBoundary`; updated `src/codex.fs.host/SessionEngineCycle.fs` to use it.
- Boundary: host single-cycle remains the PTCS/artifact/process/reply/ack interpreter; `RuntimePromptLoop` is deterministic planning and does not claim durable sharded delivery.
- Tests: `dotnet build .\codex.fs.slnx`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyRuntimeLoopExtraction.fsx` passed. The verifier delegated to `misc/verifyMessageToEngineReply.fsx` and wrote ignored real-path artifacts under `G:\codex.fs\src\codex.fs\.codex.fs\runtime002-artifacts`.
- Traceability: updated WBS/Test detail and stock rows, SD §11.3/§12 and KM. `ACTOR-002` is unblocked for real PTCS ActorFabric Foreman/Worker proof.

## 2026-07-05 14:25 +08:00 ACTOR-002 PTCS ActorFabric Foreman/Worker proof

- Scope: added the first real PTCS ActorFabric-backed Foreman/Worker participant proof after runtime prompt-loop extraction.
- Implementation: added `src/codex.fs.ptcs/ActorFabricBinding.fs` with `WorkerParticipantSpec`, registration/spawn commands, registration replies, `CodexWorkerActor`, `props` and `spawnWorker`; the actor registers through `MessageFabricBinding.registerParticipantAsync`.
- Boundary: this proves Foreman/Worker visibility over PTCS ActorFabric/MessageFabric. Durable sharded delivery, passivation/recovery and actor-invoked `RuntimePromptLoop` remain future hardening/E2E work.
- Tests: `dotnet fsi --exec .\misc\verifyPtcsActorFabricForeman.fsx -- --no-restore` passed on 2026-07-05 14:25 +08:00; the verifier checks source contracts, builds/runs `codex.fs.Tests`, starts real `CommSpaActorFabric` with LAN `ClusterHost`, spawns Foreman/Worker and verifies both as `agent` participants through MessageFabric listing.
- Traceability: updated WBS/Test detail and stock rows, SD §11.3/§14.3 and KM. `WEBR-006` is unblocked for AI target/perspective/invocation controls in the PTCS shell.

## 2026-07-05 15:05 +08:00 WEBR-006 PTCS AI intent controls

- Scope: implemented the PTCS classic shell AI target/perspective/invocation controls slice; no standalone `/chat` product path was added.
- Implementation: expanded AI extension metadata, added WebSharper append-input renderer controls, added responsive/scrolling control layout, copied PTCS package `build/**` assets into host outputs, and added `web.pcslRoot` config/health support for product webshell state.
- Behavior: real PTCS webshell can create an `AI Chat` page, add Foreman key JSON literal `"agent.codexfs.foreman"`, and append `codex.fs.web.ai-intent.v1` JSON through `/pages/api/append`.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyAiIntentControls.fsx -- --no-restore` passed. Browser evidence is under `G:\codex.fs\log\20260705\webr006-host8-*`.
- Caveats: `/sync/ws` returns 503 in current host composition while HTTP fallback APIs pass; PTCS `Server` static initialization can touch default AppContext `pcsl` before explicit hub injection, so deployments must use a dedicated `web.pcslRoot`.
- Traceability: updated SD, WBS, Test, DEVOP, README and KM. `WEBR-007` remains blocked until runtime artifact/note refs exist.

## 2026-07-05 15:20 +08:00 ACTOR-003 WorkerActor runtime artifact provider

- Scope: completed the non-fake actor/runtime artifact provider slice that unblocks WEBR-007.
- Implementation: added `CodexFs.Ptcs.RuntimeMessageFabricCycle`; refactored `CodexFs.Host.SessionEngineCycle` into a wrapper; added `RunRuntimeCycle` and `RuntimeCycleCompleted` to `CodexFs.Ptcs.ActorFabricBinding.CodexWorkerActor`.
- Behavior: WorkerActor now consumes a real PTCS MessageFabric inbox, invokes installed Agy through the shared runtime cycle, writes prompt/batch/request/rendered argv/stdout/stderr/final/result/manifest/boundary artifacts, sends a PTCS reply and acks the consumed prompt cursor.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyActorRuntimeArtifactProvider.fsx -- --no-restore` passed.
- Evidence: manifest `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\manifest.json`; boundary `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\session-boundary.json`.
- Boundary: this is real ActorFabric/MessageFabric/engine/artifact evidence but not production sharded crash-durable replay. `WEBR-007` is unblocked for browser rendering of real worker refs.

## 2026-07-05 13:55 +08:00 WEBR-007 PTCS artifact ref rendering

- Scope: added RFC-WEB-0003 and completed the PTCS classic `/chat` artifact-reference rendering slice for real worker replies.
- Runtime: added `RunNoteMarkdown`, `note.md`, `RuntimeReadyToAckBoundary.RunNotePath`, reply `note=` refs and runtime-cycle manifest/boundary persistence so CLI stdio/prompt history can be found without manual terminal copy.
- Host/Web: `codex.fs.host` now registers default Foreman participant `agent.codexfs.foreman` for first-use chat targeting; `CodexFs.Web.Client.AIChatClient` registers a PTCS reply renderer and a minimal bridge that turns codex.fs artifact reply text into an artifact card inside the PTCS message body.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyArtifactRefsInPtcsShell.fsx -- --no-restore` passed.
- Evidence: browser screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr007\webr007-artifact-refs.png`; manifest `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\manifest.json`; note `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\note.md`.
- Boundary: this renders refs inside PTCS classic chat and preserves artifact/note traceability. It is not yet the production durable sharded replay path; `E2E-004` remains the next end-to-end durable workflow item.

## 2026-07-05 14:15 +08:00 E2E-004 PTCS browser prompt to Foreman actor loop

- Scope: completed the first real PTCS classic browser prompt -> Foreman actor -> MessageFabric/runtime/Agy -> artifact reply loop.
- Implementation: `CodexFs.Host.HostWebShell` now starts a Foreman actor runtime loop when `RunningServer.ActorFabric` is available; `web.actorFabric=disabled` remains a no-actor regression profile.
- Verifier: added `misc/verifyPtcsAiChatE2E.fsx`, which builds/runs tests, starts LAN `ptcs-webshell`, uses Playwright to type prompt token `CODEXFS_E2E004_25765348a165` in `/chat`, waits for `codexfs-artifact-reply`, and verifies manifest/final/note files.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, and `dotnet fsi --exec .\misc\verifyPtcsAiChatE2E.fsx -- --no-restore` passed.
- Evidence: screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\e2e004\e2e004-ptcs-ai-chat.png`; manifest `G:\codex.fs\src\codex.fs\.codex.fs\e2e004-artifacts\e2e004-25765348a165\sessions\foreman\runs\run-20260705060552217-1daa3715\manifest.json`; final `G:\codex.fs\src\codex.fs\.codex.fs\e2e004-artifacts\e2e004-25765348a165\sessions\foreman\runs\run-20260705060552217-1daa3715\final.md`; note `G:\codex.fs\src\codex.fs\.codex.fs\e2e004-artifacts\e2e004-25765348a165\sessions\foreman\runs\run-20260705060552217-1daa3715\note.md`.
- Boundary: this is a real PTCS auto-local ActorFabric E2E path, not fake/mock. Production crash-durable sharded replay remains future durability hardening.

## 2026-07-05 20:30 +08:00 RFC-RUNTIME-0002 Foreman control plane and AI intent bridge

- Scope: accepted and implemented the Foreman control-plane correction for the five-layer product boundary: MessageFabric logical chat, ActorFabric runtime ownership, worker journal/wpcs execution truth, SA MCP tools for future worker control, and bounded Codex/Agy exec.
- Runtime: added per-command Agy permission policy fields through `RuntimePromptLoop.AgyPrintExecutionInput`, `RuntimeMessageFabricCycle.RuntimeCycleOptions`, `SessionEngineCycle.SingleCycleOptions` and `ActorFabricBinding.RunRuntimeCycle`; Foreman product webshell sets `AgyDangerouslySkipPermissions = Some true`.
- Webshell: `HostWebShell` now registers a default `codexfs-ai-chat` append page/key and starts an AI intent bridge that scans real PTCS append-page set values, parses `codex.fs.web.ai-intent.v1`, and sends MessageFabric direct/public/group messages without creating a second chat fabric.
- Tests: `dotnet build .\codex.fs.slnx --no-restore`, `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`, `dotnet fsi --exec .\misc\verifyAiIntentBridge.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-address auto --host-port 0 --host-run-seconds 300`, and `dotnet fsi --exec .\misc\verifyForemanPowershellDate.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-address auto --host-port 0 --host-run-seconds 360` passed.
- Evidence: bridge final `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-1551d57defdd\sessions\foreman\runs\run-20260705122751448-65fa215c\final.md`; PowerShell date final `G:\codex.fs\src\codex.fs\.codex.fs\e2e005-artifacts\e2e005-43de6675c620\sessions\foreman\runs\run-20260705122708659-30b8a856\final.md`; rendered argv `G:\codex.fs\src\codex.fs\.codex.fs\e2e005-artifacts\e2e005-43de6675c620\sessions\foreman\runs\run-20260705122708659-30b8a856\rendered-argv.json`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\e2e005\e2e005-foreman-powershell-date.png`.
- Boundary: AI Chat append page now reaches Foreman runtime through MessageFabric, but reply projection back into the append page remains future UI work; current runtime reply is visible in MessageFabric chat/artifacts.

## 2026-07-05 21:53 +08:00 WEBR-009 Codex intent execution correction

- Scope: fixed the AI intent path where `engine=codex` looked like echo instead of true Codex execution.
- Root cause: the webshell bridge dropped `engine/model/reasoning/invocation/approval`, runtime cycle was Agy-only, Windows npm `codex` shim was not directly startable by `ProcessStartInfo`, Codex prompt input needed UTF-8 stdin with `-`, `gpt-5-codex` is rejected by local ChatGPT-subscription Codex CLI, and Foreman replies could self-trigger another run.
- Implementation: AI intent metadata now becomes MessageFabric tags; runtime selects Agy or Codex per batch; Codex exec writes prompt through stdin, stores `--output-last-message`, resolves native Windows `codex.exe`, normalizes `default`/`gpt-5-codex` to no `--model`, and ignores self-authored replies.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; `misc/verifyAiIntentBridge.fsx` passed with Codex intent token `CODEXFS_BRIDGE_c5a847a2698c`.
- Evidence: final artifact `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-c5a847a2698c\sessions\foreman\runs\run-20260705135834033-f0d6b35d\final.md`; rendered argv `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-c5a847a2698c\sessions\foreman\runs\run-20260705135834033-f0d6b35d\rendered-argv.json`.

## 2026-07-06 08:36 +08:00 RFC-WEB-0004 AI intent output projection

- Scope: accepted the product bugfix RFC for AI Chat same-page output projection.
- Decision: `Target=Foreman`, `Invocation=Exec`, `Approval=Never` is valid; not seeing output is a UI/product bug, not user option error.
- Planning: added `doc/RFC/RFC-WEB-0004.ai-intent-output-projection.md`, `doc/WBS.WEBR-010.md`, `doc/Test.WEBR-010.md`, and stock REQ/SA/SD/WBS/Test updates.
- Acceptance: `misc/verifyAiIntentOutputProjection.fsx` must use Playwright against real PTCS webshell and fail raw JSON-only display.

## 2026-07-06 09:24 +08:00 WEBR-010 AI intent output projection implementation

- Scope: fixed the same-page AI Chat output bug; raw intent JSON is no longer the only user-visible result after Send.
- Root cause: browser append-page values from PTCS sharded stream were not fully included by the bridge scan, and the AI append renderer did not project the service participant reply thread `user.codexfs.web.ai-intent <-> agent.codexfs.foreman` back into the append page.
- Implementation: `CodexFs.Web.Client.AIChatClient` now renders `codexfs-ai-output*` controls, polls `/chat/api/thread`, labels the projected thread, parses full runtime reply bodies, and reuses artifact-card rendering; `HostWebShell.aiIntentValues` merges sharded append-page streams; `RuntimeMessageFabricCycle` now reads Agy final artifacts through stored artifact absolute paths instead of treating manifest reference paths as filesystem paths.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; `dotnet fsi --exec .\misc\verifyAiIntentOutputProjection.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-run-seconds 480` passed.
- Evidence: screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection.png`; manifest `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\manifest.json`; final `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\final.md`; rendered argv `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\rendered-argv.json`.
- Boundary: live 18488 handoff still needs deployment restart/health verification after this implementation closeout.

## 2026-07-06 09:42 +08:00 WEBR-010 live 18488 handoff and layout fix

- Scope: restarted live `http://10.28.112.93:18488` from copied runtime output and verified the user-facing AI Chat page, not just a temporary verifier host.
- UI fix: changed `codexfs-ai-controls` to flex-column with nested `codexfs-ai-fields` grid. The previous single-grid layout let full-span Send/action and output items overlap in PTCS append-page rendering.
- Verifier: `misc/verifyAiIntentOutputProjection.fsx` now supports `--existing-host-url` / `--existing-artifact-root` and includes a bounding-box gate for Send/output non-overlap.
- Live evidence: process path `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260706094118\app\codex.fs.host.tool.exe`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection-live18488.png`; manifest `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260706094118\artifacts\sessions\foreman\runs\run-20260706014146458-4f9ea7a6\manifest.json`.

## 2026-07-08 12:14 +08:00 WEBR-011 reply stdio artifact panel

- Scope: fixed the AI Chat reply layout gap where the final reply was visible only as path-like artifact refs and current-run stdio/final content could not be opened from the browser.
- Server: `CommHub.useAIChat(...)` now optionally registers `POST /client-extensions/codexfs-ai-chat/artifact/read`; `HostWebShell` enables it with the configured `artifact.root`. The handler accepts only relative paths under artifact root, rejects traversal/absolute paths and caps reads at 131072 bytes.
- Client: artifact reply cards keep `codexfs-artifact-summary` as the visible last message, keep refs collapsed in `codexfs-artifact-details`, and add `codexfs-stdio-open` to show a movable/resizable `codexfs-stdio-panel` with Final/Stdout/Stderr/Note tabs.
- Verification: `dotnet build .\codex.fs.slnx --no-restore -nr:false -p:GenerateDocumentationFile=false` passed; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore -p:GenerateDocumentationFile=false` passed; `misc/verifyAiIntentOutputProjection.fsx` passed on a temporary host and on live 18488 with output `TC-WEBR-011 reply stdio artifact panel passed`.
- Live evidence: URL `http://10.28.112.93:18488/page/codexfs-ai-chat`; runtime root `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260708121312`; PID `28360`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr011\webr010-ai-intent-output-projection-live18488.png`; manifest `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260708121312\artifacts\sessions\foreman\runs\run-20260708041454945-badcc934\manifest.json`.
- Known environment issue: normal XML doc generation is currently blocked by stale locked XML doc files under `bin/Debug` and `obj/Debug`; functional build/test/verifier used `GenerateDocumentationFile=false` and recorded the warning. This slice does not change SDK documentation output.
