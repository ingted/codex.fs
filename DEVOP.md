# DEVOP - codex.fs Host / Tool Deployment

版本：`0.1.0-alpha.6`
狀態：Active
最後更新：2026-07-05

## 1. Operator Contract

`codex.fs.host` 的使用者入口是 advertised root URL，不是隱藏 health endpoint。部署完成後，以下 URL 必須可用：

| URL | 用途 | 必須狀態 |
| --- | --- | --- |
| `http://<lan-ip>:<port>/` | operator landing page | HTTP 200 HTML |
| `http://<lan-ip>:<port>/chat` | PTCS chat guard page | HTTP 200 HTML |
| `http://<lan-ip>:<port>/diagnostics/session-send` | standalone diagnostics prompt form | HTTP 200 HTML |
| `http://<lan-ip>:<port>/api/codexfs/host/health` | machine-readable health | HTTP 200 JSON |
| `http://<lan-ip>:<port>/openapi/v1.json` | OpenAPI document | HTTP 200 JSON when docs enabled |
| `http://<lan-ip>:<port>/docs/index.html` | Swagger UI | HTTP 200 HTML when docs enabled |

Cluster/production-like profiles must use LAN/DNS-reachable bind/advertise settings. Do not hand off `localhost` / `127.0.0.1` as the peer-facing URL.

Browser chat is PTCS WebSharper chat room, not standalone `codex.fs.host`. The standalone host `/chat` route is a guard page; worker/user conversations must use PTCS participants over caller-owned `CommSpaMessageFabric` / `CommSpaActorFabric`.

## 2. Build And Pack

```powershell
dotnet restore .\codex.fs.slnx
dotnet build .\codex.fs.slnx --no-restore

$packOut = "G:\codex.fs\bin\ptcs-hub-align-packs-20260705030317-alpha6"
dotnet pack .\src\codex.fs\codex.fs.fsproj --no-restore -o $packOut
dotnet pack .\src\codex.fs.ptcs\codex.fs.ptcs.fsproj --no-restore -o $packOut
dotnet pack .\src\codex.fs.host\codex.fs.host.fsproj --no-restore -o $packOut
dotnet pack .\src\codex.fs.cli\codex.fs.cli.fsproj --no-restore -o $packOut
dotnet pack .\src\codex.fs.tool\codex.fs.tool.fsproj --no-restore -o $packOut
dotnet pack .\src\codex.fs.host.tool\codex.fs.host.tool.fsproj --no-restore -o $packOut
```

Do not keep a long-running `dotnet run` host alive while rebuilding the solution; it can lock `bin/Debug` DLLs. Long-running handoff should use installed global tools or a dedicated tool path.

## 3. Global Tool Install

```powershell
$packOut = "G:\codex.fs\bin\ptcs-hub-align-packs-20260705030317-alpha6"
dotnet tool install --global codex.fs.cli --add-source $packOut --version 0.1.0-alpha.6
dotnet tool install --global codex.fs.tool --add-source $packOut --version 0.1.0-alpha.6
dotnet tool install --global codex.fs.host.tool --add-source $packOut --version 0.1.0-alpha.6

C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe --help
C:\Users\Administrator\.dotnet\tools\codex.fs.exe --help
C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe --help
```

Expected files:

- `C:\Users\Administrator\.dotnet\tools\codex.fs.exe`
- `C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe`
- `C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe`

If a previous version is installed, uninstall only these package IDs before reinstalling:

```powershell
dotnet tool uninstall --global codex.fs.cli
dotnet tool uninstall --global codex.fs.tool
dotnet tool uninstall --global codex.fs.host.tool
```

## 4. Start Host

Example using LAN IP `10.28.112.93` and port `10481`:

```powershell
C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe start `
  --setting control.bindAddress=10.28.112.93 `
  --setting control.port=10481 `
  --setting control.advertiseUri=http://10.28.112.93:10481 `
  --setting control.allowLoopbackOnly=false `
  --setting ptcs.fabricMode=package-owned `
  --setting apiDocs.generateOpenApi=true `
  --setting apiDocs.exposeSwaggerUi=true `
  --setting apiDocs.swaggerRoutePrefix=docs
```

The command above starts the ASP.NET control host. Product browser chat must use the PTCS webshell profile:

```powershell
C:\Users\Administrator\.dotnet\tools\codex.fs.host.exe start `
  --setting web.profile=ptcs-webshell `
  --setting web.bindAddress=10.28.112.93 `
  --setting web.port=10482 `
  --setting web.advertiseUri=http://10.28.112.93:10482 `
  --setting web.allowLoopbackOnly=false `
  --setting web.actorFabric=disabled `
  --setting web.pcslRoot=G:\codex.fs\.codex.fs\ptcs-webshell-pcsl
```

`web.pcslRoot` must be a dedicated service data path. Do not rely on `AppContext.BaseDirectory\pcsl` under a dotnet tool output or `bin\Debug`; PTCS package `Server` can touch the default PCSL path during static initialization before codex.fs passes the explicit hub. If startup fails with `Unsupported fCell2 protobuf tag: 0`, inspect the default tool/build-output `pcsl` and the configured `web.pcslRoot`; archive or remove only verified build-output state after confirming the resolved absolute path.

The product PTCS webshell must serve these URLs:

| URL | 用途 | 必須狀態 |
| --- | --- | --- |
| `http://<lan-ip>:<web-port>/chat` | PTCS classic shell with tabs, participant/key list, page/session area and append controls | HTTP 200 HTML |
| `http://<lan-ip>:<web-port>/build/PulseTrade.Comm.Spa.js` | PTCS package core script copied from package `build/**` | HTTP 200 JavaScript |
| `http://<lan-ip>:<web-port>/client-extensions/codexfs-ai-chat/js/CodexFs.Web.js` | codex.fs WebSharper bundle | HTTP 200 JavaScript |
| `http://<lan-ip>:<web-port>/healthz` | PTCS webshell health | HTTP 200 JSON |

Current WEBR-006 host composition uses PTCS HTTP fallback APIs successfully for page create, key add and append. `/sync/ws` returned HTTP 503 in the browser console during verification; fix the WebSocket route before treating this profile as high-volume production chat.

## 5. Availability Verification

Minimum command checks:

```powershell
Invoke-WebRequest -Uri "http://10.28.112.93:10481/" -UseBasicParsing
Invoke-WebRequest -Uri "http://10.28.112.93:10481/chat" -UseBasicParsing
Invoke-WebRequest -Uri "http://10.28.112.93:10481/diagnostics/session-send" -UseBasicParsing
Invoke-WebRequest -Uri "http://10.28.112.93:10481/api/codexfs/host/health" -UseBasicParsing
Invoke-WebRequest -Uri "http://10.28.112.93:10481/openapi/v1.json" -UseBasicParsing
Invoke-WebRequest -Uri "http://10.28.112.93:10481/docs/index.html" -UseBasicParsing
C:\Users\Administrator\.dotnet\tools\codex.fs.exe host status --host http://10.28.112.93:10481
C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe host status --host http://10.28.112.93:10481
C:\Users\Administrator\.dotnet\tools\codex.fs.cli.exe session send --host http://10.28.112.93:10481 --prompt "hello foreman"
```

Browser gate:

- Open `http://10.28.112.93:10481/`.
- Verify the page shows `codex.fs host`, `PTCS WebSharper chat room`, `Diagnostics session send`, `Host health JSON`, `OpenAPI JSON`, and `Swagger UI`.
- Open `http://10.28.112.93:10481/chat`.
- Verify it says `Use PTCS chat` and does not present the product prompt composer.
- Open `http://10.28.112.93:10481/diagnostics/session-send`.
- Submit a diagnostics prompt with blank or `foreman` session and verify the response shows `Accepted` plus the expected `targetParticipantId`.
- Open `http://10.28.112.93:10481/docs/index.html`.
- Verify Swagger UI lists root/health/session/foreman/diagnostics endpoints.
- Start a separate `ptcs-webshell` profile on a LAN IP and verify `/chat` shows PTCS classic tabs plus a participant/key list. Create an `AI Chat` page, add key JSON literal `"agent.codexfs.foreman"`, send a prompt, and verify `/pages/api/append` stores a `codex.fs.web.ai-intent.v1` value.

Current evidence:

- `G:\codex.fs\.codex.fs\host-run\20260705030317-alpha6\stdout.log`
- `G:\codex.fs\.codex.fs\ptcs-hub-align-20260705030317-alpha6\summary.json`
- Current evidence records HTTP checks for root/chat guard/diagnostics/health/docs/OpenAPI, CLI no-session foreman send, CLI graceful connection failure, and real diagnostics form submission.

## 6. Documentation Outputs

Current API documentation:

- OpenAPI JSON: `/openapi/v1.json`
- Swagger UI: `/docs/index.html`
- SDK XML docs: generated by `GenerateDocumentationFile=true` and included in package outputs under `lib/net10.0/*.xml`.
- `/chat` is a legacy guard page pointing to PTCS Web chat.
- `/diagnostics/session-send` is a standalone diagnostics route only.
- `/api/codexfs/foreman/messages` documents the CLI first-use path where the caller does not know a session id.

Planned improvement:

- Add a generated F# reference site using FSharp.Formatting/fsdocs once public API shape stabilizes beyond the alpha host/tool usability phase.

## 7. Existing PTCS Web Verification

When validating PTCS chat behavior, first identify the deployment profile from the PTCS Host repo and docs:

- implementation repo: `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host`
- PTCS package repo: `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`
- local-login chat URL for the current profile: `http://127.0.0.1:82/chat`
- public GitHub OAuth chat URL for the current profile: `https://my-ai.co.in:81/chat`

Do not treat the 81 GitHub OAuth redirect as evidence that the chat UI is missing. For local functional checks, use the documented local PTCS.Login credential for that environment without writing the credential value into logs/docs.

Current real-browser evidence from 2026-07-05:

- `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82\summary.json`
- `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82-send\summary.json`
- `G:\codex.fs\.codex.fs\ptcs-web-inspect-20260705012257-local82-send\02-after-send.png`

Observed behavior: local 82 login reaches `/chat`, the public channel is selectable, and a public prompt is rendered in the thread. No codex.fs worker/session participants were visible in that PTCS host because the current standalone codex.fs host uses a separate package-owned in-process MessageFabric.

Production integration rule: for PTCS Web to show and talk to codex.fs workers, the PTCS Host or peer cluster node must reference `codex.fs.host` and start the runtime with caller-owned PTCS `CommSpaMessageFabric` / ActorFabric. A standalone tool host is acceptable for CLI/API/docs verification but does not make participants appear in an already running PTCS Web process.
