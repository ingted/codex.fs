# WBS Detail: DOC-004 API/SDK docs handoff gate

WBS ID：`DOC-004`
狀態：Done
Progress：100
StartTime：2026-07-05 00:41 +08:00
UpdatedAt：2026-07-05 01:00 +08:00
Previous：`DOC-003`, `HOST-004`
RFC：`RFC-OPS-0001`
SD：`SD §10`
Test：`T-DOC-004`

## Scope

把「API 文件或 SDK 文件要出來」變成交付 gate：host 啟動時要能透過瀏覽器看到 Swagger UI，透過 machine endpoint 取得 OpenAPI JSON，package output 要包含 SDK XML docs。

## Decision

- OpenAPI JSON remains the host HTTP contract source: `GET /openapi/v1.json`.
- Swagger UI is the human-facing API browser: `GET /docs/index.html` in the current profile.
- SDK docs baseline is generated XML docs in packed `lib/net10.0/*.xml` files.
- A future fsdocs/reference site can build on XML docs, but alpha.2 handoff must at least produce visible Swagger/OpenAPI and packaged XML docs.

## Implementation

- `HostControl.endpointDefinitions` now includes the root endpoint metadata alongside health/session endpoints.
- `DEVOP.md` documents OpenAPI, Swagger UI and package XML docs outputs.
- README quick start points users to root, health, OpenAPI JSON and Swagger UI URLs.

## Verification

- Playwright opened `http://10.28.112.93:10481/docs/index.html` and observed Swagger UI endpoint list.
- HTTP verification checked `http://10.28.112.93:10481/openapi/v1.json` returned HTTP 200 and contains expected paths.
- `dotnet pack` produced alpha.2 packages with SDK XML docs under `lib/net10.0/*.xml`.
- Evidence:
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\summary.json`
  - `G:\codex.fs\.codex.fs\host-usability-playwright-20260705004149-alpha2\docs.png`
  - `G:\codex.fs\bin\host-usability-packs-20260705004149-alpha2`

## Blockers

- None.

## Follow-up

- Generated F# reference site via FSharp.Formatting/fsdocs remains a future documentation improvement after public API shape stabilizes.
