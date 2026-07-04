# WBS.OPS-002 Session Persistence Boundary

## 1. Status

| Field | Value |
| --- | --- |
| WBS ID | OPS-002 |
| Title | Session persistence boundary |
| Status | Done |
| Progress | 100 |
| Started | 2026-07-04 23:44 +08:00 |
| Updated | 2026-07-04 23:46 +08:00 |
| Previous item | PTCS-003 |
| SD | SA §9, SD §11 |
| Test | T-OPS-002 |
| Test case | TC-OPS-002 `misc/verifyMessageToEngineReply.fsx` boundary gate |

## 2. Scope

`SessionEngineCycle.runSingleCycleAsync` now persists a session boundary artifact after a PTCS reply is sent and before the consumed MessageFabric cursor is acknowledged.

Implemented:

- `ArtifactKind.SessionBoundaryJson`.
- `SingleCycleResult.PersistenceBoundaryPath`.
- `session-boundary.json` with phase `ready-to-ack`, selected cursor, consumed PTCS message ids, reply message id/body, artifact manifest path, final message path, and `persistedBeforeAck = true`.
- `misc/verifyMessageToEngineReply.fsx` validates the real boundary file, reply id, selected ack cursor, final artifact, reply MessageFabric delivery, and empty session inbox after ack.

Boundary:

- This slice proves ack ordering in the bounded single-cycle host helper.
- Crash restart rehydration and sharded provider replay remain future worker-loop/provider work, not claimed here.
- The evidence path is a real generated artifact under the verifier artifact root; it is not committed to Git.

## 3. Evidence

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings / 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed.
- `dotnet fsi --exec .\misc\verifyMessageToEngineReply.fsx` passed and produced `TC-OPS-002 recovery/ack ordering passed`.
- Example generated boundary evidence: `G:\codex.fs\src\codex.fs\.codex.fs\e2e002-artifacts\sessions\e2e002-default\runs\run-20260704154722249-1de4b023\session-boundary.json`.
