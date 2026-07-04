# WBS Detail: PTCS-002 MessageFabric Session Binding

WBS ID：`PTCS-002`
狀態：Done
Progress：100
StartTime：2026-07-04 20:41 +08:00
UpdatedAt：2026-07-04 20:45 +08:00
Previous：`PTCS-001`
SD：`SD §8`, `SD §16.6`
Test：`T-PTCS-002`

## Scope

實作 `codex.fs.ptcs` thin adapter，使用真 PTCS `CommSpaMessageFabric` 做 session participant register、send、poll、wait、ack、drain。

## Required Operations

| Operation | PTCS API |
| --- | --- |
| register session participant | `CommSpaMessageFabric.RegisterParticipantAsync` |
| send prompt/reply | `SendAsync` |
| poll/wait pending inbox | `PollInboxAsync` / `WaitInboxAsync` |
| ack processed batch | `AckAsync` |
| drain terminal attach batch | `DrainInboxAsync` |

## Acceptance

- 不新增平行 chat store、cursor registry 或 private message bus。
- MessageFabric message body is data, never shell command。
- verifier：`tests/codex.fs.Tests` 中 `TC-PTCS-002 MessageFabric binding passed`。
- Evidence must show real PTCS path; fixture-only mailbox is not accepted.

## Blockers

- None.

## Evidence

- Implemented `src/codex.fs.ptcs/MessageFabricBinding.fs` over concrete `PulseTrade.Comm.Spa.CommSpaMessageFabric`.
- Verified real PTCS in-process runtime path with `CommHub.createEmpty()` and `CommSpaMessageFabric.create`.
- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-PTCS-002 MessageFabric binding passed`.
- Covered register session/user, direct send, poll, ack, wait, drain, group upsert and group poll.
