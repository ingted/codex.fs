# Verification

## Purpose

本文件記錄 codex.fs 的 real-path verifier。新增 verifier 前需先在此說明缺口、用途、適用範圍、是否 mutating、是否讀取 secret path，以及對應 Test/WBS。

## Verifiers

| Verifier | WBS/Test | Purpose | Scope | Mutating | Secret handling | Revision |
| --- | --- | --- | --- | --- | --- | --- |
| `misc/verifyMessageToEngineReply.fsx` | `E2E-002` / `T-E2E-002` | 驗證 participant -> real PTCS MessageFabric -> host single-cycle runner -> real Agy engine -> artifacts -> PTCS reply。 | Local workstation with built Debug assemblies and installed `agy` headless CLI. | Yes: writes `.codex.fs/e2e002-artifacts/` and sends in-process PTCS messages. | Does not read secret files or print secret values; invokes the installed CLI using the current user's existing CLI auth/session. | `rev-20260704-001` |

## Notes

- `misc/verifyMessageToEngineReply.fsx` is a real E2E verifier, not a fake/mock smoke path.
- The verifier may consume the user's existing CLI subscription/API allowance because it runs the installed engine.
- `.codex.fs/` is ignored by Git and stores local verifier artifacts only.
