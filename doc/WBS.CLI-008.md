# WBS Detail: CLI-008 CLI Transport Failure Graceful Error

WBS ID：`CLI-008`
狀態：Done
Progress：100
Previous：`CLI-004`, `CLI-006`
Test：`T-CLI-008`
Test case：`TC-CLI-008 readable connection failure without stack trace`
SD：`SD §14`
RFC：`RFC-HOST-0001`
動工時間：2026-07-05 02:37 +08:00
更新時間：2026-07-05 02:48 +08:00

## Scope

修正 CLI HTTP helper：

- catch `HttpRequestException`。
- catch `TaskCanceledException`。
- catch invalid URI/operation failure before HTTP request completes。
- return `CliHttpResult` with `StatusCode = 0`, `IsSuccess = false`, and readable operator guidance。
- guidance explicitly says to use the advertised host URI and not the `codex.fs.host` process id as the HTTP port。

## Evidence

- `dotnet build .\codex.fs.slnx`: passed, 0 warnings, 0 errors。
- `dotnet run --project .\tests\codex.fs.Tests\codex.fs.Tests.fsproj --no-restore`: passed。
- `TC-CLI-008` reserves then closes a loopback port and verifies `getHostStatusAsync` returns a non-success result body containing `could not reach host endpoint` and process PID guidance without throwing。

## Blocker

None.
