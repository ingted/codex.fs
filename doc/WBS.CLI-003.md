# WBS Detail: CLI-003 Attach / Drain / Status

WBS ID：`CLI-003`
狀態：Done
Progress：100
StartTime：2026-07-04 22:00 +08:00
UpdatedAt：2026-07-04 22:09 +08:00
Previous：`CLI-002`
SD：`SD §14`
Test：`T-CLI-003`

## Scope

讓 `codex.fs.cli session status|attach|drain` 透過 host HTTP control endpoint 讀取 session participant inbox，並以 early terminal transcript 回傳目前訊息。

## Implementation

- Added host routes:
  - `GET /api/codexfs/session/{sessionId}/status`
  - `POST /api/codexfs/session/{sessionId}/attach`
  - `POST /api/codexfs/session/{sessionId}/drain`
- Added typed `SessionInboxMessageResponse` / `SessionInboxResponse` DTOs and endpoint examples for OpenAPI generation.
- `status` uses real PTCS `pollInboxAsync` without ack.
- `attach` uses real PTCS bounded `waitInboxAsync` without ack.
- `drain` uses real PTCS `drainInboxAsync` and acknowledges the returned cursor.
- Added CLI `session status` command and HTTP client methods for status/attach/drain.

## Verification

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-CLI-003 attach/drain/status passed`.
- Verification used a real Kestrel host advertised through non-loopback URI `http://10.28.112.93:8040` during the test run, real `CommSpaMessageFabric`, and no fake mailbox.
- Status/attach responses returned the CLI-submitted prompt in transcript JSON before ack.
- Drain returned the prompt and after-drain status returned `pendingCount = 0`.

## Blockers

- None.

## Deferred

- `E2E-002`: consume drained/attached inbox messages through the engine runner, persist run artifacts, and send the reply back to PTCS.
