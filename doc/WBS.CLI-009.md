# WBS Detail: CLI-009 No-session Foreman Send Default

WBS ID：`CLI-009`
狀態：Done
Progress：100
Previous：`CLI-007`, `HOST-007`
Test：`T-CLI-009`
Test case：`TC-CLI-009 session send without --session targets foreman`
SD：`SD §14`
RFC：`RFC-HOST-0002`
動工時間：2026-07-05 03:03 +08:00
更新時間：2026-07-05 03:10 +08:00

## Scope

修正 first-use CLI UX：

- `codex.fs.cli session send --prompt ... --host ...` 不再要求 `--session`。
- `SessionSendOptions.SessionId` 改為 `string option`。
- `SessionId = None` 時，CLI posts to `/api/codexfs/foreman/messages`。
- `SessionId = Some id` 時，保留 explicit session route `/api/codexfs/session/{id}/messages`。
- `--worker-id` 仍是 exact target participant override。

## Evidence

- `dotnet build .\codex.fs.slnx --no-restore`: passed, 0 warnings, 0 errors。
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`: passed。
- Tests assert Argu accepts `session send` without `--session`, the HTTP client receives 202, and the response has `sessionId = foreman` / `targetParticipantId = agent.codexfs.foreman`。
- Global installed tools from `G:\codex.fs\bin\ptcs-hub-align-packs-20260705030317-alpha6` verified `codex.fs.cli` and `codex.fs` no-session sends both return `sessionId = foreman`, `targetParticipantId = agent.codexfs.foreman`。
- `codex.fs.cli session status --session foreman --host http://10.28.112.93:10481` verified both no-session prompts in the real foreman inbox。

## Blocker

None.
