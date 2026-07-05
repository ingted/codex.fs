# WEBR-002 PTCS classic shell and Dynamic bundle inventory

WBS ID：`WEBR-002`  
狀態：Done  
Progress：100  
StartTime：2026-07-05 10:45 +08:00  
UpdatedAt：2026-07-05 10:47 +08:00  
Previous：`WEBR-001`  
SD：`SD §14.3`  
Test：`T-WEBR-002`  
Verifier：`misc/verifyPtcsClassicShellInventory.fsx`

## Scope

本文件只做 source/API inventory，不宣稱 Web bundle 或 actor loop 已實作。目的在於把 `codex.fs.web` 的後續實作鎖定在 PTCS 既有 classic shell、`CommHub` extension registry、MessageFabric/ActorFabric integration 上，避免再次做出 standalone HTML chat 或 diagnostics page。

## Evidence Inputs

| Area | Source |
| --- | --- |
| PTCS package | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\PulseTrade.Comm.Spa.fsproj` |
| PTCS server routes | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Server.fs` |
| PTCS client shell | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Client.fs` |
| PTCS `CommHub` registry | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Store.fs` |
| PTCS extension DTOs | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Domain.fs` |
| PTCS MessageFabric | `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\MessageFabric.fs` |
| PTCS Host composition | `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host\Program.fs` |
| Dynamic baseline package | `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src\PulseTrade.Comm.Spa.Dynamic.fsproj` |
| Dynamic extension hook | `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src\Server\Extension.fs` |
| codex.fs cut-list | `G:\codex.fs\src\codex.fs\src\codex.fs.host\HostControl.fs` |

## PTCS Package Shape

`PulseTrade.Comm.Spa` current source package is `0.2.5-beta71`, target `net10.0`, `WebSharperProject=Html`, `WebSharperRunCompiler=true`, and includes Akka Cluster/Sharding, WebSharper, FAkka and PTCS auth/registry packages. This confirms `codex.fs.web` must consume PTCS as a package/extension rather than copy the shell.

## Classic Shell Routes

PTCS `Server.app` already provides the browser IA and APIs required by product Web:

| Route | Purpose |
| --- | --- |
| `/` | redirects to `/chat` |
| `/chat` | classic chat shell page |
| `/sets` | sets shell |
| `/actors` | actor tree shell |
| `/page/<pageId>` | append page shell |
| `/chat/api/agents` | returns fixed public channel plus registered `agent` participants |
| `/chat/api/thread` | returns public/direct chat thread, merged from durable stream and memory snapshot |
| `/chat/api/send` | HTTP fallback send path |
| `/sync/ws` | WebSocket sync path; chat UI sends `chat-send` frames here first |
| `/assets/ptc-comm-spa.css` | classic shell stylesheet |

The route map is in `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\Server.fs`; the browser verifier `Scripts\verify.browserUi.playwright.fsx` already drives `/chat`, waits for `chat-draft`/`chat-participant`, sends through the composer, verifies `thread-list`, and tests pending command replay.

## Classic Shell DOM Contract

PTCS `Client.fs` mounts the classic chat UI with these stable selectors:

| Selector / value | Meaning |
| --- | --- |
| `.page.chat-grid` | main chat page layout |
| `[data-testid='nav-chat']`, `[data-testid='nav-sets']`, `[data-testid='nav-actors']` | classic navigation links generated from route labels |
| `[data-testid='chat-participant']` plus `data-participant-id` | participant/worker target list |
| `[data-testid='chat-work']` | selected thread work area; also carries WebSocket state |
| `[data-testid='chat-pending-state']` | browser pending command state |
| `[data-testid='thread-list']` | rendered message thread |
| `[data-testid='chat-composer']` | composer container |
| `[data-testid='chat-draft']` | prompt/message text area |
| `[data-testid='chat-send']` | send command |

Implementation implication: `WEBR-005` and `E2E-004` must verify these PTCS shell elements in a real browser. A standalone `codex.fs.host` page with a textarea is not acceptable.

## Participant And MessageFabric Seam

PTCS `CommHub` already exposes participant and chat operations:

| Operation | Evidence |
| --- | --- |
| `RegisterParticipant` | visible identities; use `Kind = Some "agent"` for workers |
| `ListParticipants(Some "agent", Some true)` | source of `/chat/api/agents` |
| `SendMessage` | public/direct chat facade |
| `Thread(participantId, peerId, afterMessageId)` | direct/public thread read |
| `CommSpaMessageFabric.RegisterParticipantAsync` | async participant registration |
| `CommSpaMessageFabric.SendAsync` | direct/public/group append |
| `PollInboxAsync` / `WaitInboxAsync` / `AckAsync` / `DrainInboxAsync` | worker inbox loop |

Implementation implication: Foreman/Worker actors must register as PTCS `agent` participants in the same PTCS hub/fabric that serves the browser. The browser should see spawned workers through `/chat/api/agents`; worker conversation truth remains PTCS MessageFabric.

## Client Extension Seam

PTCS core already provides runtime extension registration:

| API | Purpose |
| --- | --- |
| `CommHub.RegisterClientExtension` | browser-visible extension manifest |
| `CommHub.RegisterClientExtensionScriptAsset` | same-origin generated JS asset |
| `CommHub.RegisterClientExtensionJsonPostHandler` | fixed JSON POST handler owned by extension package |
| `ClientExtensionRegistration.ExtensionId` | stable extension id |
| `ClientExtensionRegistration.MetadataJson` | extension-owned metadata |
| `ClientExtensionRegistration.ScriptUrls` | script module list |
| `ClientExtensionRegistration.AppendPageShapes` | custom append-page shapes |
| `AppendPageShapeTemplateRegistration` | server-side default behavior for custom shape |

Implementation implication: `codex.fs.web` should expose a `useAIChat(...)` extension function over `CommHub`, register generated WebSharper assets, and only add fixed JSON handlers such as capabilities/health/artifact summary. It must not create a generic HTTP proxy or a second MessageFabric.

## Dynamic Bundle Baseline

`PulseTrade.Comm.Spa.Dynamic` is the baseline for `codex.fs.web`:

| Requirement | Dynamic evidence |
| --- | --- |
| WebSharper bundle project | `<WebSharperProject>Bundle</WebSharperProject>` |
| generated bundle output | `<WebSharperBundleOutputDir>wwwroot\js</WebSharperBundleOutputDir>` |
| compiler enabled | `<WebSharperRunCompiler>true</WebSharperRunCompiler>` |
| package output | `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` |
| PTCS dependency | `PulseTrade.Comm.Spa [0.2.5-beta71]` |
| WebSharper dependency | `WebSharper.FSharp 10.1.5.674` |
| server extension | `Server\Extension.fs` |
| WebSharper client modules | `Client\DynamicRenderer.fs`, `Client\ArguFormRenderer.fs`, `Client\ActorDynamicTab.fs` |
| no hand-written JS escape hatch | Dynamic source explicitly says browser bundle must be generated by WebSharper/F# |

`useDynamicSdui(...)` is the concrete pattern: it registers JSON POST handlers, creates/uses an actor system, loads generated `wwwroot/js`, registers assets with `RegisterClientExtensionScriptAsset`, and registers a manifest with `RegisterClientExtension`.

## PTCS Host Composition Seam

The current PTCS Host process in `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host` already composes optional Dynamic extensions:

| PTCS Host function | Purpose |
| --- | --- |
| `createExtensionActorFabric` | starts a PTCS extension ActorFabric |
| `serverOptions` | loads optional Dynamic extension API |
| `OptionalClientExtensions.useDynamicSdui` | attaches Dynamic bundle to the PTCS hub |
| `Server.startWithSharing` | starts the PTCS WebSharper shell |
| route print | logs `/chat`, `/sets`, `/actors` URLs |

Implementation implication: product Web can be achieved by adding `codex.fs.web` as another PTCS extension package/composition path. `codex.fs.host` may later offer a `ptcs-webshell` mode, but it must serve the PTCS classic shell, not a standalone HTML clone.

## codex.fs Cut List

Current `codex.fs.host` has useful control APIs and Swagger/OpenAPI, but its browser surface is explicitly not product Web:

| Existing route | Allowed role |
| --- | --- |
| `GET /chat` | guard page saying "Use PTCS chat" |
| `GET/POST /diagnostics/session-send` | standalone diagnostics/control only |
| `/api/codexfs/*` | control APIs for CLI/ops/tests |
| `/openapi/v1.json`, `/docs` | API documentation |

Implementation implication: `WEBR-008` must keep this separation. Reintroducing a prompt composer under standalone `codex.fs.host` `/chat` is a regression.

## Decision For Next Work

`WEBR-003` should create `src/codex.fs.web` as a WebSharper Bundle project with:

- exact `PulseTrade.Comm.Spa [0.2.5-beta71]`;
- `WebSharper.FSharp 10.1.5.674`;
- generated assets under `wwwroot/js`;
- server/client F# split, no hand-written JavaScript;
- a minimal compile/build verifier proving bundle output exists.

`WEBR-004` should then implement `useAIChat(...)` over `CommHub`, following the `useDynamicSdui(...)` pattern.

## Verification

`misc/verifyPtcsClassicShellInventory.fsx` checks the real source paths above for the required route strings, DOM testids, MessageFabric operations, extension registry APIs, Dynamic bundle settings and `codex.fs.host` cut-list markers.
