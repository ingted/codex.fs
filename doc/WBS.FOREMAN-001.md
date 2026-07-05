# WBS.FOREMAN-001 Foreman control plane and AI intent bridge

建立時間：2026-07-05 20:22 +08:00  
關聯 RFC：`doc/RFC/RFC-RUNTIME-0002.foreman-control-plane-and-ai-intent-bridge.md`

| ID | Work item | Prev | Progress | Status | Blocker | StartTime | UpdatedAt | SD | Test | Verifier |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- |
| FOREMAN-001 | RFC/current-state 文件鏈同步 | E2E-004 | 100 | Done | None | 2026-07-05 20:04 +08:00 | 2026-07-05 20:30 +08:00 | SD §14.4 | T-FOREMAN-001 | file trace |
| FOREMAN-002 | Agy Foreman execution policy and argv order | FOREMAN-001 | 100 | Done | None | 2026-07-05 20:24 +08:00 | 2026-07-05 20:30 +08:00 | SD §11.3, §14.4 | T-FOREMAN-002 | `tests/codex.fs.Tests`; `misc/verifyForemanPowershellDate.fsx` |
| WEBR-009 | Host AI intent bridge from append page to MessageFabric and Codex exec | FOREMAN-001;WEBR-006;ACTOR-003 | 100 | Done | None | 2026-07-05 20:25 +08:00 | 2026-07-05 21:53 +08:00 | SD §14.4 | T-WEBR-009;T-WEBR-009-CODEX;T-RUNTIME-CODEX-STDIN;T-RUNTIME-SELF-LOOP | `misc/verifyAiIntentBridge.fsx`; `tests/codex.fs.Tests` |
| E2E-005 | Real Foreman PowerShell date prompt E2E | FOREMAN-002;WEBR-009 | 100 | Done | None | 2026-07-05 20:26 +08:00 | 2026-07-05 20:30 +08:00 | SD §14.4 | T-E2E-005 | `misc/verifyForemanPowershellDate.fsx` |

## Notes

- `FOREMAN-002` must prove the rendered Agy argv order because Agy treats tokens after `--print` as prompt text.
- `WEBR-009` must not bypass PTCS MessageFabric. Append-page intent is only the observable UI entrypoint, but `engine/model/approval` metadata must still reach runtime selection.
- `WEBR-009` must support `engine=codex` as real `codex exec`; raw intent JSON visible in append history is not acceptance by itself.
- `E2E-005` acceptance must use installed Agy and real PTCS webshell/ActorFabric path; fake/mock engine output is not accepted.

## Evidence

- `dotnet build .\codex.fs.slnx --no-restore` passed.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and includes `TC-RUNTIME-002` argv order coverage.
- `dotnet fsi --exec .\misc\verifyAiIntentBridge.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-address auto --host-port 0 --host-run-seconds 300` passed:
  - hostUrl: `http://10.28.112.93:10450`
  - final: `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-1551d57defdd\sessions\foreman\runs\run-20260705122751448-65fa215c\final.md`
  - rendered argv: `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-1551d57defdd\sessions\foreman\runs\run-20260705122751448-65fa215c\rendered-argv.json`
- `dotnet fsi --exec .\misc\verifyAiIntentBridge.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-address auto --host-port 0 --host-run-seconds 300` passed after Codex correction:
  - hostUrl: `http://10.28.112.93:7399`
  - verifierToken: `CODEXFS_BRIDGE_c5a847a2698c`
  - expectedDate: `2026-07-05`
  - final: `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-c5a847a2698c\sessions\foreman\runs\run-20260705135834033-f0d6b35d\final.md`
  - rendered argv: `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-c5a847a2698c\sessions\foreman\runs\run-20260705135834033-f0d6b35d\rendered-argv.json`
- `dotnet fsi --exec .\misc\verifyForemanPowershellDate.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-address auto --host-port 0 --host-run-seconds 360` passed:
  - hostUrl: `http://10.28.112.93:10381`
  - expectedDate: `2026-07-05`
  - final: `G:\codex.fs\src\codex.fs\.codex.fs\e2e005-artifacts\e2e005-43de6675c620\sessions\foreman\runs\run-20260705122708659-30b8a856\final.md`
  - note: `G:\codex.fs\src\codex.fs\.codex.fs\e2e005-artifacts\e2e005-43de6675c620\sessions\foreman\runs\run-20260705122708659-30b8a856\note.md`
  - rendered argv: `G:\codex.fs\src\codex.fs\.codex.fs\e2e005-artifacts\e2e005-43de6675c620\sessions\foreman\runs\run-20260705122708659-30b8a856\rendered-argv.json`
  - screenshot: `G:\codex.fs\src\codex.fs\.playwright-mcp\e2e005\e2e005-foreman-powershell-date.png`
