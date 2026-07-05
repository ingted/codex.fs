# Test Detail: WEBR-001 PTCS classic webshell rewrite

Test Group：`WEBR-001`  
狀態：Accepted for RFC/reset slice  
UpdatedAt：2026-07-05 10:30 +08:00

## Test Matrix

| Test ID | WBS ID | Test case / verifier | Type | Real path requirement | Expected evidence | Status |
| --- | --- | --- | --- | --- | --- | --- |
| T-WEBR-001 | WEBR-001 | TC-WEBR-001 RFC/WBS reset traceability | Docs | File-based RFC/stock doc trace is enough for reset slice only | `RFC-WEB-0002`, WBS, Test, Requirement, SA, SD, DevLog and KM updated | Pass |
| T-WEBR-002 | WEBR-002 | `misc/verifyPtcsClassicShellInventory.fsx` | Docs/Investigation | Read real PTCS Host/Dynamic source; no invented UI APIs | `dotnet fsi --exec .\misc\verifyPtcsClassicShellInventory.fsx` passed; `doc/WEBR-002.PTCS-classic-shell-inventory.md` maps `/chat`, `/chat/api/agents`, `/chat/api/thread`, `/chat/api/send`, `/sync/ws`, DOM testids, extension manifest/assets and cut-list | Pass |
| T-WEBR-003 | WEBR-003 | `misc/verifyCodexFsWebBundle.fsx` | Compile/WebSharper | Real `dotnet build` of `codex.fs.web` with `WebSharperProject=Bundle` | Bundle outputs under `wwwroot/js`, no hand-written JS, exact PTCS package reference | Planned |
| T-WEBR-004 | WEBR-004 | `misc/verifyUseAIChatRegistration.fsx` | Integration/Web | Real `CommHub` registration, not mocked manifest | Extension manifest includes `codex-fs-ai-chat`, script assets and JSON handlers | Planned |
| T-WEBR-005 | WEBR-005 | `misc/verifyHostPtcsWebProfile.fsx` | Browser/Integration | Real host profile serving PTCS classic shell | Playwright sees nav tabs, participant list, thread area and composer on `/chat` | Planned |
| T-RUNTIME-002 | RUNTIME-002 | `misc/verifyRuntimeLoopExtraction.fsx` | Unit/Integration | Real runtime modules, no host route mock | Runtime consumes MessageFabric refs, invokes engine adapter, persists evidence and returns reply intent | Planned |
| T-ACTOR-002 | ACTOR-002 | `misc/verifyPtcsActorFabricForeman.fsx` | Integration/Actor | Real PTCS `CommSpaActorFabric` and MessageFabric | Foreman/worker actors register as `agent` participants and appear through PTCS participant listing | Planned |
| T-WEBR-006 | WEBR-006 | `misc/verifyAiIntentControls.fsx` | Browser/Integration | Real PTCS shell extension controls | Target/perspective/engine/model/reasoning controls emit normalized intent metadata; no browser argv rendering | Planned |
| T-WEBR-007 | WEBR-007 | `misc/verifyArtifactRefsInPtcsShell.fsx` | Browser/E2E | Real worker run artifact refs | PTCS shell renders redacted reply, run id, manifest ref and note ref | Planned |
| T-WEBR-008 | WEBR-008 | `misc/verifyNoStandaloneChatProductPath.fsx` | Regression/Browser | Real host routes | No standalone diagnostics/guard page is used as product chat; control-only mode labels itself non-product | Planned |
| T-E2E-004 | E2E-004 | `misc/verifyPtcsAiChatE2E.fsx` | Browser/E2E | Real PTCS shell + ActorFabric + MessageFabric + headless engine | Human sends prompt in `/chat`, Foreman actor runs engine, artifacts are stored, reply appears with refs | Planned |

## Hard Gates

- Browser tests must use real Playwright/Chrome against a real host profile.
- UI acceptance must inspect visible PTCS classic shell elements, including tabs, participant list, thread area and composer.
- Actor tests must use PTCS ActorFabric/MessageFabric, not a fake mailbox.
- No test in this group may use standalone `GET/POST /diagnostics/session-send` as product chat acceptance.
- No test in this group may write raw prompt/stdout/stderr into public chat body.

## Evidence Paths

Future verifier evidence must record absolute paths for screenshots, Playwright summaries, generated bundle files, host logs and artifact manifests.
