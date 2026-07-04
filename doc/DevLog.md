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
