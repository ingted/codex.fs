# WBS Detail: REL-003 codex.fs.host standalone tool entrypoint

WBS ID：`REL-003`
狀態：Done
Progress：100
StartTime：2026-07-04 22:55 +08:00
UpdatedAt：2026-07-04 23:06 +08:00
Previous：`REL-002`, `HOST-003`, `E2E-002`
SD：`SD §2`, `SD §9`
Test：`T-REL-003`

## Scope

建立 standalone host dotnet tool entrypoint，同時保留 `codex.fs.host` 作為可被 `PackageReference` 的 library package。

## Decision

- 不把 `src/codex.fs.host/codex.fs.host.fsproj` 直接改成 `PackAsTool=true`，避免破壞 library package layout。
- 新增 thin wrapper project `src/codex.fs.host.tool/codex.fs.host.tool.fsproj`。
- Tool package id：`codex.fs.host.tool`。
- Tool command name：`codex.fs.host`。
- Host tool 復用 `HostConfig`、`HostRuntime`、`HostControl`，不新增第二套 host protocol。

## Implementation

- Added `CodexFs.HostTool.HostTool` Argu command surface.
- Added `status` command for non-listening local config/runtime health.
- Added `start --run-seconds <n>` command for real bounded host control startup.
- Added `codex.fs.host.tool` to `codex.fs.slnx`.
- Added tests for root help, Argu parse, status text, and bounded start/stop using a LAN advertised URI.
- Updated README local install examples.

## Verification

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-REL-003 host tool start/status passed`.
- Packed packages to `G:\codex.fs\bin\rel003-packs-202607042303`.
- `codex.fs.host.tool.0.1.0-alpha.1.nupkg` contains `tools/net10.0/any/DotnetToolSettings.xml`, `codex.fs.host.tool.dll`, `codex.fs.host.dll`, and `README.md`.
- Installed tool to `G:\codex.fs\bin\rel003-host-tool-202607042303`.
- `G:\codex.fs\bin\rel003-host-tool-202607042303\codex.fs.host.exe --help` returned exit code 0.
- Installed tool `status` passed with LAN advertised URI `http://10.28.112.93:10437`.
- Installed tool `start --run-seconds 0` passed with LAN advertised URI `http://10.28.112.93:10440` and printed `status=stopped`.

## Blockers

- None.

## Deferred

- Durable task handoff remains `PTCS-003`.
- Session persistence / startup recovery remains `OPS-002`.
- Web UI host operation remains `UI-001`.
