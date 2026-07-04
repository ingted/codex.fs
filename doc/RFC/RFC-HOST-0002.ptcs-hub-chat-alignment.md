# RFC-HOST-0002 PTCS Hub Chat Alignment

狀態：Accepted
日期：2026-07-05
關聯：`Requirement.md`, `SD.md`, `WBS.HOST-007.md`, `WBS.CLI-009.md`, `Test.md`, `DEVOP.md`, `RFC-HOST-0001`

## 背景

User clarified that the product chat specification is PTCS-based. PTCS already provides a WebSharper chat room and hub. codex.fs should make workers/session-workers visible as PTCS participants through `MessageFabric` and `ActorFabric`, not implement a separate browser chat room under standalone `codex.fs.host`.

The alpha.5 `/chat` PoC also required a human to type a session id before sending. That is not acceptable first-use UX because a user does not know an internal session id at the start of a conversation. The default path must talk to the Foreman/SessionWorker.

## 目標

- Treat PTCS WebSharper chat room as the canonical human browser chat surface.
- Keep standalone `codex.fs.host` as diagnostics/control/docs, not a product chat implementation.
- Change `/chat` on standalone host into a guard page that points users to PTCS chat.
- Move standalone prompt testing to `/diagnostics/session-send`.
- Let CLI `session send` omit `--session` and target the default Foreman/SessionWorker.
- Expose the no-session CLI path as `POST /api/codexfs/foreman/messages`.

## 非目標

- 不在 standalone `codex.fs.host` 建立新的 WebSharper chat UI。
- 不把 diagnostics form 當成 production chat room。
- 不新增平行 MessageFabric、ActorFabric、durable chat store 或 cursor registry。
- 不以 `localhost`/`127.0.0.1` 作為 clustered actor/node-facing endpoint。

## 決策

1. PTCS Web chat is the product UI. codex.fs workers communicate by registering/sending/polling as PTCS participants through caller-owned `CommSpaMessageFabric` / `CommSpaActorFabric`.
2. `GET /chat` on standalone host returns a guard page: “Use PTCS chat”.
3. Standalone diagnostics prompt form lives at `GET/POST /diagnostics/session-send`.
4. Diagnostics `sessionId` may be blank; blank means `foreman`.
5. CLI `session send --prompt ... --host ...` is valid without `--session`.
6. CLI no-session send posts to `/api/codexfs/foreman/messages`.
7. `HostControlHealthResponse` exposes `diagnosticsSessionSendUri`; `chatUri` is removed from the current contract.

## 影響範圍

- `codex.fs.host` route/health/OpenAPI contract.
- `codex.fs.cli` parser and HTTP routing.
- `README.md`, `DEVOP.md`, `Requirement.md`, `SD.md`, `WBS.md`, `Test.md`, `MCP.KM.md`, `DevLog.md`.
- Package family version bumps to `0.1.0-alpha.6`.

## 驗收

- `GET /chat` returns HTTP 200 and explains that product browser chat belongs to PTCS WebSharper.
- `GET /diagnostics/session-send` returns HTTP 200 diagnostics form.
- `POST /diagnostics/session-send` can send a prompt through real PTCS MessageFabric.
- `codex.fs.cli session send --prompt <text> --host <uri>` returns HTTP 202 and `targetParticipantId = agent.codexfs.foreman`.
- OpenAPI includes `/chat`, `/diagnostics/session-send`, and `/api/codexfs/foreman/messages`.
- `dotnet build .\codex.fs.slnx --no-restore` and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` pass.

