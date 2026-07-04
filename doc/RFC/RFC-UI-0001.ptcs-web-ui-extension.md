# RFC-UI-0001 PTCS Web UI Extension for codex.fs

ID：`RFC-UI-0001`  
狀態：Accepted for UI-001 RFC slice  
日期：2026-07-04  
關聯 WBS：`UI-001`  
關聯 Test：`T-UI-001`

## 背景

`codex.fs` 已有 terminal-to-host-to-PTCS-MessageFabric-to-engine 的 first real path。後續 Web UI 不應重新建立 chat store、actor fabric、message transport 或 session state，而應使用 PTCS 既有 extension 與 fabric。

本 RFC 依據：

- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\Requirement.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\SA.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\SD.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-PTC-SPA-0006.dynamic-client-extension-points.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-PTC-SPA-0008.unified-sdui-target-extension-contract.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-PTC-SPA-0010.actors-page-dynamic-dsl-rendering.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0001.shared-sharded-message-fabric-contract.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0002.external-actor-system-attachment.zh-Hant.md`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc\RFC-SPA-UPSTREAM-0003.shared-durable-ingress-fabric-contract.zh-Hant.md`

## 目標

1. 定義 codex.fs Web UI 作為 PTCS extension consumer 的邊界。
2. 保持 PTCS `CommSpaMessageFabric` 是 user/agent/session mailbox 的 canonical source。
3. 讓 Web UI 操作與 `codex.fs.cli` 使用同一個 host/control/session contract。
4. 使用 PTCS client extension manifest、same-origin script asset、registered JSON POST handler、page renderer/fallback pattern。
5. 確保 clustered/runtime profile 不使用 `localhost` / `127.0.0.1` 當成跨節點或跨 actor 的 advertise contract。

## 非目標

1. 本 RFC 不實作 PTCS Web UI。
2. 本 RFC 不修改 `G:\PulseTrade2.fs` source。
3. 本 RFC 不新增 fake/mock UI smoke。
4. 本 RFC 不新增另一套 MessageFabric、ActorFabric、WebSocket transport、HTTP chat endpoint 或 browser-local truth。
5. 本 RFC 不把 codex.fs host HTTP control endpoint 當成 MessageFabric transport。

## 使用情境

### C1. Human sends prompt from PTCS UI

1. Human 在 PTCS Web UI 選擇或建立 codex.fs session participant。
2. UI 使用 PTCS MessageFabric-backed path 送出 prompt。
3. Session worker 透過 MessageFabric inbox 收到訊息。
4. Session worker 執行 engine cycle，保存 artifacts。
5. Reply 以 MessageFabric message 或 result/artifact reference 回到 PTCS UI。

### C2. UI reads host/control health

1. UI 需要顯示 host health、OpenAPI/Swagger link、artifact root policy 或 enabled engines。
2. UI 只能使用 host profile 已宣告的 advertised control URI。
3. 若需要 browser same-origin，PTCS host 必須註冊固定 allow-list 的 same-origin JSON handler，不允許使用者任意輸入 URL、header、token 或 script。

### C3. UI renders artifact reference

1. MessageFabric body 顯示 redacted summary 與 artifact manifest reference。
2. UI 可提供 manifest link/status panel。
3. UI 不直接把 raw transcript/stdout/stderr 全量塞入 chat body。

## 決策

### D1. Extension registration

codex.fs UI 應以 PTCS client extension 形式掛入：

```fsharp
ExtensionId = "codex-fs-session-ui"
DisplayName = "codex.fs Sessions"
ScriptUrls = [ "/client-extensions/codexfs/CodexFs.PtcsUi.js" ]
AppendPageShapes = [ "codex-fs-session" ]
```

實際 API 名稱可依 PTCS package 版本調整，但必須映射到 `CommHub.RegisterClientExtension` 與 `RegisterClientExtensionScriptAsset` 既有 seam。

### D2. Canonical communication path

UI prompt/send/reply truth 必須走 PTCS MessageFabric：

```text
PTCS UI participant
  -> CommSpaMessageFabric direct/group send
  -> codex.fs session participant inbox
  -> Session worker engine cycle
  -> CommSpaMessageFabric reply with artifact reference
```

UI 不直接寫 artifact store，不直接呼叫 engine process，也不維護自己的 chat history。

### D3. Host control path

Host control remains HTTP control plane only：

```text
PTCS UI status widget
  -> allowed codex.fs control advertise URI or same-origin registered JSON handler
  -> codex.fs.host health/session endpoint
```

Rules：

- Clustered/non-dev profile must advertise a LAN/DNS-reachable URI.
- `localhost` / `127.0.0.1` is allowed only for explicit single-node dev profile.
- Same-origin handler must be allow-list based and registered by host/extension, not a generic proxy.
- Swagger/OpenAPI links come from `HostControlContract.OpenApiJsonUri` / `SwaggerUiUri`.

### D4. UI implementation package boundary

The preferred future package shape is:

| Package | Purpose |
| --- | --- |
| `codex.fs.host` | Referenceable host/control/session library |
| `codex.fs.host.tool` | Dotnet tool command `codex.fs.host` |
| `codex.fs.ptcs.ui` or equivalent future package | PTCS WebSharper extension registration and browser bundle |

The exact UI package name remains a future WBS decision. It must not force PTCS core to reference codex.fs.

### D5. Fallback and failure

- Extension absent：PTCS built-in chat / append UI remains available。
- codex.fs host unavailable：UI shows controlled non-secret host status error。
- renderer failure：PTCS fallback UI remains available。
- unknown session：UI can create/send to the derived session participant through MessageFabric; it must not silently create an engine run outside MessageFabric。

## 影響範圍

- `codex.fs` docs define UI integration contract.
- Future codex.fs UI package will consume PTCS extension seams.
- PTCS core implementation is not changed by this RFC.
- Host HTTP/OpenAPI docs remain authoritative for control endpoints.

## 驗收

UI implementation WBS must add real-path verifier. Planned verifier characteristics:

| Verifier | Requirement |
| --- | --- |
| `TC-UI-001A` extension manifest | PTCS host page includes registered `codex-fs-session-ui` manifest/script URL from same-origin route. |
| `TC-UI-001B` MessageFabric send | Browser UI sends prompt through PTCS MessageFabric to session participant; no parallel chat store. |
| `TC-UI-001C` engine reply view | Session worker reply appears in PTCS UI with artifact manifest reference. |
| `TC-UI-001D` host status | UI status uses advertised LAN/DNS control URI or registered same-origin allow-list handler, not localhost. |
| `TC-UI-001E` fallback | Extension absent or renderer failure leaves PTCS fallback UI usable and logs non-secret diagnostics. |

This RFC slice is accepted when:

- RFC exists and links PTCS source documents.
- WBS/Test/DevLog reference `UI-001`.
- No fake/mock UI verifier is added.

## 關聯文件

- `doc/WBS.md` row `UI-001`
- `doc/Test.md` row `T-UI-001`
- `doc/SD.md` implementation sequence item 12
- `doc/Verification.md`
