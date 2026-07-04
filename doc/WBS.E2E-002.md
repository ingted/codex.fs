# WBS Detail: E2E-002 MessageFabric To Engine Reply

WBS ID：`E2E-002`  
狀態：Planned  
Progress：0  
StartTime：未動工  
UpdatedAt：2026-07-04 17:53 +08:00  
Previous：`HOST-002`, `CLI-002`  
SD：`Requirement §10`, `SA §6.1`, `SD §15`  
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

## Planned Verifier

`misc/verifyMessageToEngineReply.fsx`

## Blockers

- `HOST-002` minimal host runtime.
- `CLI-002` session send real path.
- Installed engine availability for real engine branch.
