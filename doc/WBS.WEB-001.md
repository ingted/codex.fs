# WBS Detail: WEB-001 PTCS AI chat bundle RFC

WBS ID：`WEB-001`  
狀態：Done  
Progress：100  
StartTime：2026-07-05 04:32 +08:00  
UpdatedAt：2026-07-05 04:34 +08:00  
Previous：`PRODUCT-001`, `UI-002`  
SD：`SD §14.1`, `SD §14.2`  
Test：`T-WEB-001`

## Scope

完成 PTCS AI chat bundle 的 RFC 與 current-state docs 同步。此 slice 不實作 WebSharper bundle、不修改 PTCS Host、不宣稱 browser UI 已可用。

## Inputs Read

- `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md`
- `doc/WBS.UI-001.md`
- `doc/WBS.UI-002.md`
- `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc`
- `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src\Server\Extension.fs`
- `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src\Client\ActorDynamicTab.fs`
- `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src\Client\ArguFormRenderer.fs`

## Decision

- `codex.fs.web` is a PTCS WebSharper extension/bundle, not standalone host `/chat`.
- Registration follows PTCS Dynamic pattern: `RegisterClientExtension`, `RegisterClientExtensionScriptAsset`, `RegisterClientExtensionJsonPostHandler`.
- Browser bundle must be WebSharper/F# generated and must not use hand-written JavaScript.
- Web prompt/reply truth remains PTCS `CommSpaMessageFabric`; browser does not own prompt history, artifacts, compact or ack cursor.
- UI supports Foreman default target, exact worker participant, public channel, group id and authorized perspective switching.
- Engine/model/reasoning/invocation controls emit intent metadata only; runtime/actor validates and renders versioned CLI argv.
- Artifact/note view renders redacted summaries and manifest/note refs, not raw prompt/stdout/stderr.

## Deliverables

- `doc/RFC/RFC-WEB-0001.ptcs-ai-chat-bundle.md`
- Updated `doc/Requirement.md`
- Updated `doc/SA.md`
- Updated `doc/SD.md`
- Updated `doc/WBS.md`
- Updated `doc/Test.md`
- Updated `doc/RFC_Project_Planing.md`
- Updated `doc/DevLog.md`
- Updated `MCP.KM.md`

## Verification

- `T-WEB-001` records this RFC slice as passed and keeps implementation verifier `misc/verifyPtcsAiChatBundle.fsx` as planned future work.
- `RFC-WEB-0001` defines real PTCS Host/WebSharper/browser/MessageFabric acceptance and rejects standalone `/chat` or fake/mock UI smoke.
- `check.fsx`, `git diff --check`, sensitive text scan and encoding scan are recorded in the parent `web_bundle_docflow` op_log.

## Blockers

- None for this RFC slice.

## Deferred

- Create `codex.fs.web` WebSharper bundle project.
- Create optional `codex.fs.web.server` registration package if server handlers grow.
- Implement real browser verifier `misc/verifyPtcsAiChatBundle.fsx`.
- Wire PTCS Host reference to the bundle and shared caller-owned fabric.
