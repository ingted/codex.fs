# Test.FOREMAN-001 Foreman control plane and AI intent bridge

建立時間：2026-07-05 20:22 +08:00  
關聯 WBS：`doc/WBS.FOREMAN-001.md`

| Test ID | WBS | Case | Type | Real path requirement | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| T-FOREMAN-001 | FOREMAN-001 | RFC/current-state traceability | Docs | File trace must link RFC -> REQ/SA/SD/WBS/Test | Pass | `doc/RFC/RFC-RUNTIME-0002.foreman-control-plane-and-ai-intent-bridge.md`; stock docs updated |
| T-FOREMAN-002 | FOREMAN-002 | Agy Foreman execution policy argv order | Unit/Integration | Compiled runtime test must inspect real rendered argv from `RuntimePromptLoop.planAgyPrintExecution` | Pass | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`; rendered argv evidence from E2E-005 |
| T-WEBR-009 | WEBR-009 | AI intent bridge sends MessageFabric prompt | Integration/Browser | Real `CommHub` append page value and real `CommSpaMessageFabric` delivery | Pass | `misc/verifyAiIntentBridge.fsx`; final `G:\codex.fs\src\codex.fs\.codex.fs\webr009-artifacts\webr009-1551d57defdd\sessions\foreman\runs\run-20260705122751448-65fa215c\final.md` |
| T-E2E-005 | E2E-005 | Foreman handles `hi 請用 powershell 取日期時間` | Browser/E2E | Real PTCS `/chat` + ActorFabric Foreman loop + installed Agy + file artifacts | Pass | `misc/verifyForemanPowershellDate.fsx`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\e2e005\e2e005-foreman-powershell-date.png` |

## Acceptance Details

- `T-FOREMAN-002` must assert `--dangerously-skip-permissions` appears before `--print`.
- `T-E2E-005` must verify a completed artifact reply and a `final.md` containing the local date obtained during the run.
- Verifiers must be explicit when an evidence path is browser screenshot, artifact root, final artifact or operation log.
