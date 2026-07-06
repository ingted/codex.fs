# WBS.WEBR-010 AI intent output projection

建立時間：2026-07-06 08:36 +08:00  
關聯 RFC：`doc/RFC/RFC-WEB-0004.ai-intent-output-projection.md`

| ID | Work item | Prev | Progress | Status | Blocker | StartTime | UpdatedAt | SD | Test | Verifier |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- | --- |
| WEBR-010A | RFC/current-state 文件鏈同步 | WEBR-009 | 100 | Done | None | 2026-07-06 08:36 +08:00 | 2026-07-06 08:36 +08:00 | SD §14.2, §14.4 | T-WEBR-010A | file trace |
| WEBR-010B | AI append page output projection design | WEBR-010A | 100 | Done | None | 2026-07-06 08:38 +08:00 | 2026-07-06 09:24 +08:00 | SD §14.2.1 | T-WEBR-010B | `tests/codex.fs.Tests` source contract |
| WEBR-010C | Implement same-page MessageFabric reply projection | WEBR-010B | 100 | Done | None | 2026-07-06 08:42 +08:00 | 2026-07-06 09:24 +08:00 | SD §14.2.1 | T-WEBR-010C | `misc/verifyAiIntentOutputProjection.fsx` |
| WEBR-010D | Playwright visible-output regression | WEBR-010C | 100 | Done | None | 2026-07-06 09:03 +08:00 | 2026-07-06 09:24 +08:00 | SD §14.2.1, §15 | T-WEBR-010D | `misc/verifyAiIntentOutputProjection.fsx` |
| WEBR-010E | Handoff/deployment closeout on 18488 | WEBR-010D | 100 | Done | None | 2026-07-06 09:27 +08:00 | 2026-07-06 09:42 +08:00 | SD §14.2.1 | T-WEBR-010E | live 18488 URL + screenshot |

## Notes

- This is not a user option mistake. `Target=Foreman`, `Invocation=Exec`, `Approval=Never` must show output.
- Raw `codex.fs.web.ai-intent.v1` JSON in append history is not an acceptable result view.
- MVP projection may read the service participant thread `user.codexfs.web.ai-intent <-> agent.codexfs.foreman`, but UI must label that thread identity.
- Public/group projection remains follow-up unless this slice explicitly implements it.

## Acceptance Evidence

2026-07-06 09:21 +08:00 real-path verifier passed:

- Command: `dotnet fsi --exec .\misc\verifyAiIntentOutputProjection.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-run-seconds 480`
- Host URL: `http://10.28.112.93:2887`
- Page URL: `http://10.28.112.93:2887/page/codexfs-ai-chat`
- Prompt token: `CODEXFS_WEBR010_19a8c6204ffe`
- Expected date: `2026-07-06`
- Screenshot: `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection.png`
- Manifest: `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\manifest.json`
- Final: `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\final.md`
- Note: `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\note.md`
- Rendered argv: `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\rendered-argv.json`

The screenshot and DOM checks show the same append page output panel with state `Runtime reply received.`, thread `user.codexfs.web.ai-intent <-> agent.codexfs.foreman`, artifact refs, and final text containing the prompt token/date. The verifier rejects raw intent JSON as acceptance.

2026-07-06 09:42 +08:00 live 18488 handoff passed after restarting the host from copied runtime output:

- Host URL: `http://10.28.112.93:18488`
- Page URL: `http://10.28.112.93:18488/page/codexfs-ai-chat`
- Runtime process: `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260706094118\app\codex.fs.host.tool.exe`
- Prompt token: `CODEXFS_WEBR010_9eb8f77a5d6a`
- Screenshot: `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection-live18488.png`
- Manifest: `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260706094118\artifacts\sessions\foreman\runs\run-20260706014146458-4f9ea7a6\manifest.json`
- Final: `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260706094118\artifacts\sessions\foreman\runs\run-20260706014146458-4f9ea7a6\final.md`
- Rendered argv: `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260706094118\artifacts\sessions\foreman\runs\run-20260706014146458-4f9ea7a6\rendered-argv.json`

The live verifier includes a bounding-box gate that fails if the Send button overlaps the output panel.
