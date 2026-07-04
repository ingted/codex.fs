# WBS Detail: DOC-001 API Documentation Toolchain Spike

WBS ID：`DOC-001`  
狀態：Done  
Progress：100  
StartTime：2026-07-04 19:26 +08:00  
UpdatedAt：2026-07-04 19:34 +08:00  
Previous：`CF-001`  
SD：`SD §10`, `SD §17 SD-TBD-006`  
Test：`T-DOC-001`

## Scope

評估並定案 comment-as-SDK-doc 與 OpenAPI/Swagger 生成工具鏈。

## Candidate Tools

| Area | Candidate |
| --- | --- |
| OpenAPI JSON | MVP decision: ASP.NET Core HTTP host + `Microsoft.AspNetCore.OpenApi` (`AddOpenApi` / `MapOpenApi`) |
| Swagger UI | `Swashbuckle.AspNetCore.SwaggerUi` only as UI assets over generated OpenAPI document; profile-gated |
| SDK reference | XML docs first; FSharp.Formatting/fsdocs preferred for F# API reference; DocFX remains optional future evaluation |
| CLI docs | FAkka.Argu command metadata + examples |

## Decision 2026-07-04

- OpenAPI source of truth is typed ASP.NET Core host endpoints plus XML comments/endpoint metadata, not hand-written YAML.
- Runtime OpenAPI JSON for the HTTP control plane should use `Microsoft.AspNetCore.OpenApi`; when host exists, `AddOpenApi` / `MapOpenApi` are the baseline.
- Swagger UI is optional presentation, not the generator. If enabled, use `Swashbuckle.AspNetCore.SwaggerUi` pointed at the `MapOpenApi` route and guard it by host profile.
- SDK docs source of truth is compiler XML documentation emitted by each public package. FSharp.Formatting/fsdocs is the preferred first static reference-site generator for F# APIs; DocFX remains a later cross-language/site evaluation item.
- CLI docs come from FAkka.Argu DU usage metadata plus examples kept beside command implementation.
- Real HTTP OpenAPI verification is deferred to `DOC-003` because `HOST-003` owns the HTTP endpoint contract.

## Sources

- Microsoft Learn: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0`
- Microsoft Learn: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/using-openapi-documents?view=aspnetcore-10.0`
- Microsoft Learn: `https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/xml-documentation`
- FSharp.Formatting docs: `https://fsprojects.github.io/FSharp.Formatting/apidocs.html`
- DocFX docs: `https://dotnet.github.io/docfx/docs/dotnet-api-docs.html`

## Acceptance

- Toolchain choice recorded in SD and this detail file.
- Current public package build emits XML docs at `src/codex.fs/bin/Debug/net10.0/codex.fs.xml`.
- HTTP OpenAPI JSON/UI generation is explicitly assigned to `DOC-003` after `HOST-003`; `DOC-001` does not claim an endpoint exists.
- Examples use non-secret sample values.

## Blockers

- None for MVP docs baseline.
- Static SDK reference generator implementation remains in `DOC-002` / release docs work.
- HTTP OpenAPI runtime verification remains blocked by `HOST-003` and is tracked by `DOC-003`.
