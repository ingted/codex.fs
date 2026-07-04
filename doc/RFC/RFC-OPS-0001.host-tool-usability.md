# RFC-OPS-0001 Host / Tool Usability Gate

狀態：Accepted  
日期：2026-07-05  
關聯：`HOST-004`, `DOC-004`, `REL-004`, `DEVOP.md`

## 背景

`codex.fs.host` 曾被回報為「已啟動」，但使用者開啟 `http://10.28.112.93:10481/` 得到 HTTP 404；同時 `C:\Users\Administrator\.dotnet\tools` 找不到 `codex.fs` CLI tool。前一輪驗證只確認 `/api/codexfs/host/health` HTTP 200，且 CLI 只用 `dotnet run --project` 驗證，沒有證明產品入口、文件 UI、或使用者實際工具安裝路徑可用。

## 目標

- advertised host root URL 必須是可用產品入口，不可只讓 API health endpoint 通過。
- host 文件入口必須可由瀏覽器開啟：OpenAPI JSON 與 Swagger UI。
- dotnet tools 必須安裝到使用者預期的 global tool path，或交付時明確說明替代 tool-path。
- host 可用性驗證必須包含 browser/Playwright 觀點。
- 長駐 host 不得鎖住 dev build output，避免後續 build/test 被服務 process 擋住。

## 非目標

- 不在本 RFC 實作完整 PTCS Web UI。
- 不在本 RFC 產生完整 F# SDK reference site；SDK XML docs 與 NuGet package XML docs仍為目前 SDK 文件基線。
- 不改變 PTCS MessageFabric 作為 message truth source 的設計。

## 決策

1. `GET /` 是 host operator landing page，顯示 advertised URI、health、OpenAPI JSON、Swagger UI 與 CLI command example。
2. Swagger/OpenAPI 必須由部署設定明確開啟：
   - `apiDocs.generateOpenApi=true`
   - `apiDocs.exposeSwaggerUi=true`
   - `apiDocs.swaggerRoutePrefix=docs`
3. 使用者-facing 啟動交付必須用 installed/global tool 或獨立 tool path，不用 `dotnet run` 長駐 dev build output。
4. tool 可用性 gate 必須確認：
   - `C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe --help`
   - `C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe --help`
5. browser gate 必須確認：
   - root URL HTTP 200 且可見 `codex.fs host`
   - Swagger UI HTTP 200 且可見 endpoint list
   - OpenAPI JSON HTTP 200 且包含至少一個 expected path

## 方案取捨

| 方案 | 結論 | 理由 |
| --- | --- | --- |
| 只回報 health endpoint | Reject | 對使用者而言根 URL 仍不可用，且無文件入口。 |
| root redirect to Swagger UI | Deferred | 若 Swagger disabled，root 仍會失效；landing page 可同時列出 enabled/disabled 狀態。 |
| root landing page + docs links | Accept | 輕量、不引入前端框架，能立即改善 operator 可發現性。 |
| `dotnet run` 長駐 host | Reject for handoff | 會鎖住 dev build output，阻礙 build/test；只可作 bounded dev/internal check。 |
| global dotnet tool install | Accept | 符合使用者在 `C:\Users\Administrator\.dotnet\tools` 查找工具的期待。 |

## 影響範圍

- `CodexFs.Host.HostControl`
- `codex.fs.host.tool`
- `codex.fs.cli`
- `DEVOP.md`
- `AGENTS.md` host/CLI usability harness
- WBS/Test/SD/KM/DevLog

## 驗收

- `dotnet build .\codex.fs.slnx --no-restore` 通過。
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` 通過。
- global tools 存在並可執行 help。
- fixed host 以 global `codex.fs.host.exe` `0.1.0-alpha.2` 啟動於 `http://10.28.112.93:10481`。
- Playwright/browser evidence：
  - root summary/screenshot：`G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\summary.json`, `root.png`
  - Swagger screenshot：`G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\docs.png`

## 反省

這次問題不是單純 route bug，而是驗收哲學錯位：把「內部 API health 成功」誤當成「產品可用」。對 operator-facing tool 而言，入口 URL、文件可發現性、安裝後 command path、瀏覽器視角，都是產品契約的一部分。未來任何宣稱「host/CLI 可用」的交付都必須從使用者會採取的第一步開始驗證，而不是從工程師知道的內部 endpoint 開始。
