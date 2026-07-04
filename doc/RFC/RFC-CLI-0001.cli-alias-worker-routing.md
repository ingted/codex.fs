# RFC-CLI-0001 CLI Alias And Session Worker Routing

ID：`RFC-CLI-0001`
狀態：Accepted
日期：2026-07-05
關聯：`Requirement.md`, `SD.md`, `WBS.CLI-006.md`, `WBS.CLI-007.md`, `Test.md`

## Background

The CLI package id is `codex.fs.cli`, and the explicit executable `codex.fs.cli.exe` is the most direct PoC entrypoint. The previous alpha.3 correction made `codex.fs.exe` available but removed the explicit CLI executable, which made the package harder to inspect and test.

The same turn also clarified a routing contract: terminal `session send` should talk to the session worker / foreman by default, unless a worker id is explicitly supplied.

## Goals

- Provide both `codex.fs.cli.exe` and `codex.fs.exe`.
- Keep both executables on one command implementation path.
- Define default `session send` target as the derived SessionWorker / foreman participant.
- Add an explicit worker override for advanced PoC and multi-worker tests.

## Non-Goals

- Do not add another message fabric.
- Do not make HTTP host control a MessageFabric transport.
- Do not implement the full sharded worker actor loop in this RFC.

## Decision

1. `codex.fs.cli` package installs command `codex.fs.cli`.
2. `codex.fs.tool` package installs command `codex.fs` and delegates to the same `CodexFs.Cli.ProgramCore.run`.
3. `session send` accepts optional `--worker-id <participantId>`.
4. Blank or missing worker id sends to `<ptcs.sessionParticipantPrefix>.<sessionId>`, the session worker / foreman.
5. Supplied worker id is treated as the exact PTCS target participant id.
6. `SessionSendResponse.targetParticipantId` exposes the effective direct target for verifier and operator evidence.

## Impact

- Operators can test the PoC through `codex.fs.cli` and still use `codex.fs` as a short alias.
- Existing alpha.3 installs must uninstall/update `codex.fs.cli` before installing `codex.fs.tool`, because alpha.3 owned the `codex.fs` shim.
- Tests and docs must distinguish package id, explicit command, and alias command.

## Acceptance

- `codex.fs.cli --help` and `codex.fs --help` both work.
- Default send target equals the derived session worker participant.
- `--worker-id` send target equals the supplied worker participant.
- Real PTCS MessageFabric evidence proves the override worker inbox receives the override prompt.
