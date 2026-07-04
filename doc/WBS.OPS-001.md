# WBS Detail: OPS-001 Process Orphan Recovery

WBS ID：`OPS-001`
狀態：Done
Progress：100
StartTime：2026-07-04 22:46 +08:00
UpdatedAt：2026-07-04 22:49 +08:00
Previous：`EN-002`, `HOST-002`
SD：`SA §9`, `SD §4`
Test：`T-OPS-001`

## Scope

建立最小 orphan recovery primitive，讓 codex.fs 只能清理自己保存 lease 的受控 process，不掃描或殺任意系統 process。

## Implementation

- Added `ProcessRunner.ProcessLease` with pid, process name, start timestamp and non-secret marker.
- Added `OrphanRecoveryOptions`, `OrphanRecoveryOutcome`, `OrphanRecoveryResult`.
- Added `recoverLeasedProcessAsync`, which matches pid/name/start time before calling existing `killProcessTree`.

## Verification

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-OPS-001 orphan process recovery passed`.
- The test launched a controlled `powershell.exe Start-Sleep -Seconds 60` process, created a lease from the process identity, recovered it, and asserted the process exited.

## Blockers

- None.

## Deferred

- `OPS-002`: durable recovery/ack ordering.
- Host/session integration for persisted process leases and startup recovery sweep.
