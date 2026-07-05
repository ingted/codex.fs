# WBS Detail: ACTOR-003 WorkerActor Runtime Artifact Provider

WBS ID：`ACTOR-003`  
狀態：Done  
Progress：100  
StartTime：2026-07-05 15:10 +08:00  
UpdatedAt：2026-07-05 15:20 +08:00  
Previous：`ACTOR-002`, `RUNTIME-002`, `WEBR-006`  
SD：`SD §11.2`, `SD §11.3`, `SD §12`, `SD §14.3`  
Test：`T-ACTOR-003`

## Scope

把既有 host-only single-cycle runner 改成 PTCS runtime cycle adapter，讓 WorkerActor 能呼叫同一條 real MessageFabric -> headless engine -> artifacts -> reply -> ack 路徑。

## Deliverables

| Deliverable | Status |
| --- | --- |
| `doc/RFC/RFC-ACTOR-0002.actor-runtime-artifact-provider.md` | Done |
| `CodexFs.Ptcs.RuntimeMessageFabricCycle` | Done |
| `CodexFs.Host.SessionEngineCycle` wrapper refactor | Done |
| `ActorFabricBinding.RunRuntimeCycle` / `RuntimeCycleCompleted` | Done |
| Compiled `TC-ACTOR-003` | Done |
| `misc/verifyActorRuntimeArtifactProvider.fsx` | Done |

## Acceptance

- Real PTCS ActorFabric starts on a LAN/non-loopback host in the test profile.
- Foreman/WorkerActor registers as a PTCS `agent` participant.
- A real user participant sends a direct message to the actor participant.
- Actor receives `RunRuntimeCycle`, invokes the shared runtime adapter and returns `RuntimeCycleCompleted`.
- The runtime adapter runs installed `agy --print`, writes prompt/batch/request/rendered argv/stdout/stderr/final/result/manifest/boundary artifacts, sends a MessageFabric reply with manifest/final refs, then acks the consumed cursor.
- No fake/mock mailbox or source-only smoke can satisfy this WBS.

## Blockers

None for this non-production-durable slice. Production sharded crash-durable delivery remains future hardening.

## Verification Evidence

- `dotnet build .\codex.fs.slnx --no-restore` passed.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-ACTOR-003 actor runtime artifact provider passed`.
- `dotnet fsi --exec .\misc\verifyActorRuntimeArtifactProvider.fsx -- --no-restore` passed.
- Manifest: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\manifest.json`
- Boundary: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\session-boundary.json`
- Final: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\final.md`
