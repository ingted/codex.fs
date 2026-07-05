# RFC Project Planing

狀態：Draft
日期：2026-07-05
範圍：`G:\codex.fs\src\codex.fs`

> 檔名沿用 user 指定的 `RFC_Project_Planing.md`。本文件先做 project inventory / planning，不直接取代正式 `doc/RFC/RFC-*.md` 流程。

## 1. 目的

目前 alpha implementation 已經證明一些低階元件可用，但產品方向需要重置：

- `codex.fs.host` 不是 PTCS Host，也不應只是 HTTP diagnostics shell。
- `codex.fs.host` 應成為類似 Codex CLI runtime 的 multi-agent host：負責 prompt 拼接、headless CLI invocation、stdio/transcript/note/artifact persistence、local compact 與 actor workflow。
- PTCS Host 是 WebSharper/PTCS hub 宿主；codex.fs Web UI 應以 PTCS extension/bundle 掛入，而不是直接把 PTCS Host 既有 chat room 當成 codex.fs 完成品。
- `codex.fs.cli` 應成為 terminal participant chat client，可切換 target participant / perspective / invocation options，而不是一次性 HTTP send helper。

因此本文件先列出現有 project 與可能新增 project，作為後續 reset RFC / WBS 的 project boundary 草案。

## 2. 現有專案

現有專案來源：`G:\codex.fs\src\codex.fs\src`

| Project | Package / command | Type | Current purpose | Target role after reset | Keep / refactor |
| --- | --- | --- | --- | --- | --- |
| `src/codex.fs/codex.fs.fsproj` | `codex.fs` | library | Core domain, prompt assembly, compaction, artifacts, file artifact store, redaction, host config, engine model, process runner, session model. | 保留為 pure/core contract package。放 domain model、engine invocation contract、artifact/note/transcript model、redaction、prompt/history/compact pure logic。 | Keep, but split if it grows too large. |
| `src/codex.fs.ptcs/codex.fs.ptcs.fsproj` | `codex.fs.ptcs` | library | Thin PTCS integration boundary over `PulseTrade.Comm.Spa`; current MessageFabric and durable binding wrappers. | 保留為 PTCS adapter package。負責 `CommSpaMessageFabric` / durable fabric / participant registration / MessageFabric DTO mapping，不放 business workflow。 | Keep and expand carefully. |
| `src/codex.fs.host/codex.fs.host.fsproj` | `codex.fs.host` | library | Minimal host runtime, HTTP control endpoints, bounded single-cycle engine helper. | 需要大幅 refactor 成 codex.fs runtime host package：SessionActor/WorkerActor orchestration, headless CLI invocation loop, stdio capture, notes/artifacts, local compact, PTCS participant lifecycle。HTTP 只作 control/docs/diagnostics。 | Refactor heavily. |
| `src/codex.fs.cli/codex.fs.cli.fsproj` | `codex.fs.cli` / command `codex.fs.cli` | dotnet tool | Terminal command surface for host/session control; current alpha supports host status, send/status/attach/drain. | 需要重做成 interactive terminal participant chat client：可選 participant、切換視角、設定 model/reasoning/invocation args、attach stream、瀏覽 notes/artifacts。 | Refactor heavily. |
| `src/codex.fs.tool/codex.fs.tool.fsproj` | `codex.fs.tool` / command `codex.fs` | dotnet tool alias | Short command alias over the same CLI code. | 保留為 short alias package，或在 CLI 穩定後只包裝 `codex.fs.cli`。不可分叉出不同 behavior。 | Keep as thin alias. |
| `src/codex.fs.host.tool/codex.fs.host.tool.fsproj` | `codex.fs.host.tool` / command `codex.fs.host` | dotnet tool | Standalone host tool wrapper for `codex.fs.host`; starts HTTP control endpoint. | 保留為 standalone dev/ops host entrypoint。正式 PTCS deployment 仍應由 PTCS Host 或 peer cluster node reference `codex.fs.host` package。 | Keep as thin wrapper. |
| `src/codex.fs.web/codex.fs.web.fsproj` | `codex.fs.web` | WebSharper bundle package | PTCS AI chat extension bundle baseline: exact `PulseTrade.Comm.Spa [0.2.5-beta71]`, generated `wwwroot/js`, `CommHub.useAIChat(...)` registration seam. | 擴充成 PTCS classic `/chat` 裡的 AI controls：participant switch, Foreman/worker chat, public/direct/group, model/reasoning/invocation controls, artifacts/notes viewer。 | Keep and expand through WEBR WBS. |

## 3. 可能新增專案

| Proposed project | Type | Purpose | Primary consumers | Why separate |
| --- | --- | --- | --- | --- |
| `codex.fs.protocol` | library | 定義跨 CLI/Web/Host/Actor 共用 DTO：participant id, chat target, invocation profile, model/reasoning options, run status, artifact/note refs, streaming events。 | `codex.fs.cli`, `codex.fs.host`, WebSharper bundle, tests. | 避免 CLI/Web 直接依賴 host implementation，也避免 DTO 混在 runtime code 裡。 |
| `codex.fs.runtime` | library | Prompt/history assembly orchestration, run state machine, headless invocation request planning, transcript/note/artifact persistence boundary, local compact orchestration。 | `codex.fs.host`, `codex.fs.actor`, tests. | 把非-Akka、非-HTTP 的核心 runtime 抽出，讓 actor 與 single-cycle verifier 共用。 |
| `codex.fs.actor` | library | Akka.NET sharded cluster actors：`SessionActor` / `WorkerActor` / spawn protocol / durable delivery / mailbox accumulation / participant lifecycle。 | `codex.fs.host`, PTCS Host integration node. | ActorFabric/MessageFabric workflow 是產品核心，應獨立於 HTTP host 與 CLI。 |
| `codex.fs.persistence` | library | Notes/transcript/artifact persistence abstraction：prompt+reply stdio capture, note file layout, append-only transcript, local compact snapshot, recovery boundary。 | `codex.fs.runtime`, `codex.fs.actor`, `codex.fs.host`. | 現在 artifact store 過薄；未來要能替換 local file / durable store / encrypted config。 |
| `codex.fs.engines` | library | Codex/Agy/versioned headless CLI adapters, FAkka.Argu DU, capability probing, argv rendering, stdout/stderr/event parsing。 | `codex.fs.runtime`, `codex.fs.host`, tests. | 若 core `codex.fs` 過大，可把 version-specific CLI adapter 從 core 分出。 |
| `codex.fs.web.server` | library or bundled server extension | 將 `codex.fs.web` 註冊到 PTCS `CommHub`：client extension metadata, same-origin JSON handlers, allowed host control endpoints, dynamic Argu metadata for invocation forms。 | PTCS Host integration. | 若 WebSharper bundle 需要 server-side registration/API handlers，避免全部塞進 `codex.fs.web` client bundle。 |
| `codex.fs.sdk` | library | 給第三方 host 或 scripts 使用的 high-level client SDK：register participant, send message, attach transcript, query artifacts/notes, select invocation profile。 | External apps, F# scripts, tests. | 比直接呼叫 HTTP/MessageFabric 低階 API 穩定，方便 NuGet users。 |
| `codex.fs.verifiers` | test/support project or scripts package | Real-path verifiers：PTCS Host web bundle, CLI interactive flow, actor spawn/register, headless codex stdio persistence, recovery/local compact。 | CI/dev validation. | 避免把大型 E2E 驗證塞入 unit tests；可保留真路徑驗收腳本。 |

## 4. 外部宿主與參考專案

| External project/path | Role for codex.fs | Notes |
| --- | --- | --- |
| `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host` | 現有 PTCS Host service / WebSharper runtime / auth profile / chat hub 宿主。 | codex.fs Web UI 應掛入這類 host；PTCS Host 不會自動做 headless Codex invocation 或 stdio/note persistence。 |
| `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa` | PTCS package source：MessageFabric, ActorFabric, WebSharper extension surfaces。 | codex.fs 應 reference package/API，不重做 MessageFabric/ActorFabric。 |
| `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src` | WebSharper bundle/extension pattern reference。 | 可參考 `WebSharperProject=Bundle`, `RegisterClientExtensionScriptAsset`, `RegisterClientExtension`, `useDynamicSdui(...)` pattern，設計 `useAIChat(...)`。 |
| `codex` / `agy` installed CLI | Headless engine executable. | 由 `codex.fs.runtime/engines` 統一建模 invocation options、stdio capture 與 artifact parsing。 |

## 5. 初步分層建議

| Layer | Projects | Responsibility |
| --- | --- | --- |
| Core contracts | `codex.fs`, possible `codex.fs.protocol` | Pure domain, DTO, prompt/history/artifact/note models. |
| Engine adapters | `codex.fs`, possible `codex.fs.engines` | Versioned Codex/Agy CLI options, probing, argv rendering, output parsing. |
| PTCS adapters | `codex.fs.ptcs` | MessageFabric/ActorFabric/durable wrapper and participant mapping. |
| Runtime | possible `codex.fs.runtime`, `codex.fs.persistence` | Codex-like prompt loop, stdio transcript, notes, artifacts, local compact, recovery boundary. |
| Actors | possible `codex.fs.actor` | Sharded SessionActor/WorkerActor workflow and durable message delivery. |
| Host | `codex.fs.host`, `codex.fs.host.tool` | Runtime startup, control endpoints, OpenAPI/Swagger, standalone dev/ops entrypoint. |
| Terminal UI | `codex.fs.cli`, `codex.fs.tool` | Interactive participant chat client and ops commands. |
| Web UI | `codex.fs.web`, possible `codex.fs.web.server` | PTCS WebSharper AI chat extension/bundle. |
| Verification | tests plus possible `codex.fs.verifiers` | Real-path build/test/browser/actor/headless CLI verification. |

## 6. Open Decisions

| Decision | Options | Initial recommendation |
| --- | --- | --- |
| 是否新增 `codex.fs.protocol` | Keep DTO in `codex.fs.host`; split to protocol package. | Split once Web/CLI both need stable DTO without host dependency. |
| 是否新增 `codex.fs.runtime` | Keep in `codex.fs.host`; split runtime logic. | Accepted by `RFC-RUNTIME-0001`: runtime owns prompt-loop orchestration; physical project split happens when actor/verifier/Web consumers need it. |
| Web package name | `codex.fs.web`, `codex.fs.ptcs.web`, `codex.fs.spa` | Prefer `codex.fs.web` for product UI; namespace can expose PTCS extension functions. |
| Server extension split | One `codex.fs.web` package; separate `codex.fs.web.server`. | Start single package if small; split if server registration/API handlers grow. |
| CLI alias | Keep `codex.fs.tool`; merge into `codex.fs.cli`. | Keep alias thin and generated from same source to avoid behavior drift. |
| Engine adapters split | Keep in core; move to `codex.fs.engines`. | Keep short-term; split when versioned Codex/Agy DU grows. |

## 7. Next RFC Candidates

| RFC | Purpose |
| --- | --- |
| `RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md` | Accepted: Product-level reset for PTCS Host vs codex.fs.host vs runtime/CLI/Web/Actor responsibilities. |
| `RFC-RUNTIME-0001.prompt-loop-package-boundary.md` | Accepted: Runtime prompt-loop orchestration, ports/effects, side-effect ordering and migration from bounded host helper. |
| `RFC-ACTOR-0001.session-worker-actor-model.md` | Accepted: Sharded `SessionActor` / `WorkerActor` protocol, spawn/register, participant routing, durable delivery and runtime call boundary. |
| `RFC-WEB-0001.ptcs-ai-chat-bundle.md` | Accepted: WebSharper `useAIChat(...)` bundle design, PTCS Dynamic reference pattern, participant/perspective controls and redacted artifact refs. |
| `RFC-WEB-0002.ptcs-classic-webshell-rewrite.md` | Accepted: reset Web implementation to PTCS classic chat shell plus codex.fs WebSharper Bundle and ActorFabric-backed AI workers; standalone diagnostics/guard pages are cut from product acceptance. |
| `RFC-CLI-0002.interactive-participant-client.md` | Accepted: Terminal chat client UX: Foreman default target, target participant/public/group switching, perspective display and model/reasoning/invocation option handoff. |
| `RFC-PERSIST-0001.transcript-note-artifact-store.md` | Accepted: Prompt/reply/stdio capture, private raw artifacts, redacted run notes, local compact refs and recovery boundaries. |
