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
