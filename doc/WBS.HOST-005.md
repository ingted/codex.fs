# WBS Detail: HOST-005 Caller-owned PTCS MessageFabric host seam

WBS ID：`HOST-005`
狀態：Done
Progress：100
Previous：`HOST-002`, `PTCS-002`
Test：`T-HOST-005`
Test case：`TC-HOST-005 startWithMessageFabric caller-owned fabric identity`
SD：`SD §9`

## Goal

讓既有 PTCS Host 或同 cluster 節點可 reference `codex.fs.host` package，並用 caller-owned `CommSpaMessageFabric` 啟動 codex.fs runtime。這是 PTCS Web UI 與 codex.fs workers 共用 participant/chat truth 的必要 seam。

## Changes

- Added `CodexFs.Host.HostRuntime.startWithMessageFabric`.
- Kept `startInProcessMessageFabric` as a convenience wrapper over a package-owned PTCS MessageFabric.
- Preserved `HostControl.tryStartAsync` behavior: if runtime already has `MessageFabric`, HTTP control does not create a second in-process fabric.

## Acceptance

- `tests/codex.fs.Tests` verifies the caller-owned fabric object identity is preserved by `startWithMessageFabric`.
- This slice does not initialize ActorSystem/sharding; ActorFabric cluster binding remains a separate worker-loop slice.

## Boundary

Standalone `codex.fs.host` remains useful for CLI/API/docs verification, but it owns a separate package-owned fabric. It will not make workers visible in an already running PTCS Web host such as `http://127.0.0.1:82/chat`.
