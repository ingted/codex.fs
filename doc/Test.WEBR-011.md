# Test.WEBR-011 Reply stdio artifact panel

建立時間：2026-07-08 12:05 +08:00
關聯 WBS：`doc/WBS.WEBR-011.md`

| Test ID | WBS | Case | Type | Real path requirement | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| T-WEBR-011A | WEBR-011A | RFC/current-state traceability | Docs | RFC -> REQ/SA/SD/WBS/Test links exist | Pass | `doc/RFC/RFC-WEB-0005.reply-stdio-artifact-panel.md` |
| T-WEBR-011B | WEBR-011B | Artifact read handler contract | Unit/Integration | Real `CommHub.RegisterClientExtensionJsonPostHandler`; temp file under artifact root; traversal rejected | Pass | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore -p:GenerateDocumentationFile=false`; output included `TC-WEBR-004 useAIChat registration passed` and handler assertions for `STDOUT-CONTENT-WEBR011` plus traversal rejection |
| T-WEBR-011C | WEBR-011C | Floating stdio/final viewer | Browser/E2E | Real PTCS webshell, real Foreman runtime artifacts, Playwright opens panel and reads final content | Pass | `dotnet fsi --exec .\misc\verifyAiIntentOutputProjection.fsx -- --repo-root "G:/codex.fs/src/codex.fs" --configuration Debug --no-restore --host-address auto --host-port 0 --host-run-seconds 480 --screenshot-dir "G:/codex.fs/src/codex.fs/.playwright-mcp/webr011"` |
| T-WEBR-011D | WEBR-011D | Live 18488 handoff | Deployment/UI | Existing advertised host must be restarted from rebuilt runtime and verified through browser | Pass | Live `http://10.28.112.93:18488/page/codexfs-ai-chat`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr011\webr010-ai-intent-output-projection-live18488.png`; runtime root `G:\codex.fs\src\codex.fs\.codex.fs\runtime-hosts\18488-20260708121312` |

## Hard Gates

- `codexfs-artifact-summary` must show the actual last reply text.
- `codexfs-artifact-details` must keep refs collapsed by default.
- `codexfs-stdio-open` must open `codexfs-stdio-panel`.
- `codexfs-stdio-panel` must be movable/resizable and must not be the bottom output region.
- Final tab content must be read through the same-origin artifact read handler and contain the current run token/date.
- Traversal or absolute artifact path reads must return an error payload.
