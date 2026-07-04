# RFC-HOST-0001 Standalone Chat PoC And CLI Transport Errors

狀態：Accepted
日期：2026-07-05
關聯：`Requirement.md`, `SD.md`, `WBS.HOST-006.md`, `WBS.CLI-008.md`, `Test.md`, `DEVOP.md`

## 背景

User attempted:

```powershell
codex.fs.cli host status --host http://10.28.112.109:14724
```

The command crashed with an unhandled `.NET HttpRequestException`. The supplied endpoint used a different IP and used `14724`, which was the previous `codex.fs.host` process PID, not the HTTP port. The correct previous advertised host was `http://10.28.112.93:10481`, but the CLI still must not crash on connection failure.

User also opened `http://10.28.112.93:10481/chat` and found no chat page. Earlier host work exposed root/docs/API but did not provide a browser prompt entry point.

## 目標

- CLI transport failures return readable non-zero output without stack trace.
- Standalone host exposes `/chat` as an operator PoC prompt form.
- `/chat` sends through the same PTCS MessageFabric path as CLI `session send`.
- Documentation and tests clarify process PID vs HTTP port.

## 非目標

- 不實作 production PTCS participant-perspective Web UI。
- 不新增平行 durable chat store、cursor registry、ActorFabric 或 MessageFabric。
- 不讓 standalone package-owned host fabric 自動出現在既有 PTCS Host `/chat` 中。

## 決策

1. `CodexFs.Cli.CliHttp` catches transport-level request failures and returns `CliHttpResult` with `StatusCode = 0`, `IsSuccess = false`, and operator guidance.
2. Host adds `GET /chat` and `POST /chat`.
3. `POST /chat` accepts `sessionId`, `workerId`, and `prompt`, then calls `acceptSessionMessageAsync`.
4. blank `workerId` means default SessionWorker / 包工頭 target; nonblank `workerId` is exact worker participant override.
5. Production PTCS Web remains governed by `RFC-UI-0001` / `RFC-UI-0002`: use caller-owned PTCS MessageFabric from PTCS Host or peer cluster node.

## 影響範圍

- CLI packages: `codex.fs.cli`, `codex.fs.tool`。
- Host packages: `codex.fs.host`, `codex.fs.host.tool`。
- Host health JSON gains `chatUri`。
- OpenAPI document includes `/chat` as a human-facing host route.
- README/DEVOP/WBS/Test/SD/Requirement/KM/DevLog updated.

## 驗收

- `codex.fs.cli host status --host <closed-endpoint>` returns readable error and no stack trace.
- `GET /chat` returns HTTP 200 HTML.
- `POST /chat` returns HTTP 200 HTML containing `Accepted`.
- Session status for the posted session contains the chat prompt.
- `dotnet build .\codex.fs.slnx` and `tests/codex.fs.Tests` pass.
