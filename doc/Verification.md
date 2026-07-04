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

## Planned Verifier Specs

| Planned verifier | WBS/Test | Purpose | Status |
| --- | --- | --- | --- |
| `TC-UI-001A..E` future PTCS browser + MessageFabric verifier | `UI-001` / `T-UI-001` | Verify codex.fs PTCS Web UI extension manifest, real MessageFabric send/reply, artifact reference rendering, LAN/DNS advertised host status, and fallback behavior. | Specification only in `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md`; no script exists yet and no fake/mock UI smoke is accepted. External PTCS seam references: `RFC-PTC-SPA-0006`, `RFC-PTC-SPA-0008`, `RFC-PTC-SPA-0010`, `RFC-SPA-UPSTREAM-0001`, `RFC-SPA-UPSTREAM-0002`, `RFC-SPA-UPSTREAM-0003`. |
