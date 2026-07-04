# WBS Detail: CLI-006 Explicit CLI Command Plus Short Alias

WBS ID：`CLI-006`
狀態：Done
Progress：100
Previous：`CLI-005`, `REL-004`
Test：`T-CLI-006`
Test case：`TC-CLI-006 global codex.fs.cli and codex.fs commands`
SD：`SD §14`
RFC：`doc/RFC/RFC-CLI-0001.cli-alias-worker-routing.md`

## Goal

恢復 `codex.fs.cli.exe` 作為最直接的 PoC CLI executable，同時保留 `codex.fs.exe` 作為短 alias。兩個 command 必須共用同一套 parser、HTTP client 與 session contract。

## Implementation

- `src/codex.fs.cli/codex.fs.cli.fsproj` 的 `ToolCommandName` 改回 `codex.fs.cli`。
- 新增 `src/codex.fs.tool/codex.fs.tool.fsproj`，tool command name 為 `codex.fs`。
- 新增 `CodexFs.Cli.ProgramCore.run`，讓 explicit CLI 與 short alias 共用同一套執行路徑。
- package family bump to `0.1.0-alpha.4`。

## Evidence

- `dotnet build .\codex.fs.slnx --no-restore` passed after restore.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; tests assert canonical help `USAGE: codex.fs.cli` and short alias help `USAGE: codex.fs`。

## Blocker

None.
