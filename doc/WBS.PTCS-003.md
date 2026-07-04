# WBS.PTCS-003 Durable Task Handoff

## 1. Status

| Field | Value |
| --- | --- |
| WBS ID | PTCS-003 |
| Title | 實作 durable task handoff |
| Status | Done |
| Progress | 100 |
| Started | 2026-07-04 23:35 +08:00 |
| Updated | 2026-07-04 23:39 +08:00 |
| Previous item | PTCS-002 |
| Next item | OPS-002 |
| SD | SD §8, §16.10 |
| Test | T-PTCS-003 |
| Test case | TC-PTCS-003 durable handoff |

## 2. Scope

`codex.fs.ptcs` now exposes a thin durable binding over PTCS `CommSpaDurableMessageFabric`.

Implemented:

- `DurableMessageFabricBinding.createVolatileDurableFabric` creates real PTCS `DurableIngress` + `CommSpaDurableMessageFabric` over a PTCS `CommHub`.
- `volatileProviderProofAsync` reads PTCS provider proof and intentionally fails the sharded crash-durable production gate for volatile local ingress.
- `registerParticipantAsync` uses durable admission for participant registration.
- `submitAgentTaskAsync` calls `SubmitAgentTaskDurableAsync` and returns PTCS ticket/message/result handle evidence.
- `pollInboxAsync`, `ackInboxAsync`, and `queryTicketAsync` expose the real PTCS durable/read boundary without creating a parallel mailbox.

Not claimed:

- No crash-durable restart/readback is claimed by this slice.
- No Akka.Cluster.Sharding.Delivery provider is implemented by `codex.fs`; production proof remains a selected PTCS/provider profile concern.
- Session artifact persistence and ack-after-artifact/reply recovery ordering remain `OPS-002`.

## 3. Evidence

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings / 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and output included `TC-PTCS-003 durable handoff passed`.
- `TC-PTCS-003` verified provider proof missing requirements include `provider-specific-sharding-delivery-runtime`, submitted a real durable agent task, queried the accepted ticket, read the delivered PTCS inbox message, and acked the cursor through durable admission.
