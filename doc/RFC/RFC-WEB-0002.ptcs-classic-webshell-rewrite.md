# RFC-WEB-0002 PTCS Classic Webshell Rewrite

ID：`RFC-WEB-0002`  
狀態：Accepted for WEBR-001 reset slice  
日期：2026-07-05  
關聯 WBS：`WEBR-001`  
關聯 Test：`T-WEBR-001`

## 背景

`WEB-001` 定義了 `codex.fs.web` 應作為 PTCS WebSharper bundle，但仍不足以約束 `codex.fs.host` 起來後的 browser surface。實際結果是 `codex.fs.host` 只有 ASP.NET control/diagnostics routes，`/chat` 是 guard page，沒有 PTCS classic chat room：上方 tabs、左側 participant list、右側 chat session/thread/composer。

這與產品需求不符。使用者期待的是：

- PTCS 已有的 classic chat shell 不要重寫；
- `PulseTrade.Comm.Spa.Dynamic` 這種 WebSharper Bundle pattern 要作為 codex.fs Web implementation baseline；
- PTCS Host 沒有的 AI 功能才由 codex.fs 補上；
- AI 功能要落在 PTCS MessageFabric / ActorFabric / SessionActor / WorkerActor / runtime prompt-loop，而不是 browser-local chat 或 diagnostics form。

本 RFC 將目前錯誤方向打掉重排。

## 參考實作事實

Dynamic bundle baseline：

- `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src\PulseTrade.Comm.Spa.Dynamic.fsproj`
- `<WebSharperProject>Bundle</WebSharperProject>`
- `<WebSharperBundleOutputDir>wwwroot\js</WebSharperBundleOutputDir>`
- `<WebSharperRunCompiler>true</WebSharperRunCompiler>`
- `PackageReference Include="PulseTrade.Comm.Spa" Version="[0.2.5-beta71]"`
- server extension files under `Server/` and browser bundle files under `Client/`。

PTCS classic shell baseline：

- `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host`
- routes include `/chat`, `/sets`, `/actors`。
- `/chat` has tab/nav shell, participant list, chat work area, thread list and composer。
- extension seam uses `CommHub.RegisterClientExtension`, `RegisterClientExtensionScriptAsset`, `RegisterClientExtensionJsonPostHandler` and Dynamic-style `useDynamicSdui` loading。

## 目標

1. Make the product Web surface PTCS classic chat shell plus codex.fs AI extension, not a standalone ASP.NET page.
2. Define the cut list for unusable/misleading current web pieces.
3. Define a large rewrite WBS that starts from PTCS Host/Dynamic patterns instead of inventing a new UI.
4. Define AI behavior as ActorFabric/MessageFabric/runtime behavior: Foreman/SessionActor, WorkerActor, headless CLI invocation, artifacts/notes/reply refs.
5. Define real browser and real PTCS fabric acceptance gates.

## 非目標

1. 不重寫 PTCS classic chat shell、participant list、thread composer、tabs 或 `/chat/api/*` chat endpoints。
2. 不把 `codex.fs.host` standalone `/chat` guard、diagnostics form 或 Swagger UI 當產品 Web UI。
3. 不用 fake/mock/in-memory browser mailbox 作為驗收。
4. 不在 browser 直接拼 prompt、直接跑 headless CLI 或直接寫 artifact store。
5. 不新增平行 ActorFabric、MessageFabric、browser-local chat store 或 cursor/ack registry。

## Cut List

以下項目不得再作為產品 Web acceptance：

| Current piece | Decision |
| --- | --- |
| `codex.fs.host` `GET /chat` guard page | Cut from product UI path. It may remain only as legacy redirect/guard until removed. |
| `GET/POST /diagnostics/session-send` | Keep only as ops/debug control page, never as user chat UX. |
| `HOST-006` standalone chat PoC | Historical only; must not be revived. |
| `HOST-007` `/chat` guard as "alignment" | Insufficient; superseded by this RFC for Web implementation. |
| Browser-local participant/thread store invented by codex.fs | Forbidden. Use PTCS shell and MessageFabric-backed APIs. |
| Fake/mock UI smoke | Forbidden as acceptance. |

## Reuse List

The rewrite must reuse:

- PTCS classic shell `/chat` layout and routes。
- PTCS participant list and thread APIs, e.g. `/chat/api/agents`, `/chat/api/thread`, `/chat/api/send` or their current package equivalent。
- PTCS WebSocket/sync path, e.g. `/sync/ws` `chat-send` where available。
- PTCS `CommHub` extension registration and script asset registration。
- PTCS `CommSpaMessageFabric` for public/direct/group message truth。
- PTCS `CommSpaActorFabric` for sharded actor hosting/attachment。
- Dynamic bundle project pattern from `PulseTrade.Comm.Spa.Dynamic`。

## Target Architecture

```text
PTCS classic webshell
  -> /chat nav tabs + participant list + thread + composer
  -> codex.fs WebSharper bundle useAIChat(...)
  -> PTCS CommHub extension manifest/assets/json handlers
  -> PTCS CommSpaMessageFabric public/direct/group
  -> codex.fs.actor SessionActor / WorkerActor over CommSpaActorFabric
  -> codex.fs.runtime prompt loop
  -> Codex/Agy headless CLI
  -> transcript/note/artifact store
  -> MessageFabric reply with redacted refs
  -> PTCS classic shell renders reply/artifact controls
```

`codex.fs.host` must support two explicit modes:

| Mode | Contract |
| --- | --- |
| `control-only` | HTTP health/OpenAPI/diagnostics. No product Web claim. |
| `ptcs-webshell` | Hosts or composes PTCS classic chat shell and registers codex.fs AI bundle. This is the only acceptable product Web mode. |

If a deployment uses an existing PTCS Host process, codex.fs must integrate as package/bundle/actor participant inside that PTCS process or a peer PTCS cluster node. If `codex.fs.host.tool` claims to start a product Web site, it must start `ptcs-webshell` mode, not control-only mode.

## ActorFabric AI Boundary

PTCS Host does not own headless Codex/Agy execution. codex.fs adds that through actors:

- Foreman/SessionActor registers as PTCS `agent` participant and is visible in the PTCS participant list。
- WorkerActor registers as PTCS `agent` participant after spawn。
- Human public/direct/group messages remain MessageFabric messages。
- Actors poll/wait according to session policy and call runtime。
- Runtime assembles prompt/history, invokes engine, persists evidence and emits reply intent。
- Actor sends reply/result refs through MessageFabric and acks only after ready-to-ack evidence。

## WBS Reset

`WEBR-001` accepts this RFC and creates the large rewrite backlog. Implementation starts from these leaf items:

| WBS | Purpose |
| --- | --- |
| `WEBR-002` | PTCS classic shell/Dynamic baseline inventory and API map. |
| `WEBR-003` | Create `codex.fs.web` WebSharper Bundle project. |
| `WEBR-004` | Create `useAIChat(...)` CommHub registration/server extension. |
| `WEBR-005` | Add `ptcs-webshell` product host mode or compose into PTCS Host. |
| `RUNTIME-002` | Extract/complete reusable runtime prompt-loop boundary. |
| `ACTOR-002` | Implement ActorFabric Foreman/Worker proof with visible participants. |
| `WEBR-006` | Add AI target/perspective/invocation controls to PTCS shell. |
| `WEBR-007` | Render artifact/note refs in PTCS shell. |
| `WEBR-008` | Remove/deprecate standalone web-chat product path and enforce guard/redirect semantics. |
| `E2E-004` | Real browser PTCS AI chat end-to-end acceptance. |

## Implementation Notes

2026-07-05 WEBR-006 update:

- AI target/perspective/engine/model/reasoning/invocation/approval controls are now implemented as a PTCS append-input renderer in `codex.fs.web`, not as a standalone browser chat form.
- Browser intent payload is `codex.fs.web.ai-intent.v1`; it is appended through PTCS `/pages/api/append` and defaults to Foreman `agent.codexfs.foreman`, engine `agy`, reasoning `high`, invocation `exec`.
- Real browser evidence on `http://10.28.112.93:18488/page/webr006-ai8` is stored under `G:\codex.fs\log\20260705\webr006-host8-*`.
- `ptcs-webshell` deployments need a dedicated `web.pcslRoot` and copied PTCS package `build/**` assets. Current composition still returns 503 for `/sync/ws`; HTTP fallback APIs passed WEBR-006, but E2E/production acceptance must close the WebSocket route and artifact/note rendering gap.

## 驗收

This RFC slice is accepted when:

1. Requirement/SA/SD/WBS/Test/KM/DevLog state that current standalone `codex.fs.host` web is not the product UI.
2. WBS contains rewrite leaf items with corresponding Test items.
3. Test docs require real PTCS classic shell/browser/fabric evidence.
4. Old standalone chat/diagnostics paths are explicitly cut from product acceptance.

Future implementation is accepted only when Playwright verifies:

- `/chat` renders PTCS classic nav tabs, participant list, thread area and composer。
- Foreman/worker agents are visible in participant list。
- public/direct/group send flows through real PTCS MessageFabric。
- a SessionActor/WorkerActor over PTCS ActorFabric invokes runtime/headless engine。
- reply includes redacted summary plus manifest/note refs。
- no standalone `codex.fs.host` diagnostics form is used as the product path。

## 關聯文件

- `doc/WBS.WEBR-001.md`
- `doc/Test.WEBR-001.md`
- `doc/RFC/RFC-WEB-0001.ptcs-ai-chat-bundle.md`
- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/RFC/RFC-ACTOR-0001.session-worker-actor-model.md`
- `doc/RFC_Project_Planing.md`
