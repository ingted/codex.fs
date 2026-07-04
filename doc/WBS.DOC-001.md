# WBS Detail: DOC-001 API Documentation Toolchain Spike

WBS ID：`DOC-001`  
狀態：Planned  
Progress：0  
StartTime：未動工  
UpdatedAt：2026-07-04 17:53 +08:00  
Previous：`CF-001`  
SD：`SD §10`, `SD §17 SD-TBD-006`  
Test：`T-DOC-001`

## Scope

評估並定案 comment-as-SDK-doc 與 OpenAPI/Swagger 生成工具鏈。

## Candidate Tools

| Area | Candidate |
| --- | --- |
| OpenAPI/Swagger | ASP.NET Core OpenAPI + Swashbuckle or NSwag |
| SDK reference | XML docs + DocFX or FSharp.Formatting |
| CLI docs | FAkka.Argu command metadata + examples |

## Acceptance

- Toolchain choice recorded in SD or a linked decision note.
- Sample public API can generate XML docs.
- If HTTP endpoint is selected, sample endpoint can generate OpenAPI JSON.
- Examples use non-secret sample values.

## Blockers

- Final choice remains `SD-TBD-006`.
