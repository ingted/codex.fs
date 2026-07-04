# codex.fs

`codex.fs` 是 F#/.NET 的輕量 CLI engine contract package，用來把 PTCS/agent host 的工作請求轉成版本化的 headless Codex/Agy CLI 執行邊界。

目前狀態：`0.1.0-alpha.1`，仍是早期 alpha。此 package 目前提供：

- core domain model；
- artifact manifest 與 file artifact store primitives；
- redaction helpers；
- guarded process runner；
- Codex CLI `0.142.x` probe、Argu render、artifact mapping；
- Agy CLI `1.0.x` probe、Argu render、artifact mapping；
- PTCS MessageFabric binding package；
- minimal host runtime / HTTP control endpoint / E2E single-cycle runner；
- `codex.fs.cli` dotnet tool command surface。

此 package 不包含模型 API provider，也不取代 PTCS ActorFabric/MessageFabric。`codex.fs.host` 目前是 referenceable host package；standalone host dotnet tool、durable sharded actor loop 與 Web UI 仍在後續工項。

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
dotnet tool install codex.fs.cli --tool-path .\.codex.fs\tool --add-source .\.codex.fs\packs --version 0.1.0-alpha.1
.\.codex.fs\tool\codex.fs.cli.exe --help
```

## License

MIT. See `LICENSE`.
