# WBS Detail: HOST-003 Host Control Endpoint

WBS ID：`HOST-003`
狀態：Done
Progress：100
StartTime：2026-07-04 21:21 +08:00
UpdatedAt：2026-07-04 21:27 +08:00
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
- Endpoint metadata is OpenAPI-ready through typed DTOs, endpoint definitions, examples and `Produces<HostControlHealthResponse>`; generated OpenAPI JSON and Swagger UI evidence are tracked by `DOC-003`.
- `codex.fs.cli` must not write MessageFabric streams or artifacts directly.

## Blockers

- None.

## Implementation

- Added `CodexFs.Host.HostControl` in `src/codex.fs.host/HostControl.fs`.
- Route: `GET /api/codexfs/host/health`.
- Runtime: Kestrel HTTP listener using `control.bindAddress` + `control.port`.
- Client/admin URI: `HostControlContract.HealthUri`, derived from `control.advertiseUri`.
- Message boundary: HTTP remains control plane only; PTCS MessageFabric remains the message truth source.
- Actor boundary: this endpoint does not create an ActorSystem; production `CommSpaActorFabric` / sharded cluster binding must use LAN/routable addresses outside this HTTP control surface.

## Verification

- `dotnet restore .\codex.fs.slnx` passed.
- `dotnet build .\codex.fs.slnx --no-restore` passed before docs sync.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-HOST-003 endpoint contract passed`.
- `TC-HOST-003` dynamically selects a non-loopback IPv4 address and free port, calls `HostControlContract.HealthUri`, and asserts HTTP 200 JSON over the advertised URI.
