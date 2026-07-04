# Work Breakdown Structure

版本：`0.1.0-draft`  
狀態：Draft  
對應文件：`doc/Requirement.md`, `doc/SA.md`, `doc/SD.md`, `doc/Test.md`

## 1. 使用規則

本 WBS 是 `codex.fs` 後續標準開發流程的 current-state 工項表。主表只放可掃描的 leaf item；狀態欄只放短摘要，不放長段 evidence、決策或 debug 內容。

若某工項需要長期補充 evidence、blocker、替代方案或進度細節，`Detail` 欄必須連到 side-by-side 文件：`WBS.<ID>.md`。主表以 `@<ID>` 表示 detail 文件，例如 `@PTCS-002`。

Status 值：

- `Planned`：已排入，但尚未動工。
- `InProgress`：已動工，尚未達到對應 Test DoD。
- `Blocked`：有明確 blocker，暫時不能完成。
- `Done`：對應 Test item 已通過，且必要文件/API docs 已同步。
- `Deferred`：已決定延後，不在目前 delivery slice。

完成條件：

- `Progress=100` 前，對應 Test item 必須是 `Pass` 或有明確 approved waiver。
- API-facing 工項完成時，必須同步 SD §10 規定的 API comments、examples、parameter/output 說明、OpenAPI/SDK docs。
- 涉及 PTCS MessageFabric/ActorFabric 的工項不得新增平行 fabric。
- verifier 腳本尚未存在時，`Test case/verifier` 欄可列 planned script name，但 status 不得標 Done。

## 2. WBS Table

| ID | Work item | Previous | Progress | Status | Blocker | StartTime | UpdatedAt | SD item | Test item | Test case/verifier | Detail |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- | --- |
| PLAN-001 | 建立 WBS/Test baseline | START | 100 | Done | None | 2026-07-04 17:53 +08:00 | 2026-07-04 17:53 +08:00 | SD §15-§16 | T-PLAN-001 | TC-PLAN-001 doc traceability | inline |
| CF-001 | 建立 solution/project scaffold | PLAN-001 | 100 | Done | None | 2026-07-04 18:23 +08:00 | 2026-07-04 18:25 +08:00 | SD §2, §16.1 | T-CF-001 | TC-CF-001 `dotnet restore/build .\codex.fs.slnx` | inline |
| CF-002 | 實作 core domain model | CF-001 | 100 | Done | None | 2026-07-04 18:27 +08:00 | 2026-07-04 18:30 +08:00 | SD §3, §16.1 | T-CF-002 | TC-CF-002 `CodexFs.Domain` build/XML docs | inline |
| CF-003 | 實作 artifact manifest model | CF-002 | 100 | Done | None | 2026-07-04 18:32 +08:00 | 2026-07-04 18:35 +08:00 | SD §12, §16.1 | T-CF-003 | TC-CF-003 `CodexFs.Artifacts` build/XML docs | inline |
| CF-004 | 實作 file artifact store | CF-003 | 100 | Done | None | 2026-07-04 18:37 +08:00 | 2026-07-04 18:42 +08:00 | SD §12, §16.5 | T-CF-004 | TC-CF-004 temp FSI write/sha/no-overwrite | inline |
| CF-005 | 實作 redaction model | CF-004 | 100 | Done | None | 2026-07-04 18:44 +08:00 | 2026-07-04 18:48 +08:00 | SD §13 | T-CF-005 | TC-CF-005 temp FSI token-like redaction | inline |
| EN-001 | 實作 engine adapter contract | CF-002 | 100 | Done | None | 2026-07-04 18:50 +08:00 | 2026-07-04 18:53 +08:00 | SD §4, §16.2 | T-EN-001 | TC-EN-001 `CodexFs.Engine` build/XML docs | inline |
| EN-002 | 實作 process runner/guard contract | EN-001 | 100 | Done | None | 2026-07-04 18:55 +08:00 | 2026-07-04 18:56 +08:00 | SD §4, SA §9 | T-EN-002 | TC-EN-002 temp FSI success/timeout kill | inline |
| CDX-001 | Codex CLI probe fixtures | EN-001 | 100 | Done | None | 2026-07-04 19:11 +08:00 | 2026-07-04 19:15 +08:00 | SD §5, §15 | T-CDX-001 | TC-CDX-001 codex 0.142.4 exec help/version fixture | inline |
| CDX-002 | Codex `0.142.x` Argu DU/render | CDX-001 | 100 | Done | None | 2026-07-04 19:16 +08:00 | 2026-07-04 19:21 +08:00 | SD §5, §7, §16.3 | T-CDX-002 | TC-CDX-002 codex Argu parse/render | inline |
| CDX-003 | Codex artifact mapping | CDX-002 | 100 | Done | None | 2026-07-04 19:22 +08:00 | 2026-07-04 19:25 +08:00 | SD §5, §12 | T-CDX-003 | TC-CDX-003 codex stdout/stderr/event/final artifact map | inline |
| AGY-001 | Agy CLI probe fixtures | EN-001 | 100 | Done | None | 2026-07-04 18:58 +08:00 | 2026-07-04 18:59 +08:00 | SD §6, §15 | T-AGY-001 | TC-AGY-001 agy 1.0.16 help/version fixture | inline |
| AGY-002 | Agy `1.0.x` Argu DU/render | AGY-001 | 100 | Done | None | 2026-07-04 19:00 +08:00 | 2026-07-04 19:05 +08:00 | SD §6, §7, §16.4 | T-AGY-002 | TC-AGY-002 agy Argu parse/render | inline |
| AGY-003 | Agy artifact mapping | AGY-002 | 100 | Done | None | 2026-07-04 19:06 +08:00 | 2026-07-04 19:10 +08:00 | SD §6, §12 | T-AGY-003 | TC-AGY-003 agy stdout/stderr/final artifact map | inline |
| PTCS-001 | 定義 PTCS package/reference range | CF-001 | 100 | Done | None | 2026-07-04 20:33 +08:00 | 2026-07-04 20:44 +08:00 | SD §8, §17 | T-PTCS-001 | TC-PTCS-001 `tests/codex.fs.Tests` PTCS restore/reference | inline |
| PTCS-002 | 實作 MessageFabric session binding | PTCS-001 | 100 | Done | None | 2026-07-04 20:41 +08:00 | 2026-07-04 20:45 +08:00 | SD §8, §16.6 | T-PTCS-002 | TC-PTCS-002 `tests/codex.fs.Tests` MessageFabric binding | [@PTCS-002](WBS.PTCS-002.md) |
| PTCS-003 | 實作 durable task handoff | PTCS-002 | 0 | Blocked | SA-TBD-004 durable profile | 未動工 | 2026-07-04 17:53 +08:00 | SD §8, §16.10 | T-PTCS-003 | TC-PTCS-003 durable handoff | inline |
| SESS-001 | 實作 session state/effect model | CF-002 | 100 | Done | None | 2026-07-04 19:58 +08:00 | 2026-07-04 20:04 +08:00 | SD §11 | T-SESS-001 | TC-SESS-001 pure decide transitions | inline |
| SESS-002 | 實作 prompt assembly | SESS-001 | 100 | Done | None | 2026-07-04 19:57 +08:00 | 2026-07-04 20:04 +08:00 | SA §6, SD §11 | T-SESS-002 | TC-SESS-002 `tests/codex.fs.Tests` prompt batch assembly | inline |
| SESS-003 | 實作 rule-based local compact | SESS-002 | 100 | Done | None | 2026-07-04 20:08 +08:00 | 2026-07-04 20:12 +08:00 | Requirement R-005, SD §11.1, §17 | T-SESS-003 | TC-SESS-003 `tests/codex.fs.Tests` compact preserves blockers | inline |
| HOST-001 | 實作 host config loading | CF-005;PTCS-001 | 100 | Done | None | 2026-07-04 20:55 +08:00 | 2026-07-04 21:03 +08:00 | SD §9 | T-HOST-001 | TC-HOST-001 config parse/redaction | inline |
| HOST-002 | 實作 minimal host runtime | HOST-001;PTCS-002;SESS-001 | 100 | Done | None | 2026-07-04 21:08 +08:00 | 2026-07-04 21:14 +08:00 | SD §9, §16.7 | T-HOST-002 | TC-HOST-002 host runtime/health | inline |
| HOST-003 | 定義並實作 host control endpoint | HOST-002 | 100 | Done | None | 2026-07-04 21:21 +08:00 | 2026-07-04 21:27 +08:00 | SD §9, §17 | T-HOST-003 | TC-HOST-003 endpoint contract | [@HOST-003](WBS.HOST-003.md) |
| DOC-001 | API docs toolchain spike | CF-001 | 100 | Done | None | 2026-07-04 19:26 +08:00 | 2026-07-04 19:34 +08:00 | SD §10, §17 | T-DOC-001 | TC-DOC-001 docs toolchain decision | [@DOC-001](WBS.DOC-001.md) |
| DOC-002 | XML doc comments baseline | DOC-001;CF-002 | 100 | Done | None | 2026-07-04 19:35 +08:00 | 2026-07-04 19:40 +08:00 | SD §10 | T-DOC-002 | TC-DOC-002 XML docs generated | inline |
| DOC-003 | Swagger/OpenAPI generation | DOC-001;HOST-003 | 100 | Done | None | 2026-07-04 21:36 +08:00 | 2026-07-04 21:39 +08:00 | SD §10 | T-DOC-003 | TC-DOC-003 OpenAPI available | [@DOC-003](WBS.DOC-003.md) |
| CLI-001 | 實作 CLI command DU/help | HOST-003 | 100 | Done | None | 2026-07-04 21:44 +08:00 | 2026-07-04 21:48 +08:00 | SD §14, §16.9 | T-CLI-001 | TC-CLI-001 Argu parser/help | [@CLI-001](WBS.CLI-001.md) |
| CLI-002 | CLI session send real path | CLI-001;PTCS-002 | 100 | Done | None | 2026-07-04 21:51 +08:00 | 2026-07-04 21:56 +08:00 | Requirement §6.1, SD §14 | T-CLI-002 | TC-CLI-002 CLI send through MessageFabric | [@CLI-002](WBS.CLI-002.md) |
| CLI-003 | CLI attach/drain/status | CLI-002 | 100 | Done | None | 2026-07-04 22:00 +08:00 | 2026-07-04 22:09 +08:00 | SD §14 | T-CLI-003 | TC-CLI-003 attach/drain/status | [@CLI-003](WBS.CLI-003.md) |
| REL-001 | NuGet package metadata | DOC-002;CF-001 | 100 | Done | None | 2026-07-04 19:48 +08:00 | 2026-07-04 19:55 +08:00 | Requirement §9, SD §2 | T-REL-001 | TC-REL-001 pack metadata/docs | inline |
| REL-002 | dotnet tool package | REL-001;HOST-001 | 0 | Planned | None | 未動工 | 2026-07-04 21:03 +08:00 | Requirement R-001, SD §2 | T-REL-002 | TC-REL-002 tool install/run help | inline |
| E2E-001 | Installed engine probe real path | CDX-003;AGY-003 | 100 | Done | None | 2026-07-04 19:41 +08:00 | 2026-07-04 19:47 +08:00 | SD §15 | T-E2E-001 | TC-E2E-001 installed codex/agy probe real path | inline |
| E2E-002 | MessageFabric message to engine to reply | HOST-002;CLI-003 | 100 | Done | None | 2026-07-04 22:13 +08:00 | 2026-07-04 22:27 +08:00 | Requirement §10, SA §6.1, SD §14 | T-E2E-002 | TC-E2E-002 `misc/verifyMessageToEngineReply.fsx` | [@E2E-002](WBS.E2E-002.md) |
| E2E-003 | Multi-agent group collaboration | E2E-002;PTCS-003 | 0 | Planned | PTCS-003 optional durable path | 未動工 | 2026-07-04 17:53 +08:00 | Requirement §6.3 | T-E2E-003 | TC-E2E-003 multi-agent MessageFabric group | inline |
| OPS-001 | Process orphan recovery | EN-002;HOST-002 | 0 | Planned | None | 未動工 | 2026-07-04 21:14 +08:00 | SA §9 | T-OPS-001 | TC-OPS-001 orphan process recovery | inline |
| OPS-002 | Session persistence boundary | PTCS-003;SESS-001 | 0 | Blocked | durable profile decision | 未動工 | 2026-07-04 17:53 +08:00 | SA §9, SD §11 | T-OPS-002 | TC-OPS-002 recovery/ack ordering | inline |
| UI-001 | PTCS Web UI extension/RFC | E2E-002;DOC-003 | 0 | Planned | None | 未動工 | 2026-07-04 22:27 +08:00 | Requirement §4, SD §16.12 | T-UI-001 | TC-UI-001 PTCS UI extension RFC/verifier | inline |

## 3. Roll-up / Detail Files

| Detail | Purpose |
| --- | --- |
| [@PTCS-002](WBS.PTCS-002.md) | MessageFabric session binding is a high-risk integration slice; detail file tracks exact PTCS operations and acceptance gates. |
| [@HOST-003](WBS.HOST-003.md) | Host control endpoint uses HTTP with bind/advertise config; localhost is dev-only. |
| [@DOC-001](WBS.DOC-001.md) | API documentation toolchain selection affects NuGet SDK docs and Swagger generation. |
| [@DOC-003](WBS.DOC-003.md) | OpenAPI JSON and Swagger UI verification uses the real host endpoint through advertised non-loopback URI. |
| [@CLI-001](WBS.CLI-001.md) | Terminal client command surface is a compiled FAkka.Argu parser; real host execution is deferred to CLI-002/CLI-003. |
| [@CLI-002](WBS.CLI-002.md) | CLI session send uses real host HTTP endpoint and PTCS MessageFabric; attach/drain/status remains CLI-003. |
| [@CLI-003](WBS.CLI-003.md) | CLI status/attach/drain read the real session inbox through host control endpoints and drain acknowledges the cursor. |
| [@E2E-002](WBS.E2E-002.md) | First closed-loop real path spans MessageFabric, host, engine, artifacts and reply. |

## 4. Update Rule

When any row changes:

1. Update `Progress`, `Status`, `Blocker`, `StartTime`, `UpdatedAt`.
2. Update linked `doc/Test.md` row status and evidence.
3. If the change is API-facing, update comments/examples/parameter/output docs per SD §10.
4. Keep long evidence in `WBS.<ID>.md` or operation logs, not in the WBS table.
