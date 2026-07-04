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
| T-EN-001 | EN-001 | TC-EN-001 `CodexFs.Engine` build/XML docs | Compile | Real package compile | CF-002 | build succeeded with 0 warnings/errors; engine contract XML docs generated | Pass | None | SD §4 |
| T-EN-002 | EN-002 | TC-EN-002 temp FSI success/timeout kill | Unit/Ops | Controlled command fixture only; not production validation | EN-001 | temp internal-only FSI verified success exit 0 and timeout outcome `TimedOut` with killed process exit -1 | Pass | None | SD §4, SA §9 |
| T-CDX-001 | CDX-001 | TC-CDX-001 codex 0.142.4 exec help/version fixture | Fixture | Captured fixture parser, not live CLI readiness | EN-001 | fixture parser produced `codex-exec-0.142` with SingleTurnHeadless, Continuation, StructuredEventStream, FinalMessageFile, WorkspaceDirectories, SandboxMode, ModelSelection | Pass | None | SD §5 |
| T-CDX-002 | CDX-002 | TC-CDX-002 codex Argu parse/render | Unit | Real FAkka.Argu DU/render function | CDX-001 | internal-only FSI parsed repeatable config/enable/disable/image/add-dir, sandbox/color enum, json/final output; renderer emitted deterministic argv and quoted display | Pass | None | SD §5, §7 |
| T-CDX-003 | CDX-003 | TC-CDX-003 codex stdout/stderr/event/final artifact map | Fixture/Unit | Captured output fixture; live covered by E2E | CDX-002 | temp artifact root wrote stdout/stderr/event/final files; manifest refs were StdoutLog, StderrLog, EventJsonl, FinalMarkdown | Pass | None | SD §5, §12 |
| T-AGY-001 | AGY-001 | TC-AGY-001 agy 1.0.16 help/version fixture | Fixture | Captured fixture parser, not live CLI readiness | EN-001 | fixture parser produced `agy-print-1.0` with SingleTurnHeadless, Continuation, WorkspaceDirectories, SandboxMode, ModelSelection, Timeout, LogFile | Pass | None | SD §6 |
| T-AGY-002 | AGY-002 | TC-AGY-002 agy Argu parse/render | Unit | Real FAkka.Argu DU/render function | AGY-001 | internal-only FSI parsed `--prompt` alias, repeatable `--add-dir`, `--print-timeout 2m30s`; renderer emitted deterministic argv and quoted display | Pass | None | SD §6, §7 |
| T-AGY-003 | AGY-003 | TC-AGY-003 agy stdout/stderr/final artifact map | Fixture/Unit | Captured output fixture; live covered by E2E | AGY-002 | temp artifact root wrote stdout/stderr/final files; manifest refs were StdoutLog, StderrLog, FinalMarkdown | Pass | None | SD §6, §12 |
| T-PTCS-001 | PTCS-001 | TC-PTCS-001 `tests/codex.fs.Tests` PTCS restore/reference | Compile | Real PackageReference/restore path | CF-001 | `dotnet restore .\codex.fs.slnx`, `dotnet build .\codex.fs.slnx --no-restore`, and `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; `codex.fs.ptcs` references exact `PulseTrade.Comm.Spa [0.2.5-beta71]` and asserts PTCS concrete types | Pass | None | SD §8, §17 |
| T-PTCS-002 | PTCS-002 | TC-PTCS-002 `tests/codex.fs.Tests` MessageFabric binding | Integration | Real `CommSpaMessageFabric`; no fake mailbox | PTCS-001 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; real `CommHub.createEmpty()` + `CommSpaMessageFabric.create` covered register, direct send, poll, ack, wait, drain, group upsert and group poll | Pass | None | SD §8 |
| T-PTCS-003 | PTCS-003 | TC-PTCS-003 durable handoff | Integration | Real `CommSpaDurableMessageFabric` volatile durable admission profile; no fake mailbox | PTCS-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; real PTCS durable wrapper registered caller/worker participants, `SubmitAgentTaskDurableAsync` accepted a ticket/result handle, the target worker inbox received the agent-task envelope, provider proof failed closed for sharded crash-durable production requirements, and durable ack completed | Pass | None | SD §8 |
| T-SESS-001 | SESS-001 | TC-SESS-001 pure decide transitions | Unit | Real `SessionBehavior.decide` | CF-002 | internal-only FSI covered Tick, InboxBatchReceived, PromptPrepared, EngineRunCompleted, ReplySent, InboxAcked transitions | Pass | None | SD §11 |
| T-SESS-002 | SESS-002 | TC-SESS-002 `tests/codex.fs.Tests` prompt batch assembly | Unit | Real prompt assembly over message refs | SESS-001 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; prompt content, ordering, metadata, cursor, truncation and fence assertions | Pass | None | SA §6, SD §11 |
| T-SESS-003 | SESS-003 | TC-SESS-003 `tests/codex.fs.Tests` compact preserves blockers | Unit | Real rule-based compactor over persisted history entry refs; LLM compact optional later | SESS-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; compact summary retained blocker, decision, open item, PTCS message id, run id, artifact ref and recent context | Pass | None | Requirement R-005, SD §11.1 |
| T-HOST-001 | HOST-001 | TC-HOST-001 config parse/redaction | Unit/Ops | Real config loader; no secret echo | CF-005/PTCS-001 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; `CodexFs.HostConfig.loadFromMap` parsed camelCase keys, applied non-loopback HTTP config, redacted token-like diagnostic value, and rejected production loopback advertise/bind config | Pass | None | SD §9 |
| T-HOST-002 | HOST-002 | TC-HOST-002 host runtime/health | Integration | Real host runtime with selected PTCS mode | HOST-001/PTCS-002/SESS-001 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; `codex.fs.host` created runtime from `HostConfig`, initialized real in-process PTCS `CommSpaMessageFabric` without ActorSystem binding, produced non-secret health and redacted summary, and cleared fabric on stop | Pass | None | SD §9 |
| T-HOST-003 | HOST-003 | TC-HOST-003 endpoint contract | Integration/Docs | Real HTTP endpoint with bind address and advertised LAN/routable URI; loopback only in dev profile | HOST-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; Kestrel bound a non-loopback IPv4, advertised `/api/codexfs/host/health`, returned HTTP 200 JSON, exposed endpoint examples/metadata, and omitted raw executable override tokens | Pass | None | SD §9, §17 |
| T-DOC-001 | DOC-001 | TC-DOC-001 docs toolchain decision | Docs | Official-docs-backed toolchain decision plus real XML docs from current public package build | CF-001 | `WBS.DOC-001.md` decision recorded; SD §10 updated; `src/codex.fs/bin/Debug/net10.0/codex.fs.xml` exists and contains public API XML members | Pass | None | SD §10 |
| T-DOC-002 | DOC-002 | TC-DOC-002 XML docs generated | Docs | Real XML docs from public package compile | DOC-001/CF-002 | XML docs file exists; XML parse succeeded; 322 member entries; required core public members present with non-empty summaries | Pass | None | SD §10 |
| T-DOC-003 | DOC-003 | TC-DOC-003 OpenAPI available | Docs/Integration | Real HTTP host endpoint through advertised URI, not localhost-only | DOC-001/HOST-003 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; `/openapi/v1.json` returned HTTP 200 JSON with `3.x` version and health path; `/docs/index.html` returned HTTP 200 Swagger UI HTML through the advertised non-loopback URI | Pass | None | SD §10 |
| T-CLI-001 | CLI-001 | TC-CLI-001 Argu parser/help | Unit/CLI | Real compiled CLI parser | HOST-003 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; help listed session/run/host/engine groups, examples were present, valid commands parsed, invalid arg returned Argu error | Pass | None | SD §14 |
| T-CLI-002 | CLI-002 | TC-CLI-002 CLI send through MessageFabric | Integration | Real host + real MessageFabric | CLI-001/PTCS-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; CLI HTTP client posted to advertised non-loopback host URI, host returned 202, and host status endpoint read the real PTCS inbox for the derived session participant | Pass | None | SD §14 |
| T-CLI-003 | CLI-003 | TC-CLI-003 attach/drain/status | Integration/CLI | Real host + real MessageFabric | CLI-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; CLI status/attach returned transcript JSON without ack, drain returned the prompt and acknowledged the inbox cursor, and after-drain status returned pendingCount 0 | Pass | None | SD §14 |
| T-REL-001 | REL-001 | TC-REL-001 pack metadata/docs | Compile/Docs | Real `dotnet pack` | DOC-002/CF-001 | nupkg contains README.md, lib/net10.0 dll/xml, MIT license expression, repository metadata, FAkka.Argu/FSharp.Core dependencies | Pass | None | SD §2, §10 |
| T-REL-002 | REL-002 | TC-REL-002 tool install/run help | CLI/Package | Real local tool install from nupkg | REL-001/HOST-001/CLI-003 | `dotnet tool install codex.fs.cli --tool-path G:\codex.fs\bin\rel002-tool-202607042243 --add-source G:\codex.fs\bin\rel002-packs-202607042243 --version 0.1.0-alpha.1` passed; installed `codex.fs.cli.exe --help` returned exit code 0 and rendered command groups/examples | Pass | None | Requirement R-001, SD §14 |
| T-REL-003 | REL-003 | TC-REL-003 host tool start/status | CLI/Package/Host | Real local host tool install and bounded host status/start | REL-002/HOST-003/E2E-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; local `codex.fs.host.tool` package installed to `G:\codex.fs\bin\rel003-host-tool-202607042303`; installed `codex.fs.host.exe --help`, `status`, and `start --run-seconds 0` passed with LAN advertised URIs `http://10.28.112.93:10437` and `http://10.28.112.93:10440` | Pass | None | Requirement R-001, SD §9 |
| T-E2E-001 | E2E-001 | TC-E2E-001 installed codex/agy probe real path | E2E | Real installed Codex/Agy where available | CDX-003/AGY-003 | ProcessRunner probed installed Codex `0.142.4` and Agy `1.0.16`; parsed surfaces `codex-exec-0.142` and `agy-print-1.0`; Agy help direct output observed on stderr | Pass | None | SD §15 |
| T-E2E-002 | E2E-002 | TC-E2E-002 `misc/verifyMessageToEngineReply.fsx` | E2E | Real MessageFabric, host, installed Agy engine, artifact store | HOST-002/CLI-003 | `dotnet fsi --exec .\misc\verifyMessageToEngineReply.fsx` passed; verifier sent a real PTCS direct message, ran Agy `--print`, wrote prompt/batch/request/rendered-argv/stdout/stderr/final/result/manifest artifacts under `.codex.fs/e2e002-artifacts`, sent PTCS reply containing manifest reference, and verified session inbox empty after ack | Pass | None | Requirement §10, SA §6.1 |
| T-E2E-003 | E2E-003 | TC-E2E-003 multi-agent MessageFabric group | E2E | Real PTCS group/direct messages; durable optional | E2E-002/PTCS-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; alpha/beta session-worker participants joined a real PTCS MessageFabric group, beta received alpha's group task, beta sent a direct reply to alpha, and alpha acked the reply cursor | Pass | None | Requirement §6.3 |
| T-OPS-001 | OPS-001 | TC-OPS-001 orphan process recovery | Ops | Controlled real OS process fixture | EN-002/HOST-002 | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed; test launched `powershell.exe Start-Sleep`, created a codex.fs process lease from pid/name/start time, recovered it via `recoverLeasedProcessAsync`, and verified the process exited | Pass | None | SA §9, SD §4 |
| T-OPS-002 | OPS-002 | TC-OPS-002 `misc/verifyMessageToEngineReply.fsx` boundary gate | Ops/Integration | Real PTCS MessageFabric, real Agy engine, real file artifact store; no fake stores | PTCS-003/SESS-001 | `dotnet fsi --exec .\misc\verifyMessageToEngineReply.fsx` passed; verifier wrote real prompt/batch/request/result/manifest/final artifacts, sent a PTCS reply, verified `session-boundary.json` contains ready-to-ack phase, reply message id and selected ack cursor, then verified the session inbox was empty after ack | Pass | None | SA §9, SD §11 |
| T-UI-001 | UI-001 | TC-UI-001 PTCS UI extension RFC/verifier | Docs/UI | Real PTCS extension path after backend E2E | E2E-002/DOC-003 | `doc/RFC/RFC-UI-0001.ptcs-web-ui-extension.md` accepted for RFC slice; verifier plan requires future real PTCS browser + MessageFabric cases and explicitly forbids fake/mock UI smoke as acceptance | Pass | None | SD §16.12 |

## 3. Verifier Script Names

These script names are verifier contracts. They must not be referenced as passing evidence until the scripts exist and have been executed.

| Script | Purpose | Related tests |
| --- | --- | --- |
| `misc/verifySolutionBuild.fsx` | Restore/build and package graph check. | T-CF-001 |
| `misc/verifyArtifactStore.fsx` | File artifact store append-only and manifest integrity. | T-CF-004 |
| `misc/verifyPtcsMessageFabric.fsx` | Real PTCS register/send/poll/wait/ack/drain path. | T-PTCS-002 |
| `misc/verifyInstalledEngines.fsx` | Real installed Codex/Agy probe and capability map. | T-E2E-001 |
| `misc/verifyMessageToEngineReply.fsx` | First closed-loop participant -> MessageFabric -> worker -> engine -> artifact -> reply path. Implemented and passed for Agy single-cycle profile. | T-E2E-002 |

## 4. Evidence Rule

Each executed test update must record:

- command or verifier path;
- environment/profile;
- result summary;
- artifact/log path;
- related WBS ID;
- whether it is fixture-only, integration, or real path evidence.

Long evidence belongs in operation logs or `WBS.<ID>.md`, not in the matrix.
