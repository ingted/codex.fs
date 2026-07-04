# WBS Detail: E2E-002 MessageFabric To Engine Reply

WBS ID：`E2E-002`  
狀態：Done  
Progress：100  
StartTime：2026-07-04 22:13 +08:00  
UpdatedAt：2026-07-04 22:27 +08:00  
Previous：`HOST-002`, `CLI-003`  
SD：`Requirement §10`, `SA §6.1`, `SD §14`, `SD §15`  
Test：`T-E2E-002`

## Scope

第一個 closed-loop real path：

```text
participant
  -> PTCS CommSpaMessageFabric
  -> session worker
  -> engine adapter/process runner
  -> artifact store
  -> PTCS MessageFabric reply
```

## Acceptance

- Uses real PTCS MessageFabric.
- Uses installed engine where available; fixture-only output is not accepted as E2E pass.
- Saves prompt, stdout/stderr/event/final/result/manifest artifacts.
- Reply body contains redacted summary and artifact reference, not raw transcript.
- Ack happens only after durable-enough artifact/reply boundary for the selected profile.

## Implementation

- Added `CodexFs.Host.SessionEngineCycle.runSingleCycleAsync`.
- The single-cycle runner polls one PTCS session inbox batch, assembles a prompt, runs real Agy `--print`, writes artifacts, sends a PTCS direct reply and then acknowledges the inbox cursor.
- Agy argv rendering now places flags before `--print`; prompt text is the final positional argument.
- Added `misc/verifyMessageToEngineReply.fsx` with `FAkka.Argu` + `defaultArgumentsText` + `ParseLine.fsx`.
- Added `doc/Verification.md` entry for the verifier.

## Verification

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed.
- `dotnet fsi --exec .\misc\verifyMessageToEngineReply.fsx` passed and printed `TC-E2E-002 message to engine reply passed`.
- Verifier output:
  - artifact root: `G:\codex.fs\src\codex.fs\.codex.fs\e2e002-artifacts`
  - manifest: `G:\codex.fs\src\codex.fs\.codex.fs\e2e002-artifacts\sessions\e2e002-default\runs\run-20260704143115611-dd1309d3\manifest.json`
  - final: `G:\codex.fs\src\codex.fs\.codex.fs\e2e002-artifacts\sessions\e2e002-default\runs\run-20260704143115611-dd1309d3\final.md`

## Blockers

- None.

## Deferred

- Durable/crash recovery boundary remains `PTCS-003` / `OPS-002`.
- Long-running sharded actor loop remains future ActorFabric/session-worker work; this slice is bounded single-cycle helper only.
