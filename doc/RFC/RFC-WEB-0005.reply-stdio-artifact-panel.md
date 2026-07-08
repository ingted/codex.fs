# RFC-WEB-0005 Reply Stdio Artifact Panel

狀態：Accepted
日期：2026-07-08
關聯 WBS：`WEBR-011`
關聯 Test：`T-WEBR-011`
關聯 verifier：`misc/verifyAiIntentOutputProjection.fsx`

## 背景

`WEBR-010` 讓 AI Chat append page 能投影 Foreman runtime reply，但實測仍有兩個產品缺口：

- 使用者要先看到本輪 `--output-last-message` 的最後回覆正文，而不是只看到 run path 或 refs。
- stdout/stderr/final/note 是 worker physical session truth，不能要求使用者手動去檔案系統找，也不能把整段 stdio 永久塞進 MessageFabric body。

目前 MessageFabric 回覆會包含 redacted summary 與 artifact refs；這是正確邊界。但 UI 需要一個可操作的 artifact viewer。

## 目標

1. Artifact reply card 預設顯示最後回覆正文。
2. Manifest/final/stdout/stderr/note refs 維持 collapsed，不撐爆 chat layout。
3. 使用者可從 reply card 開啟本輪 artifact viewer，查看 stdout/stderr/final/note。
4. Viewer 必須是浮動、可拖曳、可縮放的 browser div，不佔住 append page 底部大片空間。
5. Artifact 內容必須透過 PTCS same-origin client-extension JSON POST handler 讀取，並限制在 host `artifact.root` 之下。

## 非目標

- 不把 raw stdout/stderr 全文寫回 MessageFabric body。
- 不新增獨立 HTTP fabric 或平行 chat store。
- 不讓 browser 直接讀任意本機檔案路徑。
- 不在本 slice 做完整 artifact ACL；此 slice 先使用 PTCS same-origin extension handler 與 artifact root path boundary。

## 決策

| Decision | Detail |
| --- | --- |
| Artifact read endpoint | `POST /client-extensions/codexfs-ai-chat/artifact/read`。 |
| Server boundary | `useAIChat(...)` 可選擇註冊 artifact root；host webshell 以自己的 `artifact.root` 啟用。 |
| Path safety | 只接受 relative artifact path；拒絕 absolute path 與 traversal；resolved path 必須位於 artifact root 下。 |
| Response size | 單次讀取預設最多 `131072` bytes，回覆 `truncated` 與原始 file size。 |
| UI | `codexfs-stdio-panel` 是 fixed div，支援 drag 與 CSS `resize: both`。 |
| MessageFabric body | 保持 redacted summary + refs；raw stdio 只按需讀取。 |

## 驗收

`misc/verifyAiIntentOutputProjection.fsx` 必須在 real PTCS webshell 上：

1. 送出 Foreman/Codex prompt。
2. 等待同頁 `codexfs-ai-output-message` 出現 artifact reply。
3. 確認 `codexfs-artifact-summary` 含最後回覆正文。
4. 點擊 `codexfs-stdio-open`。
5. 確認 `codexfs-stdio-panel` 可見，先載入 stdout artifact 且沒有 read failed。
6. 切到 `codexfs-stdio-tab-final`，確認 `codexfs-stdio-content` 含本輪 prompt token/date。
7. 保存 Playwright screenshot 與 artifact evidence。

## 關聯文件

- `doc/Requirement.md`
- `doc/SA.md`
- `doc/SD.md`
- `doc/WBS.md`
- `doc/WBS.WEBR-011.md`
- `doc/Test.md`
- `doc/Test.WEBR-011.md`
- `doc/Verification.md`
- `doc/DevLog.md`
