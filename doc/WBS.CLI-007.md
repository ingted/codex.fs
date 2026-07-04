# WBS Detail: CLI-007 Session Worker Default Target And Worker Override

WBS ID：`CLI-007`
狀態：Done
Progress：100
Previous：`CLI-002`, `CLI-006`
Test：`T-CLI-007`
Test case：`TC-CLI-007 session send default foreman and --worker-id override`
SD：`SD §14`
RFC：`doc/RFC/RFC-CLI-0001.cli-alias-worker-routing.md`

## Goal

明確定義 `session send` 的預設收件者是該 session 的 SessionWorker / 包工頭 participant。只有在 CLI 明確指定 `--worker-id` 時，才改送指定 worker participant。

## Implementation

- `SessionSendArgument` 新增 `Worker_Id` optional argument。
- `SessionSendOptions` / `SessionSendRequest` 帶 `WorkerId`。
- `SessionSendResponse` 新增 `TargetParticipantId`，讓 PoC 驗證可看到實際 direct target。
- Host default target is `<ptcs.sessionParticipantPrefix>.<sessionId>`; override uses the exact supplied worker participant id.

## Evidence

- `tests/codex.fs.Tests` sends one prompt without `WorkerId` and asserts `targetParticipantId = sessionParticipantId`。
- The same test sends one prompt with explicit worker id, asserts target differs from the session worker, and polls that worker inbox through real PTCS `CommSpaMessageFabric`。

## Blocker

None.
