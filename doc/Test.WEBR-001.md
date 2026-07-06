# Test Detail: WEBR-001 PTCS classic webshell rewrite

Test Group：`WEBR-001`  
狀態：Accepted for RFC/reset slice  
UpdatedAt：2026-07-05 15:20 +08:00

## Test Matrix

| Test ID | WBS ID | Test case / verifier | Type | Real path requirement | Expected evidence | Status |
| --- | --- | --- | --- | --- | --- | --- |
| T-WEBR-001 | WEBR-001 | TC-WEBR-001 RFC/WBS reset traceability | Docs | File-based RFC/stock doc trace is enough for reset slice only | `RFC-WEB-0002`, WBS, Test, Requirement, SA, SD, DevLog and KM updated | Pass |
| T-WEBR-002 | WEBR-002 | `misc/verifyPtcsClassicShellInventory.fsx` | Docs/Investigation | Read real PTCS Host/Dynamic source; no invented UI APIs | `dotnet fsi --exec .\misc\verifyPtcsClassicShellInventory.fsx` passed; `doc/WEBR-002.PTCS-classic-shell-inventory.md` maps `/chat`, `/chat/api/agents`, `/chat/api/thread`, `/chat/api/send`, `/sync/ws`, DOM testids, extension manifest/assets and cut-list | Pass |
| T-WEBR-003 | WEBR-003 | `misc/verifyCodexFsWebBundle.fsx` | Compile/WebSharper | Real `dotnet build` of `codex.fs.web` with `WebSharperProject=Bundle` | Passed on 2026-07-05 11:07 +08:00; generated 4 WebSharper JavaScript files under `src/codex.fs.web/wwwroot/js`; nupkg contains `content/wwwroot/js/CodexFs.Web.js`; exact PTCS package reference and no hand-written JS verified | Pass |
| T-WEBR-004 | WEBR-004 | `misc/verifyUseAIChatRegistration.fsx` | Integration/Web | Real `CommHub` registration, not mocked manifest | Passed on 2026-07-05 11:37 +08:00; verifier builds/runs `codex.fs.Tests` and asserts extension manifest, generated script assets, metadata JSON handler, runtime asset and append-page shape template | Pass |
| T-WEBR-005 | WEBR-005 | `misc/verifyHostPtcsWebProfile.fsx` | Browser/Integration | Real host profile serving PTCS classic shell | Passed on 2026-07-05 12:32 +08:00; verifier builds/runs `codex.fs.Tests`, starts real PTCS webshell on LAN IP, verifies `/chat` manifest, `codex-fs-ai-chat`, generated script asset, `/healthz`, non-guard page and host tool bounded start | Pass |
| T-RUNTIME-002 | RUNTIME-002 | `misc/verifyRuntimeLoopExtraction.fsx` | Unit/Integration | Real runtime modules, no host route mock | Passed on 2026-07-05 13:59 +08:00; verifier checks `RuntimePromptLoop`, builds/runs `codex.fs.Tests` for `TC-RUNTIME-002`, then delegates to real MessageFabric -> Agy -> artifact -> reply evidence under `G:\codex.fs\src\codex.fs\.codex.fs\runtime002-artifacts` | Pass |
| T-ACTOR-002 | ACTOR-002 | `misc/verifyPtcsActorFabricForeman.fsx` | Integration/Actor | Real PTCS `CommSpaActorFabric` and MessageFabric | Passed on 2026-07-05 14:25 +08:00; verifier checks `ActorFabricBinding`, builds/runs `codex.fs.Tests`, starts real PTCS ActorFabric on LAN host, spawns Foreman/Worker actors and confirms both appear as `agent` participants through MessageFabric listing | Pass |
| T-ACTOR-003 | ACTOR-003 | `misc/verifyActorRuntimeArtifactProvider.fsx` | Integration/Actor/E2E | Real PTCS `CommSpaActorFabric`, MessageFabric, installed Agy and file artifact store | Passed on 2026-07-05 15:20 +08:00; WorkerActor invokes shared PTCS runtime cycle, writes artifacts, sends reply with manifest/final refs and returns `RuntimeCycleCompleted` | Pass |
| T-WEBR-006 | WEBR-006 | `misc/verifyAiIntentControls.fsx`; Playwright PTCS webshell evidence | Browser/Integration | Real PTCS shell extension controls | Passed on 2026-07-05 15:05 +08:00; target/perspective/engine/model/reasoning/invocation controls emit normalized `codex.fs.web.ai-intent.v1` metadata through PTCS append APIs; browser never renders CLI argv | Pass |
| T-WEBR-007 | WEBR-007 | `misc/verifyArtifactRefsInPtcsShell.fsx` | Browser/E2E | Real worker run artifact refs | Passed on 2026-07-05 14:18 +08:00; PTCS shell renders redacted reply, run id, manifest/final/note refs from real ACTOR-003 artifacts; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr007\webr007-artifact-refs.png` | Pass |
| T-WEBR-008 | WEBR-008 | `misc/verifyNoStandaloneChatProductPath.fsx` | Regression/Browser | Real host routes | Passed on 2026-07-05 13:03 +08:00; verifier builds/runs `codex.fs.Tests` and asserts control-only `/chat` guard has no composer/form/PTCS manifest, diagnostics is diagnostic-only and product path guidance points to `web.profile=ptcs-webshell` | Pass |
| T-E2E-004 | E2E-004 | `misc/verifyPtcsAiChatE2E.fsx` | Browser/E2E | Real PTCS shell + ActorFabric + MessageFabric + headless engine | Passed on 2026-07-05 14:15 +08:00; Playwright sent a prompt from PTCS `/chat` composer to Foreman, Foreman actor consumed it through MessageFabric, ran installed Agy, persisted manifest/final/note artifacts, and rendered `codexfs-artifact-reply` in the same PTCS thread; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\e2e004\e2e004-ptcs-ai-chat.png` | Pass |
| T-WEBR-010 | WEBR-010 | `misc/verifyAiIntentOutputProjection.fsx` | Browser/E2E | Real PTCS append page + MessageFabric reply projection | Passed on 2026-07-06 09:21 +08:00 and live 18488 on 2026-07-06 09:42 +08:00; Playwright sent Codex Foreman prompts from `/page/codexfs-ai-chat`, verified same-page `codexfs-ai-output-message` with token/date/artifact refs, rejected raw JSON-only display and checked Send/output non-overlap. Live screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection-live18488.png` | Pass |

## Hard Gates

- Browser tests must use real Playwright/Chrome against a real host profile.
- UI acceptance must inspect visible PTCS classic shell elements, including tabs, participant list, thread area and composer.
- Actor tests must use PTCS ActorFabric/MessageFabric, not a fake mailbox.
- ACTOR-003 must invoke the runtime cycle from a WorkerActor, not call host-only helper code directly from the verifier.
- No test in this group may use standalone `GET/POST /diagnostics/session-send` as product chat acceptance.
- No test in this group may write raw prompt/stdout/stderr into public chat body.

## Evidence Paths

Future verifier evidence must record absolute paths for screenshots, Playwright summaries, generated bundle files, host logs and artifact manifests.

WEBR-006 evidence:

- Desktop screenshot: `G:\codex.fs\log\20260705\webr006-host8-desktop-after-send.png`
- Desktop geometry: `G:\codex.fs\log\20260705\webr006-host8-desktop-geometry.json`
- Mobile screenshot: `G:\codex.fs\log\20260705\webr006-host8-mobile-after-send.png`
- Mobile geometry: `G:\codex.fs\log\20260705\webr006-host8-mobile-geometry.json`
- Append request body: `G:\codex.fs\log\20260705\webr006-host8-append-request-186.body.json`
- Console/network summaries: `G:\codex.fs\log\20260705\webr006-host8-console-all.md`, `G:\codex.fs\log\20260705\webr006-host8-network-after-send.md`

WEBR-007 evidence:

- Browser screenshot: `G:\codex.fs\src\codex.fs\.playwright-mcp\webr007\webr007-artifact-refs.png`
- Real actor manifest: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\manifest.json`
- Real actor final: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\final.md`
- Real actor note: `G:\codex.fs\src\codex.fs\.codex.fs\actor003-artifacts\actor003-a4ab9da1154c\sessions\actor003-a4ab9da1154c\runs\run-20260705054839745-bb6f3f50\note.md`

E2E-004 evidence:

- Browser screenshot: `G:\codex.fs\src\codex.fs\.playwright-mcp\e2e004\e2e004-ptcs-ai-chat.png`
- Prompt token: `CODEXFS_E2E004_25765348a165`
- Real Foreman manifest: `G:\codex.fs\src\codex.fs\.codex.fs\e2e004-artifacts\e2e004-25765348a165\sessions\foreman\runs\run-20260705060552217-1daa3715\manifest.json`
- Real Foreman final: `G:\codex.fs\src\codex.fs\.codex.fs\e2e004-artifacts\e2e004-25765348a165\sessions\foreman\runs\run-20260705060552217-1daa3715\final.md`
- Real Foreman note: `G:\codex.fs\src\codex.fs\.codex.fs\e2e004-artifacts\e2e004-25765348a165\sessions\foreman\runs\run-20260705060552217-1daa3715\note.md`
