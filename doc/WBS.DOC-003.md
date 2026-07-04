# WBS Detail: DOC-003 OpenAPI / Swagger

WBS ID：`DOC-003`
狀態：Done
Progress：100
StartTime：2026-07-04 21:36 +08:00
UpdatedAt：2026-07-04 21:39 +08:00
Previous：`DOC-001`, `HOST-003`
SD：`SD §10`
Test：`T-DOC-003`

## Scope

在 `HOST-003` 的 real HTTP control endpoint 上啟用 OpenAPI JSON 與 Swagger UI route，並確保驗證使用 advertised non-loopback URI。

## Decision

- OpenAPI JSON uses `Microsoft.AspNetCore.OpenApi [10.0.9]`.
- `Microsoft.OpenApi [2.7.5]` is referenced directly to avoid the GHSA-v5pm-xwqc-g5wc affected 2.0.x transitive versions.
- Swagger UI uses `Swashbuckle.AspNetCore.SwaggerUI [10.2.3]` and is only exposed when the active profile sets `apiDocs.exposeSwaggerUi = true`.
- OpenAPI JSON remains the source of truth; Swagger UI is presentation only.

## Implementation

- `HostControl.mapApiDocs` maps `GET /openapi/v1.json` when `apiDocs.generateOpenApi = true`.
- `HostControl.mapApiDocs` enables Swagger UI at `/<apiDocs.swaggerRoutePrefix>/index.html` when Swagger UI is allowed.
- `HostControlContract` exposes `OpenApiJsonUri`, `SwaggerUiUri`, `GenerateOpenApi`, and `ExposeSwaggerUi`.
- `HostControlHealthResponse` reports the advertised docs URIs so CLI/Web/admin callers can inspect available docs endpoints without reading config files.

## Verification

- `dotnet restore .\codex.fs.slnx` passed without NU1903 after the `Microsoft.OpenApi [2.7.5]` override.
- `dotnet build .\codex.fs.slnx --no-restore` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore` passed and printed `TC-DOC-003 OpenAPI available passed`.
- `TC-DOC-003` dynamically selected a non-loopback IPv4 address and free port, then verified:
  - `GET <advertiseUri>/openapi/v1.json` returned HTTP 200 JSON.
  - OpenAPI JSON contained a `3.x` `openapi` value and the `/api/codexfs/host/health` path.
  - `GET <advertiseUri>/docs/index.html` returned HTTP 200 Swagger UI HTML.

## Blockers

- None.
