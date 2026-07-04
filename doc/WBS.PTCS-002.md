# WBS Detail: PTCS-002 MessageFabric Session Binding

WBS ID：`PTCS-002`  
狀態：Planned  
Progress：0  
StartTime：未動工  
UpdatedAt：2026-07-04 17:53 +08:00  
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
- verifier：planned `misc/verifyPtcsMessageFabric.fsx`。
- Evidence must show real PTCS path; fixture-only mailbox is not accepted.

## Blockers

- `PTCS-001` must choose exact PTCS package/reference range.
