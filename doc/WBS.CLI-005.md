# WBS Detail: CLI-005 Installed command name usability correction

WBS ID：`CLI-005`
狀態：Done
Progress：100
Previous：`CLI-004`, `REL-004`
Test：`T-CLI-005`
Test case：`TC-CLI-005 global tool command codex.fs help/status`
SD：`SD §14`

## Goal

修正 dotnet tool 使用面：package id 仍為 `codex.fs.cli`，但安裝後的使用者命令必須是 `codex.fs`。文件、help examples、host landing page 與 global tool verification 必須一致。

## Changes

- Changed `src/codex.fs.cli/codex.fs.cli.fsproj` `ToolCommandName` to `codex.fs`.
- Updated CLI parser `programName` and examples from `codex.fs.cli` to `codex.fs`.
- Updated host landing page CLI example to `codex.fs host status --host <advertiseUri>`.
- Bumped package family to `0.1.0-alpha.3` for a clean install/pack handoff.

## Acceptance

- `dotnet tool list --global` shows package `codex.fs.cli 0.1.0-alpha.3` with command `codex.fs`.
- `C:\Users\Administrator\.dotnet\tools\codex.fs.exe --help` and `codex.fs --help` returned exit code 0.
- `codex.fs host status --host http://10.28.112.93:10481` returned running host JSON against the installed alpha.3 host.
- Playwright evidence for the restarted alpha.3 host root/docs is `G:\codex.fs\.codex.fs\host-usability-playwright-20260705012257-alpha3\summary.json`.

## Boundary

No typo alias is added for `condex.fs`. The canonical command is `codex.fs`.

## Superseded

CLI-006 supersedes the alpha.3 command-name decision. Current contract restores explicit `codex.fs.cli` as the canonical PoC command and keeps `codex.fs` as a short alias from package `codex.fs.tool`.
