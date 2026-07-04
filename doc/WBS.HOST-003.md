# WBS Detail: HOST-003 Host Control Endpoint

WBS ID：`HOST-003`  
狀態：Blocked  
Progress：0  
StartTime：未動工  
UpdatedAt：2026-07-04 17:53 +08:00  
Previous：`HOST-002`  
SD：`SD §9`, `SD §17 SD-TBD-001`  
Test：`T-HOST-003`

## Scope

定義 `codex.fs.cli` 與 `codex.fs.host` 的 control endpoint protocol。此 endpoint 只能作 control plane；message truth source 仍是 PTCS MessageFabric。

## Options To Decide

| Option | Notes |
| --- | --- |
| HTTP | Enables OpenAPI/Swagger and easier admin tooling. |
| Named pipe | Local-only and lightweight, but needs separate docs surface. |
| stdin/stdout | Simple for tool mode, harder for concurrent session operations. |

## Acceptance

- Endpoint protocol documented in SD before implementation.
- If HTTP is selected, OpenAPI metadata and Swagger generation are mandatory per SD §10.
- `codex.fs.cli` must not write MessageFabric streams or artifacts directly.

## Blockers

- `SD-TBD-001` unresolved.
