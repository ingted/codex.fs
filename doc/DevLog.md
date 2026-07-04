# DevLog

## 2026-07-04 20:04 +08:00 SESS-002 prompt assembly

- Scope: implemented `CodexFs.PromptAssembly` as a pure markdown prompt assembler for one session run.
- Behavior: renders run metadata, optional system instruction, history/summary references, additional context, MessageFabric message metadata and ordered message bodies.
- Safety: prompt body rendering uses dynamic markdown fences based on the longest backtick run, and optional per-message truncation.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-SESS-002 prompt batch assembly passed`.
- Traceability: WBS `SESS-002` and Test `T-SESS-002` updated to `Done` / `Pass`.

## 2026-07-04 20:12 +08:00 SESS-003 local compaction

- Scope: implemented `CodexFs.Compaction` as deterministic rule-based local compaction for persisted session history entries.
- Decision: resolved `SD-TBD-003` for MVP; compaction does not call selected engine or a dedicated LLM adapter. Future LLM-backed compaction can reuse the same core input/output contract.
- Behavior: retains decisions, blockers, open items, run entries, artifact entries, and any entry carrying PTCS message refs, run ids, or artifact refs; recent non-critical context is retained by policy.
- Tests: `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors; `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` printed `TC-SESS-002 prompt batch assembly passed` and `TC-SESS-003 compact preserves blockers passed`.
- Traceability: WBS `SESS-003` and Test `T-SESS-003` updated to `Done` / `Pass`; SD §11.1 and §17 updated.
