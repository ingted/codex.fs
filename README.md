# codex.fs

`codex.fs` 是 F#/.NET 的輕量 CLI engine contract package，用來把 PTCS/agent host 的工作請求轉成版本化的 headless Codex/Agy CLI 執行邊界。

目前狀態：`0.1.0-alpha.1`，仍是早期 alpha。此 package 目前提供：

- core domain model；
- artifact manifest 與 file artifact store primitives；
- redaction helpers；
- guarded process runner；
- Codex CLI `0.142.x` probe、Argu render、artifact mapping；
- Agy CLI `1.0.x` probe、Argu render、artifact mapping。

此 package 不包含模型 API provider，也不取代 PTCS ActorFabric/MessageFabric。Production host integration、PTCS MessageFabric binding、HTTP control endpoint、dotnet tool 與 Web UI 仍在後續工項。

## Build

```powershell
dotnet restore .\codex.fs.slnx
dotnet build .\codex.fs.slnx --no-restore
```

## License

MIT. See `LICENSE`.
