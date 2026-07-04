# WBS CLI-004 - CLI Terminal Self-Use Hardening

## 摘要

CLI-004 是續航規則下新增的 terminal usability slice。前一輪 WBS 已無未完成項，但 user 明確要求繼續並「自己試用 cli」，因此本工項補上 compiled CLI 的手動 real-path walkthrough，並修正自用時會遇到的兩個落差：

- `codex.fs.cli host status --host <advertiseUri>` 需真的呼叫 host health endpoint。
- `codex.fs.cli session send --prompt @file` 需在 CLI client 端讀檔後把 prompt text 送入 host。

## Traceability

| 欄位 | 值 |
| --- | --- |
| WBS ID | CLI-004 |
| Previous | CLI-003; HOST-003 |
| SD | SD §14 |
| Test | T-CLI-004 |
| Test case | TC-CLI-004 real terminal walkthrough |
| StartTime | 2026-07-05 00:10 +08:00 |
| UpdatedAt | 2026-07-05 00:16 +08:00 |
| Progress | 100 |
| Status | Done |
| Blocker | None |

## 實作內容

- `CodexFs.Cli.Cli.tryParseHostStatus` parses `host status --host <uri>` into a typed option.
- `CodexFs.Cli.Cli.tryResolvePromptText` treats leading `@` as a prompt file reference and rejects blank files/paths.
- `CodexFs.Cli.CliHttp.getHostStatusAsync` calls `GET /api/codexfs/host/health`.
- `CodexFs.Cli.Program.main` now executes `host status` and resolves `@file` before `session send`.

## 驗證

- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-CLI-004 host status and @file prompt resolver passed`.
- Manual terminal walkthrough used real host tool + real compiled CLI through LAN URI `http://10.28.112.93:10481`:
  - `codex.fs.cli host status --host http://10.28.112.93:10481` returned `status=running` JSON.
  - `codex.fs.cli session send --session cli004.selfuse --prompt @G:\codex.fs\src\codex.fs\.codex.fs\cli004-selfuse\prompt.md --host http://10.28.112.93:10481` returned `status=accepted`.
  - `session status` returned `pendingCount=1`.
  - `session drain` returned `status=drained`.
  - final `session status` returned `pendingCount=0`.

Evidence summary: `G:\codex.fs\src\codex.fs\.codex.fs\cli004-selfuse\summary.json`.

## 風險與後續

- `session create`, `run status`, `run artifacts`, and `engine probe` still parse but do not execute in the terminal entrypoint. They should become separate WBS rows rather than being hidden inside CLI-004.
- This slice validates host control + MessageFabric terminal interaction. It does not execute an engine run; that remains covered by E2E-002/OPS-002.
