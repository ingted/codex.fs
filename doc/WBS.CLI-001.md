# WBS Detail: CLI-001 Argu Command / Help

WBS ID：`CLI-001`
狀態：Done
Progress：100
StartTime：2026-07-04 21:44 +08:00
UpdatedAt：2026-07-04 21:48 +08:00
Previous：`HOST-003`
SD：`SD §14`, `SD §16.9`
Test：`T-CLI-001`

## Scope

建立 `codex.fs.cli` compiled CLI parser/help contract。此工項只負責 command DU、help、examples 與 invalid arg 行為，不連線 host、不寫 MessageFabric、不寫 artifacts。

## Implementation

- Added `src/codex.fs.cli/codex.fs.cli.fsproj`.
- Added `CodexFs.Cli.Cli` with nested FAkka.Argu DU command groups:
  - `session create/send/attach/drain`.
  - `run status/artifacts`.
  - `host status`.
  - `engine probe`.
- Added `CodexFs.Cli.Program` compiled entrypoint.
- Added `codex.fs.cli` to `codex.fs.slnx`.

## Verification

- `dotnet restore .\codex.fs.slnx` passed.
- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-CLI-001 Argu parser/help passed`.
- Tests verified help text, examples, valid command groups, and invalid arg error.

## Blockers

- None.

## Deferred

- `CLI-002`: submit session messages through host APIs / PTCS MessageFabric.
- `CLI-003`: attach/drain/status transcript behavior.
