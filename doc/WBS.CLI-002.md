# WBS Detail: CLI-002 Session Send Real Path

WBS ID：`CLI-002`
狀態：Done
Progress：100
StartTime：2026-07-04 21:51 +08:00
UpdatedAt：2026-07-04 21:56 +08:00
Previous：`CLI-001`, `PTCS-002`
SD：`SD §14`
Test：`T-CLI-002`

## Scope

讓 CLI `session send` 透過 host HTTP control endpoint，把 prompt 交給 host，再由 host 使用 real PTCS MessageFabric 寫入 session participant inbox。

## Implementation

- Added host route `POST /api/codexfs/session/{sessionId}/messages`.
- Added `HostControl.SessionSendRequest` / `SessionSendResponse`.
- Host derives session participant id from `ptcs.sessionParticipantPrefix` and registers both sender/session participants in real PTCS MessageFabric.
- Added `CodexFs.Cli.Cli.tryParseSessionSend`.
- Added `CodexFs.Cli.CliHttp.sendSessionMessageAsync`.
- `codex.fs.cli` entrypoint dispatches `session send` to the HTTP client; other command execution remains deferred.

## Verification

- `dotnet restore .\codex.fs.slnx` passed.
- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-CLI-002 CLI send through MessageFabric passed`.
- `TC-CLI-002` dynamically selected a non-loopback advertised host URI, posted CLI send through `CodexFs.Cli.CliHttp`, and polled real PTCS MessageFabric inbox for the derived session participant.

## Blockers

- None.

## Deferred

- `CLI-003`: attach/drain/status transcript behavior.
- `E2E-002`: engine execution and reply path after message intake.
