# Test Plan

版本：`0.1.0-draft`  
狀態：Draft  
對應文件：`doc/Requirement.md`, `doc/SA.md`, `doc/SD.md`, `doc/WBS.md`

## 1. 測試原則

本文件是 `doc/WBS.md` 的 canonical test map。每個 WBS leaf item 至少有一個 Test item；WBS row 不可標 `Done`，除非對應 Test item 為 `Pass`，或有明確 approved waiver 並回鏈 operation log。

測試分層：

| Type | Purpose |
| --- | --- |
| Compile | solution/project 能 restore/build，public API shape 可編譯。 |
| Unit | 純 domain、adapter rendering、parser、redaction、manifest 等 deterministic 行為。 |
| Fixture | 使用 captured CLI help/version/output fixtures；只驗證 parser/render/mapping，不代表 production real path。 |
| Integration | 使用真 PTCS `CommSpaMessageFabric` / `CommSpaActorFabric` 或 host runtime。 |
| E2E | 從 participant/message 到 worker/engine/artifact/reply 的 real path。 |
| Docs | XML docs、OpenAPI/Swagger、SDK reference generation。 |
| Ops | crash/recovery、process guard、redaction/secret hygiene。 |

重要限制：

- Fixture tests 不能作為 production readiness 驗收。
- 若 verifier 腳本尚未存在，Status 必須保持 `Planned` 或 `Blocked`。
- 涉及 MessageFabric 的驗收必須走 real PTCS path，不得以 fake/mock mailbox 取代交付驗收。
- API-facing 工項必須同步 SD §10 的 comments/examples/parameter/output docs。

Status 值：

| Status | 意義 |
| --- | --- |
| Planned | 已定義，尚未實作。 |
| Ready | 前置條件滿足，可執行。 |
| Blocked | 有明確 blocker。 |
| Pass | 已通過並有 evidence。 |
| Fail | 已執行但失敗。 |
| Waived | 有明確批准與風險記錄。 |

## 2. Test Matrix

| Test ID | WBS ID | Test case / verifier | Type | Real path requirement | Preconditions | Expected evidence | Status | Blocker | SD item |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| T-PLAN-001 | PLAN-001 | TC-PLAN-001 doc traceability | Docs | File-based doc trace is enough | WBS/Test docs exist | `check.fsx` doc traceability PASS | Pass | None | SD §15-§16 |
| T-CF-001 | CF-001 | TC-CF-001 `dotnet restore/build .\codex.fs.slnx` | Compile | `dotnet restore/build` on real solution | Project scaffold exists | restore succeeded; build succeeded with 0 warnings and 0 errors | Pass | None | SD §2 |
| T-CF-002 | CF-002 | TC-CF-002 `CodexFs.Domain` build/XML docs | Unit | Real compiled core project | CF-001 | build succeeded with 0 warnings/errors; generated XML docs include `CodexFs.Domain` members | Pass | None | SD §3 |
| T-CF-003 | CF-003 | TC-CF-003 `CodexFs.Artifacts` build/XML docs | Unit | Real artifact manifest implementation | CF-002 | build succeeded with 0 warnings/errors; generated XML docs include artifact members | Pass | None | SD §12 |
| T-CF-004 | CF-004 | TC-CF-004 temp FSI write/sha/no-overwrite | Integration | Real file artifact store on temp workspace | CF-003 | temp artifact write succeeded; SHA-256 matched; overwrite rejected by `IOException` | Pass | None | SD §12 |
| T-CF-005 | CF-005 | TC-CF-005 temp FSI token-like redaction | Unit/Ops | Real redaction module | CF-004 | fake token-like sample redacted; safe text unchanged; one hit recorded | Pass | None | SD §13 |
| T-EN-001 | EN-001 | TC-EN-001 adapter contract compile | Compile | Real package compile | CF-002 | adapter contract compiles and XML docs generated | Planned | None | SD §4 |
| T-EN-002 | EN-002 | TC-EN-002 process timeout/kill fixture | Unit/Ops | Controlled command fixture only; not production validation | EN-001 | timeout, cancellation, kill-after-grace evidence | Planned | EN-001 | SD §4, SA §9 |
| T-CDX-001 | CDX-001 | TC-CDX-001 codex help/version fixture | Fixture | Captured fixture parser, not live CLI readiness | EN-001 | parsed surface/capabilities | Planned | EN-001 | SD §5 |
| T-CDX-002 | CDX-002 | TC-CDX-002 codex argv render | Unit | Real FAkka.Argu DU/render function | CDX-001 | rendered argv + redacted display snapshots | Planned | CDX-001 | SD §5, §7 |
| T-CDX-003 | CDX-003 | TC-CDX-003 codex output map | Fixture/Unit | Captured output fixture; live covered by E2E | CDX-002 | artifact mapping and final message path | Planned | CDX-002 | SD §5, §12 |
| T-AGY-001 | AGY-001 | TC-AGY-001 agy help/version fixture | Fixture | Captured fixture parser, not live CLI readiness | EN-001 | parsed surface/capabilities | Planned | EN-001 | SD §6 |
| T-AGY-002 | AGY-002 | TC-AGY-002 agy argv render | Unit | Real FAkka.Argu DU/render function | AGY-001 | rendered argv + redacted display snapshots | Planned | AGY-001 | SD §6, §7 |
| T-AGY-003 | AGY-003 | TC-AGY-003 agy output map | Fixture/Unit | Captured output fixture; live covered by E2E | AGY-002 | log/final/result mapping | Planned | AGY-002 | SD §6, §12 |
| T-PTCS-001 | PTCS-001 | TC-PTCS-001 PTCS restore/reference | Compile | Real PackageReference/restore path | CF-001 | restore/build references exact PTCS version | Planned | SD-TBD-004 | SD §8, §17 |
| T-PTCS-002 | PTCS-002 | TC-PTCS-002 / planned `misc/verifyPtcsMessageFabric.fsx` | Integration | Real `CommSpaMessageFabric`; no fake mailbox | PTCS-001 | register/send/poll/wait/ack/drain evidence | Planned | PTCS-001 | SD §8 |
| T-PTCS-003 | PTCS-003 | TC-PTCS-003 durable handoff | Integration | Real `CommSpaDurableMessageFabric` profile | PTCS-002 | SubmitAgentTaskDurableAsync ticket/result evidence | Blocked | SA-TBD-004 | SD §8 |
| T-SESS-001 | SESS-001 | TC-SESS-001 pure decide transitions | Unit | Real `SessionBehavior.decide` | CF-002 | transition table tests | Planned | CF-002 | SD §11 |
| T-SESS-002 | SESS-002 | TC-SESS-002 prompt batch assembly | Unit | Real prompt assembly over message refs | SESS-001 | prompt content, ordering, metadata assertions | Planned | SESS-001 | SA §6, SD §11 |
| T-SESS-003 | SESS-003 | TC-SESS-003 compact preserves blockers | Unit/Integration | Rule-based compactor first; LLM compact optional later | SESS-002 | compact summary retains blockers, ids, artifacts | Planned | SD-TBD-003 | Requirement R-005 |
| T-HOST-001 | HOST-001 | TC-HOST-001 config parse/redaction | Unit/Ops | Real config loader; no secret echo | CF-005/PTCS-001 | config loaded, redacted diagnostics | Planned | PTCS-001 | SD §9 |
| T-HOST-002 | HOST-002 | TC-HOST-002 host boot/health | Integration | Real host runtime with selected PTCS mode | HOST-001/PTCS-002/SESS-001 | health output, non-secret PTCS metadata | Planned | dependencies | SD §9 |
| T-HOST-003 | HOST-003 | TC-HOST-003 endpoint contract | Integration/Docs | Real HTTP endpoint with bind address and advertised LAN/routable URI; loopback only in dev profile | HOST-002 | endpoint contract tests, advertised URI check, docs metadata | Planned | HOST-002 | SD §9, §17 |
| T-DOC-001 | DOC-001 | TC-DOC-001 docs generator spike | Docs | Real Swashbuckle/XML-doc invocation on sample/public API | CF-001 | selected toolchain note and generated sample | Planned | None | SD §10 |
| T-DOC-002 | DOC-002 | TC-DOC-002 XML docs generated | Docs | Real XML docs from public package compile | DOC-001/CF-002 | XML docs file and missing-docs check | Planned | DOC-001 | SD §10 |
| T-DOC-003 | DOC-003 | TC-DOC-003 OpenAPI available | Docs/Integration | Real HTTP host endpoint through advertised URI, not localhost-only | DOC-001/HOST-003 | OpenAPI JSON + Swagger UI route evidence | Planned | HOST-003 | SD §10 |
| T-CLI-001 | CLI-001 | TC-CLI-001 Argu parser/help | Unit/CLI | Real compiled CLI parser | HOST-003 | help output, invalid arg errors, examples | Planned | HOST-003 | SD §14 |
| T-CLI-002 | CLI-002 | TC-CLI-002 CLI send through MessageFabric | Integration | Real host + real MessageFabric | CLI-001/PTCS-002 | message accepted in PTCS inbox | Planned | dependencies | SD §14 |
| T-CLI-003 | CLI-003 | TC-CLI-003 attach/drain/status | Integration/CLI | Real host + real MessageFabric | CLI-002 | attach/drain/status transcript artifact | Planned | CLI-002 | SD §14 |
| T-REL-001 | REL-001 | TC-REL-001 pack metadata/docs | Compile/Docs | Real `dotnet pack` | DOC-002/CF-001 | nupkg metadata, XML docs included | Planned | DOC-002 | SD §2, §10 |
| T-REL-002 | REL-002 | TC-REL-002 tool install/run help | CLI/Package | Real local tool install from nupkg | REL-001/HOST-001 | tool install output and `--help` | Planned | dependencies | Requirement R-001 |
| T-E2E-001 | E2E-001 | TC-E2E-001 / planned `misc/verifyInstalledEngines.fsx` | E2E | Real installed Codex/Agy where available | CDX-003/AGY-003 | probe result with capability map | Planned | local engine availability | SD §15 |
| T-E2E-002 | E2E-002 | TC-E2E-002 / planned `misc/verifyMessageToEngineReply.fsx` | E2E | Real MessageFabric, host, installed engine, artifact store | HOST-002/CLI-002 | prompt, run manifest, final reply reference | Planned | dependencies | Requirement §10, SA §6.1 |
| T-E2E-003 | E2E-003 | TC-E2E-003 multi-agent MessageFabric group | E2E | Real PTCS group/direct messages; durable optional | E2E-002/PTCS-003 | two session workers exchange message/reply | Planned | PTCS-003 optional | Requirement §6.3 |
| T-OPS-001 | OPS-001 | TC-OPS-001 orphan process recovery | Ops | Controlled real OS process fixture | EN-002/HOST-002 | recovery detects or kills orphan process | Planned | dependencies | SA §9 |
| T-OPS-002 | OPS-002 | TC-OPS-002 recovery/ack ordering | Ops/Integration | Real selected durability profile | PTCS-003/SESS-001 | no ack before durable artifact/reply evidence | Blocked | durable profile decision | SA §9, SD §11 |
| T-UI-001 | UI-001 | TC-UI-001 PTCS UI extension RFC/verifier | Docs/UI | Real PTCS extension path after backend E2E | E2E-002/DOC-003 | RFC accepted + UI verifier plan | Planned | E2E-002/DOC-003 | SD §16.12 |

## 3. Planned Verifier Script Names

These script names are planned contracts. They must not be referenced as passing evidence until the scripts exist and have been executed.

| Script | Purpose | Related tests |
| --- | --- | --- |
| `misc/verifySolutionBuild.fsx` | Restore/build and package graph check. | T-CF-001 |
| `misc/verifyArtifactStore.fsx` | File artifact store append-only and manifest integrity. | T-CF-004 |
| `misc/verifyPtcsMessageFabric.fsx` | Real PTCS register/send/poll/wait/ack/drain path. | T-PTCS-002 |
| `misc/verifyInstalledEngines.fsx` | Real installed Codex/Agy probe and capability map. | T-E2E-001 |
| `misc/verifyMessageToEngineReply.fsx` | First closed-loop participant -> MessageFabric -> worker -> engine -> artifact -> reply path. | T-E2E-002 |

## 4. Evidence Rule

Each executed test update must record:

- command or verifier path;
- environment/profile;
- result summary;
- artifact/log path;
- related WBS ID;
- whether it is fixture-only, integration, or real path evidence.

Long evidence belongs in operation logs or `WBS.<ID>.md`, not in the matrix.
