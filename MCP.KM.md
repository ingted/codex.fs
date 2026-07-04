# MCP.KM

## 2026-07-04 SESS-002 PromptAssembly

- `CodexFs.PromptAssembly.assemble` is intentionally pure: PTCS polling, ack, persistence and engine process execution remain host/actor responsibilities.
- Prompt assembly input carries `SessionId`, `RunId`, participant id, engine selection, working directory, policy, and an ordered message batch.
- Message body rendering must use a markdown fence longer than any backtick run in the body, so user/agent content cannot prematurely close the fenced block.
- `PromptAssemblyResult.LastCursor` is derived from the last available message cursor and is the value a host can persist before later ack behavior.

## 2026-07-04 SESS-003 Compaction

- MVP compaction is deterministic and rule-based in `CodexFs.Compaction`; it does not consume Codex/Agy tokens and does not start engine processes.
- Retention-sensitive entries are default-preserved when kind is `Decision`, `Blocker`, `OpenItem`, `Run`, or `Artifact`, or when the entry carries PTCS message refs, run ids, or artifact refs.
- `MaxSummaryChars` is a soft budget for non-critical recent entries. Mandatory retained content may exceed the budget and sets `OverBudget = true`.
- Future LLM/engine compaction should be an adapter over the same `CompactionEntry` / `CompactionResult` contract, not a replacement for durable history or artifact storage.

## 2026-07-04 PTCS-001 Reference Range

- First supported PTCS dependency is `PulseTrade.Comm.Spa [0.2.5-beta71]`.
- `codex.fs.ptcs` owns the PTCS reference and compile-time boundary; `codex.fs` core remains independent from PTCS runtime packages.
- PTCS beta71 depends on `FAkka.Argu 10.1.301`, so `codex.fs` aligns its direct FAkka.Argu reference to exact `[10.1.301]`.
- Compile proof uses concrete PTCS types `PulseTrade.Comm.Spa.CommSpaMessageFabric` and `PulseTrade.Comm.Spa.CommSpaActorFabricOptions`.
