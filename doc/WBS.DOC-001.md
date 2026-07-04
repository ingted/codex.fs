# WBS Detail: DOC-001 API Documentation Toolchain Spike

WBS ID：`DOC-001`  
狀態：Planned  
Progress：0  
StartTime：未動工  
UpdatedAt：2026-07-04 18:10 +08:00  
Previous：`CF-001`  
SD：`SD §10`, `SD §17 SD-TBD-006`  
Test：`T-DOC-001`

## Scope

評估並定案 comment-as-SDK-doc 與 OpenAPI/Swagger 生成工具鏈。

## Candidate Tools

| Area | Candidate |
| --- | --- |
| OpenAPI/Swagger | MVP decision: ASP.NET Core HTTP host + Swashbuckle + XML comments |
| SDK reference | XML docs first; DocFX or FSharp.Formatting remains an evaluation |
| CLI docs | FAkka.Argu command metadata + examples |

## Acceptance

- Toolchain choice recorded in SD or a linked decision note.
- Sample public API can generate XML docs.
- Sample HTTP endpoint can generate OpenAPI JSON with Swashbuckle.
- Examples use non-secret sample values.

## Blockers

- None for MVP docs baseline. Static SDK reference generator evaluation remains open but does not block Swagger/OpenAPI baseline.
