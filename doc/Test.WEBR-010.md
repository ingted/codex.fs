# Test.WEBR-010 AI intent output projection

建立時間：2026-07-06 08:36 +08:00  
關聯 WBS：`doc/WBS.WEBR-010.md`

| Test ID | WBS | Case | Type | Real path requirement | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| T-WEBR-010A | WEBR-010A | RFC/current-state traceability | Docs | RFC -> REQ/SA/SD/WBS/Test links exist | Pass | `doc/RFC/RFC-WEB-0004.ai-intent-output-projection.md` |
| T-WEBR-010B | WEBR-010B | Output projection selectors and source contract | Unit/Source | Generated WebSharper client must expose stable selectors and poll MessageFabric thread, not local fake store | Pass | `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`; `TC-WEBR-010 AI intent output projection source contract passed` |
| T-WEBR-010C | WEBR-010C | Same-page reply projection | Browser/E2E | Real PTCS webshell, real MessageFabric, real Foreman actor runtime, installed engine | Pass | `dotnet fsi --exec .\misc\verifyAiIntentOutputProjection.fsx`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection.png` |
| T-WEBR-010D | WEBR-010D | Raw JSON alone fails acceptance | Browser/Regression | Playwright must assert visible output is final/reply/artifact, not only intent JSON | Pass | Verifier asserted `codexfs-ai-output-message` does not contain raw `codex.fs.web.ai-intent.v1`; final `G:\codex.fs\src\codex.fs\.codex.fs\webr010-artifacts\webr010-19a8c6204ffe\sessions\foreman\runs\run-20260706012130613-7c68a5ce\final.md` |
| T-WEBR-010E | WEBR-010E | Live 18488 handoff | Deployment/UI | Existing handoff URL must show output after Send | Pass | `dotnet fsi --exec .\misc\verifyAiIntentOutputProjection.fsx -- --existing-host-url "http://10.28.112.93:18488" --existing-artifact-root "G:/codex.fs/src/codex.fs/.codex.fs/runtime-hosts/18488-20260706094118/artifacts"`; screenshot `G:\codex.fs\src\codex.fs\.playwright-mcp\webr010\webr010-ai-intent-output-projection-live18488.png` |

## Playwright Verifier Contract

`misc/verifyAiIntentOutputProjection.fsx` must:

- use FAkka.Argu and project `ParseLine.fsx` style default arguments；
- build `codex.fs.slnx` or fail fast；
- start a real `ptcs-webshell` on LAN IP with `web.actorFabric=auto-local`；
- use Playwright to open `/page/codexfs-ai-chat`；
- fill `codexfs-ai-prompt` with a unique token and `Get-Date` request；
- click `codexfs-ai-send`；
- wait for `codexfs-ai-output`；
- assert visible output contains the token/date or artifact refs；
- assert output region includes thread identity；
- assert Send button and output panel do not overlap by bounding boxes；
- save screenshot under `.playwright-mcp/webr010/` and print absolute evidence paths.

## Hard Gates

- No fake/mock mailbox.
- No direct artifact injection.
- No standalone `/diagnostics/session-send` acceptance.
- Raw intent JSON display does not count as output.
- If engine execution fails, visible failure with run refs is acceptable; silent append-only JSON is not.
