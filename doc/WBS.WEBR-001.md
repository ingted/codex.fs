# WBS Detail: WEBR-001 PTCS classic webshell rewrite

WBS ID：`WEBR-001`  
狀態：Done for RFC/reset slice  
Progress：100  
StartTime：2026-07-05 10:30 +08:00  
UpdatedAt：2026-07-05 10:30 +08:00  
Previous：`WEB-001`, `ACTOR-001`, `PERSIST-001`  
SD：`SD §9`, `SD §14.1`, `SD §14.3`  
Test：`T-WEBR-001`

## Scope

本 detail 是「大型打掉重寫」的 side-by-side WBS。它不宣稱 implementation 已完成；它把現有不能作為產品 UI 的 standalone web/diagnostics 路線切掉，並把後續工作重新排回 PTCS classic chat shell + WebSharper Bundle + ActorFabric AI worker 主線。

## Reset Decision

- `codex.fs.host` control-only web is not product UI.
- Product Web must show PTCS classic `/chat`: tabs/nav shell, participant list, chat thread/session area and composer.
- `codex.fs.web` must be a WebSharper Bundle project modeled after `PulseTrade.Comm.Spa.Dynamic`.
- PTCS Host missing AI controls are codex.fs extension work; PTCS existing chat shell and fabric must be reused.
- AI execution belongs to ActorFabric/SessionActor/WorkerActor/runtime, not browser code or diagnostics route.

## Rewrite Leaf WBS

| ID | Work item | Previous | Progress | Status | Blocker | SD item | Test item | Verifier |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- |
| WEBR-002 | PTCS classic shell and Dynamic bundle baseline inventory | WEBR-001 | 100 | Done | None | SD §14.3 | T-WEBR-002 | `misc/verifyPtcsClassicShellInventory.fsx`; [inventory](WEBR-002.PTCS-classic-shell-inventory.md) |
| WEBR-003 | Create `codex.fs.web` WebSharper Bundle project | WEBR-002 | 0 | Planned | None | SD §14.3 | T-WEBR-003 | `misc/verifyCodexFsWebBundle.fsx` |
| WEBR-004 | Implement `useAIChat(...)` CommHub registration/server extension | WEBR-003 | 0 | Planned | WEBR-003 bundle scaffold | SD §14.3 | T-WEBR-004 | `misc/verifyUseAIChatRegistration.fsx` |
| WEBR-005 | Add product `ptcs-webshell` host mode or PTCS Host composition path | WEBR-004 | 0 | Planned | WEBR-004 registration | SD §9, §14.3 | T-WEBR-005 | `misc/verifyHostPtcsWebProfile.fsx` |
| RUNTIME-002 | Extract/complete reusable runtime prompt-loop modules | RUNTIME-001;PERSIST-001 | 0 | Planned | None | SD §11.3, §12 | T-RUNTIME-002 | `misc/verifyRuntimeLoopExtraction.fsx` |
| ACTOR-002 | Implement PTCS ActorFabric Foreman/Worker proof | ACTOR-001;RUNTIME-002 | 0 | Planned | RUNTIME-002 | SD §11.2, §14.3 | T-ACTOR-002 | `misc/verifyPtcsActorFabricForeman.fsx` |
| WEBR-006 | Add AI target/perspective/invocation controls in PTCS shell | WEBR-004;ACTOR-002 | 0 | Planned | ACTOR-002 visible participants | SD §14.2, §14.3 | T-WEBR-006 | `misc/verifyAiIntentControls.fsx` |
| WEBR-007 | Render artifact/note refs in PTCS shell | WEBR-006;PERSIST-001 | 0 | Planned | runtime artifact provider | SD §12, §14.3 | T-WEBR-007 | `misc/verifyArtifactRefsInPtcsShell.fsx` |
| WEBR-008 | Remove/deprecate standalone web-chat product path | WEBR-005 | 0 | Planned | product web profile exists | SD §9, §14.3 | T-WEBR-008 | `misc/verifyNoStandaloneChatProductPath.fsx` |
| E2E-004 | Real PTCS classic browser AI chat E2E | WEBR-006;WEBR-007;ACTOR-002 | 0 | Planned | all implementation slices | SD §14.3 | T-E2E-004 | `misc/verifyPtcsAiChatE2E.fsx` |

## Cut / Rewrite Notes

- `HOST-006` remains historical alpha evidence only.
- `HOST-007` is insufficient for product Web and is superseded by `WEBR-001` for implementation planning.
- `/diagnostics/session-send` remains allowed only as ops/debug control.
- Any implementation that does not render PTCS classic chat shell fails `WEBR-005` and `E2E-004`.

## Definition Of Done

`WEBR-001` is done when:

- `RFC-WEB-0002` exists and is accepted for reset slice.
- The rewrite WBS above exists with corresponding Test items.
- Requirement/SA/SD explicitly mark product Web as PTCS classic shell + codex.fs extension.
- DevLog/KM capture the reset.

Each implementation leaf item remains Planned until its real verifier exists and passes.

## WEBR-002 Closeout

UpdatedAt：2026-07-05 10:47 +08:00

- Source/API inventory is recorded in `doc/WEBR-002.PTCS-classic-shell-inventory.md`.
- Verifier `dotnet fsi --exec .\misc\verifyPtcsClassicShellInventory.fsx` passed on 2026-07-05 10:48 +08:00 and checks real PTCS package, PTCS Host, Dynamic bundle and codex.fs host cut-list source paths.
- `WEBR-003` is unblocked: create `codex.fs.web` as a WebSharper Bundle project with exact PTCS `[0.2.5-beta71]` reference and generated assets under `wwwroot/js`.
