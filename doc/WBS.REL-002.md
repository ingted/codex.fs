# WBS Detail: REL-002 codex.fs.cli dotnet tool package

WBS ID：`REL-002`
狀態：Done
Progress：100
StartTime：2026-07-04 22:35 +08:00
UpdatedAt：2026-07-04 22:41 +08:00
Previous：`REL-001`, `HOST-001`, `CLI-003`
SD：`SD §2`, `SD §14`
Test：`T-REL-002`

## Scope

驗證 `codex.fs.cli` 可從本機 nupkg 作為 dotnet tool 安裝並啟動，不執行 NuGet push，不新增 `nuget.config`。

## Implementation

- `codex.fs.cli.fsproj` already had `PackAsTool=true` and `ToolCommandName=codex.fs.cli`.
- Added root help handling in `CodexFs.Cli.Program.isRootHelp` for `--help`, `-h`, `help`, `/?`, and empty argv.
- Added tests for root help detection.

## Verification

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed.
- Packed local packages to `G:\codex.fs\bin\rel002-packs-202607042243`:
  - `codex.fs.0.1.0-alpha.1.nupkg`
  - `codex.fs.ptcs.0.1.0-alpha.1.nupkg`
  - `codex.fs.host.0.1.0-alpha.1.nupkg`
  - `codex.fs.cli.0.1.0-alpha.1.nupkg`
- Installed the CLI tool:
  - `dotnet tool install codex.fs.cli --tool-path G:\codex.fs\bin\rel002-tool-202607042243 --add-source G:\codex.fs\bin\rel002-packs-202607042243 --version 0.1.0-alpha.1`
- `G:\codex.fs\bin\rel002-tool-202607042243\codex.fs.cli.exe --help` returned exit code 0 and rendered root command groups/examples.

## Blockers

- None for `codex.fs.cli`.

## Deferred

- `REL-003`: standalone `codex.fs.host` dotnet tool entrypoint. This remains separate because the library package must remain referenceable as a normal NuGet package.
