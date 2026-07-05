# Test Detail: ACTOR-003 WorkerActor Runtime Artifact Provider

Test ID：`T-ACTOR-003`  
狀態：Pass  
UpdatedAt：2026-07-05 15:20 +08:00  
WBS：`ACTOR-003`

## Test Case

`misc/verifyActorRuntimeArtifactProvider.fsx`

## Real Path Requirement

- Real PTCS `CommSpaActorFabric` with LAN/non-loopback cluster host where the test environment has one.
- Real shared `CommHub` + `CommSpaMessageFabric`.
- Real user participant direct message to the actor participant.
- Real WorkerActor `Ask<RuntimeCycleCompleted>`.
- Real installed `agy --print`.
- Real file artifact store under ignored `.codex.fs/actor003-artifacts`.

## Expected Evidence

- Compiled test prints `TC-ACTOR-003 actor runtime artifact provider passed`.
- Verifier prints artifact root, manifest path, boundary path, final path and reply message id.
- Manifest, final and `session-boundary.json` exist on disk.
- User inbox contains the runtime reply body with manifest/final refs.
- Actor participant remains visible through MessageFabric participant listing.

## Actual Evidence

- `dotnet build .\codex.fs.slnx --no-restore` passed.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-ACTOR-003 actor runtime artifact provider passed`.
- `dotnet fsi --exec .\misc\verifyActorRuntimeArtifactProvider.fsx -- --no-restore` passed.
- Manifest: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\manifest.json`
- Boundary: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\session-boundary.json`
- Final: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-5d73330172b7\sessions\actor003-5d73330172b7\runs\run-20260705051932302-0f5dc2e5\final.md`

## Rejection Criteria

- Fake mailbox, fake process runner, fake artifact refs, source-string only smoke, or host-only verifier path.
- Raw prompt/stdout/stderr dumped into public chat body.
- MessageFabric cursor acked before reply evidence and boundary artifact.
