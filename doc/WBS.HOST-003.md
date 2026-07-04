# WBS Detail: HOST-003 Host Control Endpoint

WBS ID：`HOST-003`  
狀態：Planned  
Progress：0  
StartTime：未動工  
UpdatedAt：2026-07-04 18:10 +08:00  
Previous：`HOST-002`  
SD：`SD §9`, `SD §17 SD-TBD-001`  
Test：`T-HOST-003`

## Scope

定義 `codex.fs.cli` 與 `codex.fs.host` 的 control endpoint protocol。此 endpoint 只能作 control plane；message truth source 仍是 PTCS MessageFabric。

## Decision

- MVP protocol: HTTP.
- Single-node development may use loopback only when explicitly configured.
- Production and sharded cluster profiles must bind to a LAN or routable address and publish an advertised URI.
- Other nodes and CLI clients must use the advertised URI, not `localhost`.
- Actor-to-actor/session workflow still uses PTCS `CommSpaActorFabric` / `CommSpaMessageFabric`; HTTP does not become a second message fabric.

## Acceptance

- Endpoint protocol and network profile documented in SD before implementation.
- Config includes bind address, optional port, advertised URI and loopback-only dev switch.
- Cluster/runtime verifier uses advertised LAN/routable URI, not loopback.
- OpenAPI metadata and Swagger generation are mandatory per SD §10.
- `codex.fs.cli` must not write MessageFabric streams or artifacts directly.

## Blockers

- `HOST-002` minimal host runtime.
