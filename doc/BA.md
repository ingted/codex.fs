# Business Analysis

版本：`0.1.0-draft`  
狀態：Draft  
對應文件：`doc/Requirement.md`, `doc/RFC/RFC-PRODUCT-0001.codexfs-agent-runtime-reset.md`

## 1. 產品問題

使用者目前需要手動把 Codex/Agy terminal 上一段 prompt、stdout/stderr、final response 複製到 notes，且多個 agent/session 之間沒有共同的 participant/chat/runtime truth。這讓複雜工作無法可靠交給「包工頭」式 Foreman/SessionActor，也無法讓其他 WorkerActor 用同一個 PTCS fabric 溝通與追溯。

## 2. 業務目標

| 目標 | 成功判斷 |
| --- | --- |
| 降低人工複製 terminal history | 每次 runtime cycle 自動保存 prompt、request、argv、stdout、stderr、final、manifest、ready-to-ack boundary。 |
| 讓人與 agents 使用同一套 chat truth | PTCS MessageFabric participant/direct/public/group message 是唯一 user/agent 溝通來源。 |
| 讓包工頭/worker 能協作 | Foreman/SessionActor 與 child WorkerActor 都註冊成 PTCS `agent` participant。 |
| 保持輕量可嵌入 | `codex.fs` 消費 PTCS packages，不重寫 ActorFabric、MessageFabric 或 Web chat shell。 |

## 3. Stakeholders

| Stakeholder | Need |
| --- | --- |
| Human operator | 用 CLI 或 PTCS Web 指派任務、看到 worker reply 與 artifact refs。 |
| Foreman/SessionActor | 接收預設任務、可自行執行 runtime cycle，也可 spawn child workers。 |
| WorkerActor | 以 participant 身分消費 assigned messages、執行 headless engine、回覆 result refs。 |
| PTCS Host/Web shell | 提供既有 chat room、participant list、tabs/thread/composer 與 extension hosting。 |
| Host operator | 有清楚 OpenAPI/Swagger/health/tool handoff 與 artifact privacy policy。 |

## 4. ACTOR-003 business slice

`ACTOR-003` 的 business value 是把「worker 可見」推進到「worker 可做事且留下證據」。WEBR-007 需要真實 run refs；若沒有 ACTOR-003，就只能在 Web 上顯示假 refs 或 host-only refs，兩者都不符合產品。

Acceptance business statement：

> 一個使用者 participant 發送 prompt 給 Foreman/Worker participant 後，WorkerActor 能在 PTCS ActorFabric 上呼叫 runtime cycle，產生 private artifacts，透過 MessageFabric 回覆 redacted summary + manifest/final refs，並在 reply evidence 後 ack 消費的 inbox cursor。

## 5. Non-goals

- 不以 standalone diagnostics form 作為產品 chat。
- 不把 raw artifacts 放進 public chat body。
- 不在本 slice 宣稱 production sharded crash-durable delivery 已完成。
