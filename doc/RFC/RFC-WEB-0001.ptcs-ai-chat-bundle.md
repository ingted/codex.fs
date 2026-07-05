# RFC-WEB-0001 PTCS AI Chat Bundle

ID：`RFC-WEB-0001`  
狀態：Accepted for WEB-001 RFC slice  
日期：2026-07-05  
關聯 WBS：`WEB-001`  
關聯 Test：`T-WEB-001`

> 後續修正：`RFC-WEB-0002` supersedes any interpretation that this bundle RFC is enough for product Web implementation. Product Web must reuse PTCS classic `/chat` shell; `codex.fs.host` control-only web is not acceptable as product chat.

## 背景

`PRODUCT-001` 已將產品責任重設：PTCS Host 擁有 WebSharper chat room / hub / auth profile；`codex.fs.host` 擁有 codex.fs composition/control/docs/deployment；prompt/history 拼接、local compact、headless CLI invocation、stdio capture、notes/artifacts 屬於 runtime/session worker。

因此 codex.fs Web 不能再以 standalone `codex.fs.host` `/chat` 取代 PTCS chat room。它必須像 `PulseTrade.Comm.Spa.Dynamic` 一樣，以 PTCS WebSharper extension/bundle 掛入既有 PTCS Host，並共用同一個 `CommSpaMessageFabric` / `CommSpaActorFabric`。

本 RFC 依據：

- `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host`
- `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa\doc`
- `C:\Users\Administrator\test_gemini\PulseTrade.Comm.Spa.Dynamic\src`
- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md`
- `doc/RFC/RFC-CLI-0002.interactive-participant-client.md`
- `doc/RFC/RFC-PERSIST-0001.transcript-note-artifact-store.md`
- `doc/RFC/RFC-ACTOR-0001.session-worker-actor-model.md`

## 目標

1. 定義 `codex.fs.web` / `useAIChat(...)` 作為 PTCS WebSharper AI chat bundle 的邊界。
2. 讓 Web UI 使用 PTCS MessageFabric public/direct/group scopes 與 worker/session participants 溝通。
3. 支援預設 Foreman/SessionActor、明確 worker participant、public channel、group channel 的 target vocabulary。
4. 支援 authorized perspective switching，用於查看特定 participant 角度的對話，不用來偽造 sender identity。
5. 提供 engine/model/reasoning/invocation option controls，但只送出 intent metadata；runtime/actor 才負責 policy validation 與 argv rendering。
6. 顯示 redacted run summary、manifest/note refs、artifact status；不在 chat body 或 browser store 暴露 raw prompt/stdout/stderr。
7. 定義未來 real PTCS Host / browser / MessageFabric verifier 的驗收門檻。

## 非目標

1. 本 RFC 不實作 WebSharper bundle。
2. 本 RFC 不修改 PTCS Host 或 PTCS core source。
3. 本 RFC 不新增 fake/mock/in-memory UI smoke。
4. 本 RFC 不讓 browser 直接呼叫 Codex/Agy/headless process。
5. 本 RFC 不讓 browser 或 `codex.fs.web` 擁有 chat history、prompt assembly、local compact、artifact store 或 ack cursor truth。
6. 本 RFC 不把 standalone `codex.fs.host` `/chat` 當成產品 Web UI。

## 使用情境

### C1. Human 公頻廣播給 workers

1. Human 在 PTCS Web chat 選 public target。
2. `codex.fs.web` 使用 PTCS public scope 送出 message。
3. Worker actors 依 session policy poll/wait public/group inclusion。
4. Worker reply 仍透過 PTCS MessageFabric 回到 public/direct/group scope，並附 redacted artifact/note refs。

### C2. Human direct 到 Foreman 或 worker

1. Human 預設 target 是 Foreman participant，例如 `agent.codexfs.foreman`。
2. Human 可改成 exact worker participant，例如 `agent.codexfs.worker.<session>.<role>`。
3. Web bundle 將 target metadata 交給 PTCS/host control path；不要求使用者知道 internal Akka entity id。
4. SessionActor/WorkerActor 收到 message 後呼叫 runtime 做 prompt loop。

### C3. 切換 participant perspective

1. Human 選擇 perspective，例如 Foreman、某 worker、或自己的 user participant。
2. UI 以 authorized read/render policy 切換可視 message set、sender/target labels、artifact refs。
3. Perspective 不等於 impersonation；send identity 仍是目前登入 user participant，除非未來明確實作受控 delegation。

### C4. 設定 invocation options

1. Human 在 UI 選 engine、model、reasoning effort、sandbox/approval profile、其他 CLI-specific options。
2. Web bundle 送出 normalized intent metadata。
3. Runtime/actor 根據 policy 與 engine adapter capability 決定是否接受，並由 versioned Codex/Agy adapter render argv。
4. 被拒絕的 option 以 MessageFabric reply 或 host control error 顯示，不能讓 browser 自行拼 argv。

### C5. 查看 artifacts/notes

1. Worker reply 包含 redacted summary 與 manifest/note refs。
2. UI 顯示 run status、manifest summary、note link、stdout/stderr availability。
3. raw artifacts 仍受 persistence policy 與 host/ops authorization 控制。

## 決策

### D1. Package and registration boundary

目標 package 名稱為 `codex.fs.web`。若 server-side registration/handler 成長，才拆出 `codex.fs.web.server`。初版設計應對應 PTCS Dynamic pattern：

```text
PTCS Host / CommHub
  -> useAIChat(options, runtime/control registration)
  -> RegisterClientExtension(...)
  -> RegisterClientExtensionScriptAsset(...)
  -> RegisterClientExtensionJsonPostHandler(...)
  -> WebSharper bundle
```

建議 extension metadata：

```fsharp
ExtensionId = "codex-fs-ai-chat"
DisplayName = Some "codex.fs AI Chat"
AppendPageShapes =
  [ { Shape = "codexfs-ai-chat"
      Label = Some "AI Chat"
      Badge = Some "ai"
      ClassName = Some "codexfs-ai-chat" } ]
MetadataJson = Some "<participant, target, perspective and invocation schema>"
```

Browser bundle 必須由 WebSharper/F# 產生，不手寫 `.js` 或 inline JavaScript。

### D2. Canonical MessageFabric truth

Web prompt/send/reply truth 只屬於 PTCS MessageFabric：

```text
PTCS user participant
  -> CommSpaMessageFabric public/direct/group send
  -> Foreman/Worker PTCS participant inbox
  -> codex.fs actor/runtime
  -> Artifact/Note store
  -> CommSpaMessageFabric reply with refs
```

UI 不建立第二套 browser-local chat store，也不將 host HTTP control endpoint 當 MessageFabric transport。

### D3. Participant model

- Foreman/SessionActor 是預設 target participant。
- Child workers spawn 後必須 register/refresh 為 PTCS `agent` participant。
- `/chat/api/agents` 或等效 participant list 應能呈現 codex.fs agent participants。
- Public target 映射到 MessageFabric public channel。
- Group target 映射到 MessageFabric group id。
- Direct target 使用 exact participant id 或從 session id 導出的 Foreman participant。

### D4. Perspective is rendering policy

Perspective switching 是 read/render policy：

- 必須顯示目前 sender、target、scope、perspective。
- 不得 silently forge `agent.*` sender。
- 若未授權查看某 participant 視角，UI 必須顯示受控錯誤或降級到自己的 user participant 視角。

### D5. Invocation controls are intent metadata

Web controls 可參考 PTCS Dynamic 的 Argu metadata/form rendering pattern，但輸出是 normalized invocation intent：

```text
engine = codex | agy
model = <model id>
reasoning = xhigh | high | medium | low | default
approval = <policy id>
sandbox = <profile id>
extra = validated structured options
```

Runtime/actor 才能：

- 驗證 policy；
- probe adapter capability；
- 選擇 Codex/Agy surface module；
- render versioned argv；
- 組 prompt/history；
- local compact；
- 寫 transcript/note/artifact；
- 控制 MessageFabric ack ordering。

### D6. Artifact/note rendering

UI 應渲染：

- redacted final summary；
- run id；
- manifest ref；
- note ref；
- PTCS consumed message ids/cursor；
- status/outcome。

UI 不應把 raw prompt、stdout、stderr、events、final transcript 全量塞入 MessageFabric body 或 browser state。需引用 `RFC-PERSIST-0001` 的 private raw artifact / public redacted export policy。

### D7. Browser verifier must be real

未來驗收必須使用 real PTCS Host 與 real browser：

- local profile：`http://127.0.0.1:82/chat`。
- public OAuth profile：`https://my-ai.co.in:81/chat` 可作 auth redirect 行為驗證。
- verifier 要載入 extension manifest/script；
- verifier 要透過 real PTCS MessageFabric public/direct/group send；
- verifier 要看見 Foreman/worker participants；
- verifier 要操作 target/perspective controls；
- verifier 要驗證 artifact/note refs rendering；
- verifier 不可使用 standalone `codex.fs.host` `/chat`、fake mailbox、mock browser 或 internal-only smoke 作為交付驗收。

## 影響範圍

- `doc/Requirement.md`：Web UI 需求以 PTCS extension/bundle 為準。
- `doc/SA.md`：產品邊界加入 WEB-001 accepted contract。
- `doc/SD.md`：補齊 `codex.fs.web` / `useAIChat(...)` minimal design。
- `doc/WBS.md` / `doc/WBS.WEB-001.md`：追蹤此 RFC slice。
- `doc/Test.md`：將 `T-WEB-001` 標記為 RFC slice pass，實作驗收仍是 future verifier。
- `MCP.KM.md` / `doc/DevLog.md`：沉澱 PTCS Web bundle 決策。

## 驗收

本 RFC slice acceptance：

| Case | Requirement |
| --- | --- |
| `TC-WEB-001A` RFC traceability | RFC、WBS、Test、SA、SD、Requirement、DevLog、KM 互相回鏈。 |
| `TC-WEB-001B` PTCS extension boundary | RFC 明確使用 `RegisterClientExtension`、script asset、JSON handler 與 WebSharper bundle。 |
| `TC-WEB-001C` MessageFabric truth | RFC 明確禁止 standalone `/chat`、browser-local store、parallel fabric。 |
| `TC-WEB-001D` participant UX | RFC 明確 Foreman default、worker participant、public/direct/group、authorized perspective。 |
| `TC-WEB-001E` artifact policy | RFC 明確只 render redacted summary + manifest/note refs，raw artifacts 不入 chat body。 |
| `TC-WEB-001F` real verifier plan | Test row 指向未來 `misc/verifyPtcsAiChatBundle.fsx`，且明確不是 passing implementation evidence。 |

Future implementation acceptance：

| Case | Requirement |
| --- | --- |
| `TC-WEB-001G` extension load | PTCS Host `/chat` includes `codex-fs-ai-chat` extension manifest and same-origin WebSharper bundle assets. |
| `TC-WEB-001H` participant list | Browser participant list includes Foreman and spawned worker `agent` participants from the shared PTCS hub/fabric. |
| `TC-WEB-001I` public/direct/group send | Browser can send through real PTCS public/direct/group scopes and workers receive through MessageFabric. |
| `TC-WEB-001J` perspective switch | Browser can switch authorized perspective without changing sender identity. |
| `TC-WEB-001K` invocation controls | Browser sends normalized invocation intent; runtime/actor validates and records selected options in artifacts. |
| `TC-WEB-001L` artifact refs | Browser renders redacted reply, manifest ref and note ref produced by a real worker run. |

## 關聯文件

- `doc/WBS.md` row `WEB-001`
- `doc/WBS.WEB-001.md`
- `doc/Test.md` row `T-WEB-001`
- `doc/SD.md` §14.1, §14.2
- `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md`
- `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`
- `doc/RFC/RFC-CLI-0002.interactive-participant-client.md`
- `doc/RFC/RFC-PERSIST-0001.transcript-note-artifact-store.md`
