# RFC-WEB-0004 AI Intent Output Projection

狀態：Accepted  
日期：2026-07-06  
關聯 WBS：`WEBR-010`  
關聯 Test：`T-WEBR-010`  
關聯 verifier：`misc/verifyAiIntentOutputProjection.fsx`

## 背景

`WEBR-009` 已證明 `codexfs-ai-chat` append page 的 `codex.fs.web.ai-intent.v1` value 會被 host bridge 投遞到 MessageFabric，Foreman actor 會執行 bounded Codex/Agy run，並透過 MessageFabric 回覆 artifact refs。

但目前使用者在 `http://10.28.112.93:18488/page/codexfs-ai-chat` 送出 prompt 後，只看得到 append page value stream 中的 raw JSON。實際 runtime reply 存在於 service participant direct thread：

```text
user.codexfs.web.ai-intent <-> agent.codexfs.foreman
```

而使用者通常從 browser/admin 角度看 append page，因此看不到 output。這不是使用者選項錯誤；`Target=Foreman`、`Invocation=Exec`、`Approval=Never` 是基本合法路徑。若 user 送出 prompt 後看不到 final text 或 artifact refs，產品不可用。

## 目標

1. AI Chat append page 在送出 prompt 後，必須在同一個 user-visible surface 顯示 runtime reply summary 與 artifact refs。
2. Reply projection 必須仍以 PTCS MessageFabric thread 為 truth，不新增平行 chat store。
3. Projection 必須清楚標示 sender/target/perspective，避免把 read/render perspective 誤當 sender impersonation。
4. Playwright verifier 必須操作 real PTCS webshell：選 Foreman、送 prompt、等待可見 output。
5. 若 headless execution 失敗，UI 必須顯示 failure reply/stderr summary 或 run refs，而不是只留下 raw JSON intent。

## 非目標

- 不改變 Foreman runtime ownership；actor/runtime 仍透過 MessageFabric 消費 prompt。
- 不把 raw prompt/stdout/stderr 全文公開塞進 append page。
- 不把 browser 變成 direct engine caller。
- 不偽造 `agent.*` sender identity；projection 是 read/render 行為。

## 現象與證據

2026-07-06 investigation 使用截圖：

`G:\PulseTrade2.fs\misc\2026-07-06_prompt輸出結果哪裡去了.png`

查證結果：

- `final.md` 存在：`G:\codex.fs\src\codex.fs\.codex.fs\runtime-18488-20260705220035\artifacts\sessions\foreman\runs\run-20260705140141224-9ef5d82b\final.md`。
- PTCS thread `user.codexfs.web.ai-intent <-> agent.codexfs.foreman` 含 original prompt 與 Foreman reply。
- PTCS thread `admin <-> agent.codexfs.foreman` 沒有該 reply。
- Append page value stream 只顯示 raw intent JSON，沒有投影 reply。

## 方案取捨

| Option | Decision | Reason |
| --- | --- | --- |
| 告訴使用者去查 artifact path | Rejected | 這只能 debug，不是可用產品。 |
| 要 user 手動切到 hidden service participant thread | Rejected | 一般使用者不知道 `user.codexfs.web.ai-intent`，且目前 UI 沒提供明確 perspective switch。 |
| 在 append page client poll MessageFabric thread and render latest replies | Accepted for WEBR-010 MVP | 最小修正；不改 runtime，不新增 store，能讓同頁可見 output。 |
| 修改 bridge sender 成目前登入 user | Deferred | 較正確但牽涉 PTCS browser identity/ACL；需要獨立 identity RFC。 |
| 新增 host projection API | Deferred | 可作長期最佳化；MVP 可先用既有 `/chat/api/thread` real PTCS API。 |

## 決策

1. `codexfs-ai-chat` renderer 在同頁新增 output projection region。
2. Projection region 使用 stable selectors：
   - `data-testid="codexfs-ai-output"`
   - `data-testid="codexfs-ai-output-state"`
   - `data-testid="codexfs-ai-output-message"`
   - 若 reply 可解析 artifact refs，沿用 `codexfs-artifact-reply` renderer。
3. MVP projection source 是 existing PTCS chat API：

```text
/chat/api/thread?participantId=user.codexfs.web.ai-intent&peerId=<targetParticipantId>
```

4. Send 後 UI 立即顯示 waiting state，並輪詢 thread 直到出現 target participant 的 newer reply 或 timeout。
5. Projection 必須顯示至少：
   - latest final/reply summary；
   - run id/outcome；
   - manifest/final/note refs when available；
   - sender/target thread identity。
6. 預設 option `Target=Foreman`、`Engine=Agy|Codex`、`Invocation=Exec`、`Approval=Never` 不應要求 user 額外設定才能看到 output。
7. 若 selected target 是 public/group，projection rules 需明確顯示 scope；WEBR-010 MVP 可先 gate direct Foreman/participant projection，public/group 列為 follow-up。

## 影響範圍

| Area | Change |
| --- | --- |
| `codex.fs.web` | AI append renderer 增加 output projection region、thread polling 與 artifact renderer reuse。 |
| `codex.fs.host` | 不改 runtime ownership；必要時補 metadata/route docs。 |
| `misc` verifiers | 新增 Playwright verifier `verifyAiIntentOutputProjection.fsx`。 |
| Docs | REQ/SA/SD/WBS/Test/DevLog/KM 更新可見 output requirement。 |

## 驗收

`misc/verifyAiIntentOutputProjection.fsx` 必須：

1. build solution and generated WebSharper bundle；
2. start real LAN `ptcs-webshell` with `web.actorFabric=auto-local`；
3. open `/page/codexfs-ai-chat` with Playwright；
4. select Foreman target, choose engine, fill prompt containing unique token and PowerShell date request；
5. click Send；
6. wait for visible `codexfs-ai-output-message` or `codexfs-artifact-reply`；
7. assert visible text contains the unique token/date or artifact refs；
8. assert raw intent JSON alone does not satisfy acceptance；
9. record screenshot and artifact paths.

## 關聯文件

- `doc/Requirement.md`
- `doc/SA.md`
- `doc/SD.md`
- `doc/WBS.md`
- `doc/WBS.WEBR-010.md`
- `doc/Test.md`
- `doc/Test.WEBR-010.md`
- `doc/RFC/RFC-RUNTIME-0002.foreman-control-plane-and-ai-intent-bridge.md`
- `doc/DevLog.md`
- `MCP.KM.md`
