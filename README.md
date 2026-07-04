# codex.fs

`codex.fs` 是 F#/.NET 的輕量 CLI engine contract package，用來把 PTCS/agent host 的工作請求轉成版本化的 headless Codex/Agy CLI 執行邊界。

目前狀態：`0.1.0-alpha.4`，仍是早期 alpha。此 package 目前提供：

- core domain model；
- artifact manifest 與 file artifact store primitives；
- redaction helpers；
- guarded process runner；
- Codex CLI `0.142.x` probe、Argu render、artifact mapping；
- Agy CLI `1.0.x` probe、Argu render、artifact mapping；
- PTCS MessageFabric binding package；
- minimal host runtime / HTTP control endpoint / E2E single-cycle runner；
- host operator landing page、OpenAPI JSON 與 Swagger UI；
- `codex.fs.cli` dotnet tool package，安裝後命令為 `codex.fs.cli`；
- `codex.fs.tool` short alias dotnet tool package，安裝後命令為 `codex.fs`；
- `codex.fs.host` command via the `codex.fs.host.tool` dotnet tool wrapper。

此 package 不包含模型 API provider，也不取代 PTCS ActorFabric/MessageFabric。`codex.fs.host` 保持 referenceable host library package；standalone dotnet tool 由薄 wrapper package `codex.fs.host.tool` 提供，tool command name 是 `codex.fs.host`。若要讓既有 PTCS Web UI 看到 worker/session participants，production host 應由 PTCS Host 或同 cluster 節點 reference `codex.fs.host` package 並傳入 caller-owned PTCS MessageFabric；standalone tool 的 package-owned fabric 只適合獨立驗證與早期操作。durable sharded actor loop 與 Web UI extension 仍在後續工項。

## Build

```powershell
dotnet restore .\codex.fs.slnx
dotnet build .\codex.fs.slnx --no-restore
```

## CLI tool local install

```powershell
dotnet pack .\src\codex.fs\codex.fs.fsproj --no-restore -o .\.codex.fs\packs
dotnet pack .\src\codex.fs.ptcs\codex.fs.ptcs.fsproj --no-restore -o .\.codex.fs\packs
dotnet pack .\src\codex.fs.host\codex.fs.host.fsproj --no-restore -o .\.codex.fs\packs
dotnet pack .\src\codex.fs.cli\codex.fs.cli.fsproj --no-restore -o .\.codex.fs\packs
dotnet pack .\src\codex.fs.tool\codex.fs.tool.fsproj --no-restore -o .\.codex.fs\packs
dotnet pack .\src\codex.fs.host.tool\codex.fs.host.tool.fsproj --no-restore -o .\.codex.fs\packs
dotnet tool install codex.fs.cli --tool-path .\.codex.fs\tool --add-source .\.codex.fs\packs --version 0.1.0-alpha.4
dotnet tool install codex.fs.tool --tool-path .\.codex.fs\tool --add-source .\.codex.fs\packs --version 0.1.0-alpha.4
dotnet tool install codex.fs.host.tool --tool-path .\.codex.fs\host-tool --add-source .\.codex.fs\packs --version 0.1.0-alpha.4
.\.codex.fs\tool\codex.fs.cli.exe --help
.\.codex.fs\tool\codex.fs.exe --help
.\.codex.fs\host-tool\codex.fs.host.exe --help
.\.codex.fs\host-tool\codex.fs.host.exe status --setting control.bindAddress=192.168.10.20 --setting control.port=8788 --setting control.advertiseUri=http://192.168.10.20:8788 --setting control.allowLoopbackOnly=false
```

## Host quick start

User-facing handoff should use installed tools, not a long-running `dotnet run` over dev build output.
If alpha.3 or earlier was installed, uninstall `codex.fs.cli` first because alpha.3 owned the `codex.fs` shim.

```powershell
dotnet tool uninstall --global codex.fs.cli
dotnet tool uninstall --global codex.fs.tool
dotnet tool uninstall --global codex.fs.host.tool
dotnet tool install --global codex.fs.cli --add-source G:\codex.fs\bin\cli-command-packs-20260705020253-alpha4 --version 0.1.0-alpha.4
dotnet tool install --global codex.fs.tool --add-source G:\codex.fs\bin\cli-command-packs-20260705020253-alpha4 --version 0.1.0-alpha.4
dotnet tool install --global codex.fs.host.tool --add-source G:\codex.fs\bin\cli-command-packs-20260705020253-alpha4 --version 0.1.0-alpha.4

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

Expected operator/API docs URLs:

- `http://10.28.112.93:10481/`
- `http://10.28.112.93:10481/api/codexfs/host/health`
- `http://10.28.112.93:10481/openapi/v1.json`
- `http://10.28.112.93:10481/docs/index.html`

Terminal command check:

```powershell
codex.fs --help
codex.fs.cli --help
codex.fs host status --host http://10.28.112.93:10481
codex.fs.cli host status --host http://10.28.112.93:10481
codex.fs.host --help
```

`session send` default target is the session worker / foreman participant derived as `<ptcs.sessionParticipantPrefix>.<sessionId>`. Use `--worker-id <participantId>` only when the prompt should go directly to a specific worker participant.

## PTCS Web profile note

Existing PTCS Host lives in `G:\PulseTrade.fs\Libs\PulseTrade.Comm\src\PulseTrade.Comm.Spa.Host` and consumes the PTCS package from `G:\PulseTrade2.fs\Libs\PulseTrade.Comm.Spa`.

- `http://127.0.0.1:82/chat` is the local PTCS.Login chat entry for the current host profile.
- `https://my-ai.co.in:81/chat` is the public GitHub OAuth profile and may redirect to GitHub login by design.
- codex.fs worker visibility in that chat UI requires the same PTCS hub/fabric, not a separate standalone package-owned host fabric.

## License

MIT. See `LICENSE`.
