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
| PTCS-003 | 實作 durable task handoff | PTCS-002 | 100 | Done | None | 2026-07-04 23:35 +08:00 | 2026-07-04 23:39 +08:00 | SD §8, §16.10 | T-PTCS-003 | TC-PTCS-003 durable handoff | [@PTCS-003](WBS.PTCS-003.md) |
| SESS-001 | 實作 session state/effect model | CF-002 | 100 | Done | None | 2026-07-04 19:58 +08:00 | 2026-07-04 20:04 +08:00 | SD §11 | T-SESS-001 | TC-SESS-001 pure decide transitions | inline |
| SESS-002 | 實作 prompt assembly | SESS-001 | 100 | Done | None | 2026-07-04 19:57 +08:00 | 2026-07-04 20:04 +08:00 | SA §6, SD §11 | T-SESS-002 | TC-SESS-002 `tests/codex.fs.Tests` prompt batch assembly | inline |
| SESS-003 | 實作 rule-based local compact | SESS-002 | 100 | Done | None | 2026-07-04 20:08 +08:00 | 2026-07-04 20:12 +08:00 | Requirement R-005, SD §11.1, §17 | T-SESS-003 | TC-SESS-003 `tests/codex.fs.Tests` compact preserves blockers | inline |
| HOST-001 | 實作 host config loading | CF-005;PTCS-001 | 100 | Done | None | 2026-07-04 20:55 +08:00 | 2026-07-04 21:03 +08:00 | SD §9 | T-HOST-001 | TC-HOST-001 config parse/redaction | inline |
| HOST-002 | 實作 minimal host runtime | HOST-001;PTCS-002;SESS-001 | 100 | Done | None | 2026-07-04 21:08 +08:00 | 2026-07-04 21:14 +08:00 | SD §9, §16.7 | T-HOST-002 | TC-HOST-002 host runtime/health | inline |
| HOST-003 | 定義並實作 host control endpoint | HOST-002 | 100 | Done | None | 2026-07-04 21:21 +08:00 | 2026-07-04 21:27 +08:00 | SD §9, §17 | T-HOST-003 | TC-HOST-003 endpoint contract | [@HOST-003](WBS.HOST-003.md) |
| HOST-004 | Host operator landing page / usability gate | HOST-003;DOC-003 | 100 | Done | None | 2026-07-05 00:41 +08:00 | 2026-07-05 01:00 +08:00 | SD §9, §10 | T-HOST-004 | TC-HOST-004 root landing browser gate | [@HOST-004](WBS.HOST-004.md) |
| HOST-005 | Caller-owned PTCS MessageFabric host seam | HOST-002;PTCS-002 | 100 | Done | None | 2026-07-05 01:22 +08:00 | 2026-07-05 01:22 +08:00 | SD §9 | T-HOST-005 | TC-HOST-005 `startWithMessageFabric` caller-owned fabric identity | [@HOST-005](WBS.HOST-005.md) |
| HOST-006 | Historical standalone host `/chat` PoC form | HOST-004;CLI-007 | 100 | Done | Superseded by HOST-007 | 2026-07-05 02:37 +08:00 | 2026-07-05 03:10 +08:00 | SD §9, §10 | T-HOST-006 | TC-HOST-006 `/chat` form send through MessageFabric | [@HOST-006](WBS.HOST-006.md) |
| HOST-007 | PTCS hub chat alignment and diagnostics split | HOST-006;UI-002 | 100 | Done | None | 2026-07-05 03:03 +08:00 | 2026-07-05 03:10 +08:00 | SD §9, §14.1 | T-HOST-007 | TC-HOST-007 `/chat` guard + diagnostics send + OpenAPI paths | [@HOST-007](WBS.HOST-007.md) |
| DOC-001 | API docs toolchain spike | CF-001 | 100 | Done | None | 2026-07-04 19:26 +08:00 | 2026-07-04 19:34 +08:00 | SD §10, §17 | T-DOC-001 | TC-DOC-001 docs toolchain decision | [@DOC-001](WBS.DOC-001.md) |
| DOC-002 | XML doc comments baseline | DOC-001;CF-002 | 100 | Done | None | 2026-07-04 19:35 +08:00 | 2026-07-04 19:40 +08:00 | SD §10 | T-DOC-002 | TC-DOC-002 XML docs generated | inline |
| DOC-003 | Swagger/OpenAPI generation | DOC-001;HOST-003 | 100 | Done | None | 2026-07-04 21:36 +08:00 | 2026-07-04 21:39 +08:00 | SD §10 | T-DOC-003 | TC-DOC-003 OpenAPI available | [@DOC-003](WBS.DOC-003.md) |
| DOC-004 | API/SDK docs handoff gate | DOC-003;HOST-004 | 100 | Done | None | 2026-07-05 00:41 +08:00 | 2026-07-05 01:00 +08:00 | SD §10 | T-DOC-004 | TC-DOC-004 Swagger/OpenAPI/XML docs visible | [@DOC-004](WBS.DOC-004.md) |
| CLI-001 | 實作 CLI command DU/help | HOST-003 | 100 | Done | None | 2026-07-04 21:44 +08:00 | 2026-07-04 21:48 +08:00 | SD §14, §16.9 | T-CLI-001 | TC-CLI-001 Argu parser/help | [@CLI-001](WBS.CLI-001.md) |
| CLI-002 | CLI session send real path | CLI-001;PTCS-002 | 100 | Done | None | 2026-07-04 21:51 +08:00 | 2026-07-04 21:56 +08:00 | Requirement §6.1, SD §14 | T-CLI-002 | TC-CLI-002 CLI send through MessageFabric | [@CLI-002](WBS.CLI-002.md) |
| CLI-003 | CLI attach/drain/status | CLI-002 | 100 | Done | None | 2026-07-04 22:00 +08:00 | 2026-07-04 22:09 +08:00 | SD §14 | T-CLI-003 | TC-CLI-003 attach/drain/status | [@CLI-003](WBS.CLI-003.md) |
| CLI-004 | CLI terminal self-use hardening | CLI-003;HOST-003 | 100 | Done | None | 2026-07-05 00:10 +08:00 | 2026-07-05 00:16 +08:00 | SD §14 | T-CLI-004 | TC-CLI-004 real terminal walkthrough | [@CLI-004](WBS.CLI-004.md) |
| CLI-005 | Installed command name usability correction | CLI-004;REL-004 | 100 | Done | None | 2026-07-05 01:22 +08:00 | 2026-07-05 01:22 +08:00 | SD §14 | T-CLI-005 | TC-CLI-005 global tool command `codex.fs` help/status | [@CLI-005](WBS.CLI-005.md) |
| CLI-006 | Explicit CLI command plus short alias | CLI-005;REL-004 | 100 | Done | None | 2026-07-05 02:02 +08:00 | 2026-07-05 02:02 +08:00 | SD §14 | T-CLI-006 | TC-CLI-006 global `codex.fs.cli` and `codex.fs` commands | [@CLI-006](WBS.CLI-006.md) |
| CLI-007 | Session worker default target and worker override | CLI-002;CLI-006 | 100 | Done | None | 2026-07-05 02:05 +08:00 | 2026-07-05 02:05 +08:00 | Requirement §6.1, SD §14 | T-CLI-007 | TC-CLI-007 session send default foreman and `--worker-id` override | [@CLI-007](WBS.CLI-007.md) |
| CLI-008 | CLI transport failure graceful error | CLI-004;CLI-006 | 100 | Done | None | 2026-07-05 02:37 +08:00 | 2026-07-05 02:48 +08:00 | Requirement R-002, SD §14 | T-CLI-008 | TC-CLI-008 readable connection failure without stack trace | [@CLI-008](WBS.CLI-008.md) |
| CLI-009 | No-session Foreman send default | CLI-007;HOST-007 | 100 | Done | None | 2026-07-05 03:03 +08:00 | 2026-07-05 03:10 +08:00 | Requirement R-002, SD §14 | T-CLI-009 | TC-CLI-009 `session send` without `--session` targets foreman | [@CLI-009](WBS.CLI-009.md) |
| REL-001 | NuGet package metadata | DOC-002;CF-001 | 100 | Done | None | 2026-07-04 19:48 +08:00 | 2026-07-04 19:55 +08:00 | Requirement §9, SD §2 | T-REL-001 | TC-REL-001 pack metadata/docs | inline |
| REL-002 | codex.fs.cli dotnet tool package | REL-001;HOST-001;CLI-003 | 100 | Done | None | 2026-07-04 22:35 +08:00 | 2026-07-04 22:41 +08:00 | Requirement R-001, SD §2, §14 | T-REL-002 | TC-REL-002 tool install/run help | [@REL-002](WBS.REL-002.md) |
| REL-003 | codex.fs.host standalone tool entrypoint | REL-002;HOST-003;E2E-002 | 100 | Done | None | 2026-07-04 22:55 +08:00 | 2026-07-04 23:06 +08:00 | Requirement R-001, SD §2, §9 | T-REL-003 | TC-REL-003 host tool start/status | [@REL-003](WBS.REL-003.md) |
| REL-004 | Global tool install and host handoff | REL-003;HOST-004;DOC-004 | 100 | Done | None | 2026-07-05 00:41 +08:00 | 2026-07-05 01:00 +08:00 | SD §2, §9, §10 | T-REL-004 | TC-REL-004 global tool install + LAN host docs | [@REL-004](WBS.REL-004.md) |
| E2E-001 | Installed engine probe real path | CDX-003;AGY-003 | 100 | Done | None | 2026-07-04 19:41 +08:00 | 2026-07-04 19:47 +08:00 | SD §15 | T-E2E-001 | TC-E2E-001 installed codex/agy probe real path | inline |
| E2E-002 | MessageFabric message to engine to reply | HOST-002;CLI-003 | 100 | Done | None | 2026-07-04 22:13 +08:00 | 2026-07-04 22:27 +08:00 | Requirement §10, SA §6.1, SD §14 | T-E2E-002 | TC-E2E-002 `misc/verifyMessageToEngineReply.fsx` | [@E2E-002](WBS.E2E-002.md) |
| E2E-003 | Multi-agent group collaboration | E2E-002;PTCS-002 | 100 | Done | None | 2026-07-04 23:28 +08:00 | 2026-07-04 23:38 +08:00 | Requirement §6.3 | T-E2E-003 | TC-E2E-003 multi-agent MessageFabric group | [@E2E-003](WBS.E2E-003.md) |
| OPS-001 | Process orphan recovery | EN-002;HOST-002 | 100 | Done | None | 2026-07-04 22:46 +08:00 | 2026-07-04 22:49 +08:00 | SA §9, SD §4 | T-OPS-001 | TC-OPS-001 orphan process recovery | [@OPS-001](WBS.OPS-001.md) |
| OPS-002 | Session persistence boundary | PTCS-003;SESS-001 | 100 | Done | None | 2026-07-04 23:44 +08:00 | 2026-07-04 23:46 +08:00 | SA §9, SD §11 | T-OPS-002 | TC-OPS-002 `misc/verifyMessageToEngineReply.fsx` boundary gate | [@OPS-002](WBS.OPS-002.md) |
| UI-001 | PTCS Web UI extension/RFC | E2E-002;DOC-003 | 100 | Done | None | 2026-07-04 23:13 +08:00 | 2026-07-04 23:23 +08:00 | Requirement §4, SD §16.12 | T-UI-001 | TC-UI-001 PTCS UI extension RFC/verifier | [@UI-001](WBS.UI-001.md) |
| UI-002 | PTCS local82 chat profile/browser correction | UI-001;HOST-005 | 100 | Done | None | 2026-07-05 01:22 +08:00 | 2026-07-05 01:22 +08:00 | SD §9, §16.12 | T-UI-002 | TC-UI-002 real PTCS Host 82 login/send + worker visibility boundary | [@UI-002](WBS.UI-002.md) |
| PRODUCT-001 | Product responsibility reset RFC and stock docs | UI-002;HOST-007;CLI-009 | 100 | Done | None | 2026-07-05 04:05 +08:00 | 2026-07-05 04:22 +08:00 | Requirement §2.1, SA §3.6, SD §2, §9, §11, §14.2 | T-PRODUCT-001 | TC-PRODUCT-001 product boundary doc traceability | [@PRODUCT-001](WBS.PRODUCT-001.md) |
| RUNTIME-001 | Runtime prompt-loop package boundary RFC | PRODUCT-001 | 100 | Done | None | 2026-07-05 04:13 +08:00 | 2026-07-05 04:25 +08:00 | SD §2, §11.3, §16 | T-RUNTIME-001 | TC-RUNTIME-001 runtime RFC + pure prompt-loop verifier plan | [@RUNTIME-001](WBS.RUNTIME-001.md) |
| ACTOR-001 | SessionWorker sharded actor model RFC | PRODUCT-001;RUNTIME-001;PTCS-003 | 100 | Done | None | 2026-07-05 04:16 +08:00 | 2026-07-05 04:34 +08:00 | SD §8, §11.2, §17 | T-ACTOR-001 | TC-ACTOR-001 actor protocol RFC + PTCS ActorFabric verifier plan | [@ACTOR-001](WBS.ACTOR-001.md) |
| CLI-010 | Interactive participant CLI client RFC | PRODUCT-001;CLI-009 | 100 | Done | None | 2026-07-05 04:21 +08:00 | 2026-07-05 04:22 +08:00 | SD §14, §14.2 | T-CLI-010 | TC-CLI-010 interactive participant CLI RFC + installed verifier plan | [@CLI-010](WBS.CLI-010.md) |
| WEB-001 | PTCS AI chat bundle RFC | PRODUCT-001;UI-002 | 100 | Done | None | 2026-07-05 04:32 +08:00 | 2026-07-05 04:34 +08:00 | SD §14.1, §14.2 | T-WEB-001 | TC-WEB-001 PTCS WebSharper bundle RFC + real browser verifier plan | [@WEB-001](WBS.WEB-001.md) |
| PERSIST-001 | Transcript note/artifact store RFC | PRODUCT-001;OPS-002 | 100 | Done | None | 2026-07-05 04:27 +08:00 | 2026-07-05 04:27 +08:00 | SD §12, §13, RFC-PRODUCT-0001 | T-PERSIST-001 | TC-PERSIST-001 transcript/note artifact RFC + verifier plan | [@PERSIST-001](WBS.PERSIST-001.md) |
| WEBR-001 | PTCS classic webshell rewrite RFC/WBS reset | WEB-001;ACTOR-001;PERSIST-001 | 100 | Done | None | 2026-07-05 10:30 +08:00 | 2026-07-05 10:30 +08:00 | SD §9, §14.3 | T-WEBR-001 | TC-WEBR-001 RFC/WBS reset traceability | [@WEBR-001](WBS.WEBR-001.md) |
| WEBR-002 | PTCS classic shell and Dynamic bundle baseline inventory | WEBR-001 | 100 | Done | None | 2026-07-05 10:45 +08:00 | 2026-07-05 10:47 +08:00 | SD §14.3 | T-WEBR-002 | `misc/verifyPtcsClassicShellInventory.fsx` | [@WEBR-001](WBS.WEBR-001.md); [inventory](WEBR-002.PTCS-classic-shell-inventory.md) |
| WEBR-003 | Create `codex.fs.web` WebSharper Bundle project | WEBR-002 | 0 | Planned | None | - | 2026-07-05 10:47 +08:00 | SD §14.3 | T-WEBR-003 | `misc/verifyCodexFsWebBundle.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| WEBR-004 | Implement `useAIChat(...)` CommHub registration/server extension | WEBR-003 | 0 | Planned | WEBR-003 bundle scaffold | - | 2026-07-05 10:30 +08:00 | SD §14.3 | T-WEBR-004 | `misc/verifyUseAIChatRegistration.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| WEBR-005 | Add product `ptcs-webshell` host mode or PTCS Host composition path | WEBR-004 | 0 | Planned | WEBR-004 registration | - | 2026-07-05 10:30 +08:00 | SD §9, §14.3 | T-WEBR-005 | `misc/verifyHostPtcsWebProfile.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| RUNTIME-002 | Extract/complete reusable runtime prompt-loop modules | RUNTIME-001;PERSIST-001 | 0 | Planned | None | - | 2026-07-05 10:30 +08:00 | SD §11.3, §12 | T-RUNTIME-002 | `misc/verifyRuntimeLoopExtraction.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| ACTOR-002 | Implement PTCS ActorFabric Foreman/Worker proof | ACTOR-001;RUNTIME-002 | 0 | Planned | RUNTIME-002 | - | 2026-07-05 10:30 +08:00 | SD §11.2, §14.3 | T-ACTOR-002 | `misc/verifyPtcsActorFabricForeman.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| WEBR-006 | Add AI target/perspective/invocation controls in PTCS shell | WEBR-004;ACTOR-002 | 0 | Planned | ACTOR-002 visible participants | - | 2026-07-05 10:30 +08:00 | SD §14.2, §14.3 | T-WEBR-006 | `misc/verifyAiIntentControls.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| WEBR-007 | Render artifact/note refs in PTCS shell | WEBR-006;PERSIST-001 | 0 | Planned | runtime artifact provider | - | 2026-07-05 10:30 +08:00 | SD §12, §14.3 | T-WEBR-007 | `misc/verifyArtifactRefsInPtcsShell.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| WEBR-008 | Remove/deprecate standalone web-chat product path | WEBR-005 | 0 | Planned | product web profile exists | - | 2026-07-05 10:30 +08:00 | SD §9, §14.3 | T-WEBR-008 | `misc/verifyNoStandaloneChatProductPath.fsx` | [@WEBR-001](WBS.WEBR-001.md) |
| E2E-004 | Real PTCS classic browser AI chat E2E | WEBR-006;WEBR-007;ACTOR-002 | 0 | Planned | all implementation slices | - | 2026-07-05 10:30 +08:00 | SD §14.3 | T-E2E-004 | `misc/verifyPtcsAiChatE2E.fsx` | [@WEBR-001](WBS.WEBR-001.md) |

## 3. Roll-up / Detail Files

| Detail | Purpose |
| --- | --- |
| [@PTCS-002](WBS.PTCS-002.md) | MessageFabric session binding is a high-risk integration slice; detail file tracks exact PTCS operations and acceptance gates. |
| [@PTCS-003](WBS.PTCS-003.md) | Durable task handoff uses real PTCS `CommSpaDurableMessageFabric` ticketed admission; crash-durable recovery remains OPS-002/future provider profile scope. |
| [@HOST-003](WBS.HOST-003.md) | Host control endpoint uses HTTP with bind/advertise config; localhost is dev-only. |
| [@HOST-004](WBS.HOST-004.md) | Host root URL is a human-facing landing page verified by browser/Playwright, not a 404 or hidden health-only endpoint. |
| [@HOST-005](WBS.HOST-005.md) | Existing PTCS hosts can start codex.fs runtime with caller-owned `CommSpaMessageFabric` so UI and workers share participant truth. |
| [@HOST-006](WBS.HOST-006.md) | Historical alpha.5 standalone `/chat` PoC; superseded by HOST-007 because product browser chat belongs to PTCS WebSharper. |
| [@HOST-007](WBS.HOST-007.md) | Standalone `/chat` is now a PTCS chat guard page; diagnostics prompt send moved to `/diagnostics/session-send` and OpenAPI includes the foreman route. |
| [@DOC-001](WBS.DOC-001.md) | API documentation toolchain selection affects NuGet SDK docs and Swagger generation. |
| [@DOC-003](WBS.DOC-003.md) | OpenAPI JSON and Swagger UI verification uses the real host endpoint through advertised non-loopback URI. |
| [@DOC-004](WBS.DOC-004.md) | API/SDK docs handoff requires visible Swagger/OpenAPI plus packaged XML docs evidence. |
| [@CLI-001](WBS.CLI-001.md) | Terminal client command surface is a compiled FAkka.Argu parser; real host execution is deferred to CLI-002/CLI-003. |
| [@CLI-002](WBS.CLI-002.md) | CLI session send uses real host HTTP endpoint and PTCS MessageFabric; attach/drain/status remains CLI-003. |
| [@CLI-003](WBS.CLI-003.md) | CLI status/attach/drain read the real session inbox through host control endpoints and drain acknowledges the cursor. |
| [@CLI-004](WBS.CLI-004.md) | Terminal self-use verifies compiled CLI against a LAN-advertised host and hardens `host status` plus `@file` prompt input. |
| [@CLI-005](WBS.CLI-005.md) | Historical alpha.3 command-name correction; superseded by CLI-006 which restores explicit `codex.fs.cli` and keeps `codex.fs` as an alias. |
| [@CLI-006](WBS.CLI-006.md) | `codex.fs.cli` is the canonical PoC command and `codex.fs` is a short alias package over the same command surface. |
| [@CLI-007](WBS.CLI-007.md) | `session send` defaults to the derived SessionWorker/foreman participant and only uses a specified worker when `--worker-id` is supplied. |
| [@CLI-008](WBS.CLI-008.md) | CLI HTTP transport failures return readable non-zero errors instead of unhandled .NET stack traces. |
| [@CLI-009](WBS.CLI-009.md) | First-use `session send` no longer requires a user-known session id; blank session routes to default `foreman`. |
| [@CLI-010](WBS.CLI-010.md) | Interactive terminal participant client RFC defines Foreman default, target/perspective switching and invocation-option handoff without making CLI the prompt-loop owner. |
| [@E2E-002](WBS.E2E-002.md) | First closed-loop real path spans MessageFabric, host, engine, artifacts and reply. |
| [@REL-002](WBS.REL-002.md) | `codex.fs.cli` installs and runs as a local dotnet tool from generated nupkg; host standalone tool is tracked separately. |
| [@REL-003](WBS.REL-003.md) | `codex.fs.host.tool` installs as a dotnet tool and exposes command name `codex.fs.host`; host library remains referenceable. |
| [@REL-004](WBS.REL-004.md) | User-facing handoff verifies global tool path, LAN host URL, root page, OpenAPI JSON and Swagger UI. |
| [@E2E-003](WBS.E2E-003.md) | Non-durable multi-agent collaboration uses real PTCS MessageFabric group/direct messages; durable hardening remains PTCS-003/OPS-002. |
| [@OPS-001](WBS.OPS-001.md) | Process orphan recovery kills only a pid/name/start-time matched codex.fs-owned lease. |
| [@OPS-002](WBS.OPS-002.md) | Session single-cycle runner writes a ready-to-ack persistence boundary after reply evidence and before MessageFabric ack. |
| [@UI-001](WBS.UI-001.md) | PTCS Web UI extension RFC defines codex.fs as a PTCS extension consumer over MessageFabric, not a new UI fabric. |
| [@UI-002](WBS.UI-002.md) | PTCS Host profile verification uses `http://127.0.0.1:82/chat` local-login for real browser prompt send; 81 OAuth redirect is expected for the public profile. |
| [@PRODUCT-001](WBS.PRODUCT-001.md) | Product reset distinguishes PTCS Host, codex.fs.host, runtime, actor, CLI, Web and persistence boundaries before further implementation. |
| [@RUNTIME-001](WBS.RUNTIME-001.md) | Runtime prompt-loop boundary owns orchestration and side-effect ordering; host/actor/PTCS/CLI/Web remain adapters. |
| [@ACTOR-001](WBS.ACTOR-001.md) | Actor RFC defines WorkerActor / specialized SessionActor, Foreman participant, MessageFabric scopes and delivery/ack ordering. |
| [@WEB-001](WBS.WEB-001.md) | PTCS AI chat bundle RFC defines `codex.fs.web` / `useAIChat(...)` as a WebSharper extension over PTCS MessageFabric, with Foreman/worker/public/group targets, authorized perspective and artifact refs. |
| [@PERSIST-001](WBS.PERSIST-001.md) | Transcript/note/artifact policy defines private raw run evidence, public redacted export, note summaries, compact refs and ready-to-ack boundary requirements. |
| [@WEBR-001](WBS.WEBR-001.md) | Reset/rewrite backlog: product Web must be PTCS classic chat shell plus codex.fs WebSharper Bundle and ActorFabric-backed AI workers; standalone diagnostics/guard pages are cut from product acceptance. |

## 4. Update Rule

When any row changes:

1. Update `Progress`, `Status`, `Blocker`, `StartTime`, `UpdatedAt`.
2. Update linked `doc/Test.md` row status and evidence.
3. If the change is API-facing, update comments/examples/parameter/output docs per SD §10.
4. Keep long evidence in `WBS.<ID>.md` or operation logs, not in the WBS table.
