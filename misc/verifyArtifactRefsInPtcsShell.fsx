#r "nuget: FAkka.Argu, 10.1.301"
#r "nuget: Microsoft.Playwright, 1.52.0"
#load "ParseLine.fsx"

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Net.NetworkInformation
open System.Net.Sockets
open System.Text
open System.Text.Json
open System.Threading
open Argu
open Microsoft.Playwright

type VerifyArgument =
    | Repo_Root of path: string
    | Configuration of value: string
    | No_Restore
    | Host_Address of value: string
    | Host_Port of value: int
    | Host_Run_Seconds of value: int
    | Browser_Executable_Path of path: string
    | Screenshot_Dir of path: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repo_Root _ -> "Root path of the public codex.fs repo."
            | Configuration _ -> "Build configuration."
            | No_Restore -> "Pass --no-restore to dotnet build/run."
            | Host_Address _ -> "LAN/browser host address, or auto."
            | Host_Port _ -> "PTCS web shell port; 0 reserves a free port."
            | Host_Run_Seconds _ -> "Bounded host process lifetime."
            | Browser_Executable_Path _ -> "Chromium-compatible browser executable. Blank lets Playwright use its bundled browser."
            | Screenshot_Dir _ -> "Directory for Playwright evidence screenshots."

let knownBrowserPaths =
    [ @"C:\Program Files\Google\Chrome\Application\chrome.exe"
      @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
      @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
      @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" ]

let defaultBrowserPath =
    knownBrowserPaths
    |> List.tryFind File.Exists
    |> Option.defaultValue ""

let defaultArgumentsText =
    $"""
--repo-root "G:/codex.fs/src/codex.fs"
--configuration Debug
--host-address auto
--host-port 0
--host-run-seconds 90
--browser-executable-path "{defaultBrowserPath}"
--screenshot-dir "G:/codex.fs/src/codex.fs/.playwright-mcp/webr007"
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyArtifactRefsInPtcsShell.fsx")

let defaultArgv =
    defaultArgumentsText.Replace("\r", " ").Replace("\n", " ")
    |> PL.parseLine [| ' ' |] (Some '"') None true

let externalArgv =
    fsi.CommandLineArgs
    |> Array.skip 1
    |> fun args ->
        if args.Length > 0 && args[0] = "--" then
            args |> Array.skip 1
        else
            args

let argv =
    if externalArgv.Length > 0 then externalArgv else defaultArgv

let results = parser.ParseCommandLine argv

let repoRoot =
    results.TryGetResult VerifyArgument.Repo_Root
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue "."
    |> Path.GetFullPath

let configuration =
    results.TryGetResult VerifyArgument.Configuration
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue "Debug"

let hostRunSeconds =
    results.TryGetResult VerifyArgument.Host_Run_Seconds
    |> Option.defaultValue 90
    |> max 20

let requestedHostPort =
    results.TryGetResult VerifyArgument.Host_Port
    |> Option.defaultValue 0

let browserExecutablePath =
    results.TryGetResult VerifyArgument.Browser_Executable_Path
    |> Option.defaultValue defaultBrowserPath

let screenshotDir =
    results.TryGetResult VerifyArgument.Screenshot_Dir
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue (Path.Combine(repoRoot, ".playwright-mcp", "webr007"))
    |> Path.GetFullPath

Directory.CreateDirectory screenshotDir |> ignore

let strictUtf8 = UTF8Encoding(false, true)
let solution = Path.Combine(repoRoot, "codex.fs.slnx")
let testsProject = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "codex.fs.Tests.fsproj")
let hostToolProject = Path.Combine(repoRoot, "src", "codex.fs.host.tool", "codex.fs.host.tool.fsproj")
let webClient = Path.Combine(repoRoot, "src", "codex.fs.web", "Client", "AIChatClient.fs")
let hostWebShell = Path.Combine(repoRoot, "src", "codex.fs.host", "HostWebShell.fs")
let runtimeCycle = Path.Combine(repoRoot, "src", "codex.fs.ptcs", "RuntimeMessageFabricCycle.fs")
let generatedMainBundle = Path.Combine(repoRoot, "src", "codex.fs.web", "wwwroot", "js", "CodexFs.Web.js")

let readText label path =
    let fullPath = Path.GetFullPath path
    if not (File.Exists fullPath) then
        failwith $"{label} not found: {fullPath}"

    File.ReadAllText(fullPath, strictUtf8)

let requireContains label (content: string) (needle: string) =
    if not (content.Contains(needle, StringComparison.Ordinal)) then
        failwith $"{label} missing required text: {needle}"

let requireAll label content needles =
    needles |> List.iter (requireContains label content)

let runProcess (timeout: TimeSpan) (label: string) (fileName: string) (args: string list) =
    let startInfo = ProcessStartInfo(fileName)
    startInfo.WorkingDirectory <- repoRoot
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    startInfo.Environment["WebSharperBuildService"] <- "false"
    startInfo.Environment["WebSharperBuildServiceLogging"] <- "false"
    args |> List.iter (fun arg -> startInfo.ArgumentList.Add arg)

    use proc = new Process()
    proc.StartInfo <- startInfo

    if not (proc.Start()) then
        failwith $"Failed to start {label}."

    let stdoutTask = proc.StandardOutput.ReadToEndAsync()
    let stderrTask = proc.StandardError.ReadToEndAsync()
    let exited = proc.WaitForExit(int timeout.TotalMilliseconds)

    if not exited then
        try
            proc.Kill(true)
        with _ ->
            ()

        failwith $"{label} timed out after {timeout}."

    let stdout = stdoutTask.GetAwaiter().GetResult()
    let stderr = stderrTask.GetAwaiter().GetResult()

    if proc.ExitCode <> 0 then
        failwith $"{label} failed exit={proc.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}"

    stdout, stderr

let reserveTcpPort () =
    use listener = new TcpListener(IPAddress.Any, 0)
    listener.Start()
    let port = (listener.LocalEndpoint :?> IPEndPoint).Port
    listener.Stop()
    port

let nonLoopbackIpv4Addresses () =
    NetworkInterface.GetAllNetworkInterfaces()
    |> Array.filter (fun nic -> nic.OperationalStatus = OperationalStatus.Up)
    |> Array.collect (fun nic -> nic.GetIPProperties().UnicastAddresses |> Seq.toArray)
    |> Array.choose (fun address ->
        let ip = address.Address
        if ip.AddressFamily = AddressFamily.InterNetwork && not (IPAddress.IsLoopback ip) then
            Some(ip.ToString())
        else
            None)
    |> Array.distinct

let resolveHostAddress () =
    let requested =
        results.TryGetResult VerifyArgument.Host_Address
        |> Option.defaultValue "auto"

    if requested.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase) then
        nonLoopbackIpv4Addresses () |> Array.tryHead |> Option.defaultValue "127.0.0.1"
    else
        requested.Trim()

let hostAddress = resolveHostAddress ()
let hostPort = if requestedHostPort <= 0 then reserveTcpPort () else requestedHostPort
let hostUrl = $"http://{hostAddress}:{hostPort}"
let pcslRoot = Path.Combine(repoRoot, ".codex.fs", "webr007-pcsl", Guid.NewGuid().ToString("N"))

let valueAfterPrefix prefix (text: string) =
    text.Replace("\r\n", "\n").Split('\n')
    |> Array.tryFind (fun line -> line.StartsWith(prefix, StringComparison.Ordinal))
    |> Option.map (fun line -> line.Substring(prefix.Length).Trim())
    |> Option.defaultWith (fun () -> failwith $"Missing output line prefix: {prefix}")

let relativeToRoot root path =
    Path.GetRelativePath(root, path).Replace("\\", "/")

let runIdFromManifestRelativePath (manifestRelativePath: string) =
    let parts = manifestRelativePath.Split([| '/'; '\\' |], StringSplitOptions.RemoveEmptyEntries)
    let runIndex = parts |> Array.tryFindIndex ((=) "runs")

    match runIndex with
    | Some index when index + 1 < parts.Length -> parts[index + 1]
    | _ -> failwith $"Could not derive run id from manifest path: {manifestRelativePath}"

let sourceClient = readText "AIChatClient.fs" webClient
let sourceHostWebShell = readText "HostWebShell.fs" hostWebShell
let sourceRuntimeCycle = readText "RuntimeMessageFabricCycle.fs" runtimeCycle

requireAll
    "AIChatClient.fs"
    sourceClient
    [ "type ArtifactReplyDto"
      "parseArtifactReply"
      "codexfs-artifact-reply"
      "codexfs-artifact-manifest"
      "codexfs-artifact-final"
      "codexfs-artifact-note"
      "PulseTradeRegisterRenderer"
      "enhanceExistingMessageBodies"
      "pre.message-body:not([data-codexfs-artifact-scanned])" ]

requireAll
    "HostWebShell.fs"
    sourceHostWebShell
    [ "registerDefaultForeman"
      "agent.codexfs.foreman"
      "hub.useAIChat()" ]

requireAll
    "RuntimeMessageFabricCycle.fs"
    sourceRuntimeCycle
    [ "RunNoteMarkdown"
      "\"note.md\""
      "RunNotePath"
      "RuntimePromptLoop.replyIntent runId outcome manifestRelativePath finalPath noteArtifact.Reference.Path" ]

let buildArgs =
    [ yield "build"
      yield solution
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess (TimeSpan.FromMinutes 8.0) "dotnet build solution" "dotnet" buildArgs

let generatedBundleText = readText "CodexFs.Web.js" generatedMainBundle
requireAll
    "CodexFs.Web.js"
    generatedBundleText
    [ "codexfs-artifact-reply"
      "codexfs-artifact-manifest"
      "codexfs-artifact-final"
      "codexfs-artifact-note"
      "PulseTradeRegisterRenderer"
      "pre.message-body:not([data-codexfs-artifact-scanned])" ]

let testArgs =
    [ yield "run"
      yield "--project"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let testStdout, testStderr = runProcess (TimeSpan.FromMinutes 12.0) "dotnet run tests" "dotnet" testArgs
requireContains "dotnet run tests stdout" testStdout "TC-ACTOR-003 actor runtime artifact provider passed"
requireContains "dotnet run tests stdout" testStdout "TC-WEBR-006 AI intent controls passed"

let artifactRoot = valueAfterPrefix "actor003ArtifactRoot=" testStdout
let manifestPath = valueAfterPrefix "actor003Manifest=" testStdout
let finalPath = valueAfterPrefix "actor003Final=" testStdout
let notePath = valueAfterPrefix "actor003Note=" testStdout

for label, path in [ "manifest", manifestPath; "final", finalPath; "note", notePath ] do
    if not (File.Exists path) then
        failwith $"{label} artifact not found: {path}"

let manifestRelative = relativeToRoot artifactRoot manifestPath
let finalRelative = relativeToRoot artifactRoot finalPath
let noteRelative = relativeToRoot artifactRoot notePath
let runId = runIdFromManifestRelativePath manifestRelative
let artifactReplyBody =
    $"run {runId} completed; manifest={manifestRelative}; final={finalRelative}; note={noteRelative}; summary=WEBR-007 rendered real actor artifact refs"

let startHostProcess () =
    let args =
        [ "run"
          "--project"
          hostToolProject
          "--configuration"
          configuration
          if results.Contains VerifyArgument.No_Restore then
              "--no-restore"
          "--"
          "start"
          "--run-seconds"
          string hostRunSeconds
          "--setting"
          "web.profile=ptcs-webshell"
          "--setting"
          $"web.bindAddress={hostAddress}"
          "--setting"
          $"web.port={hostPort}"
          "--setting"
          $"web.advertiseUri={hostUrl}"
          "--setting"
          "web.allowLoopbackOnly=false"
          "--setting"
          "web.actorFabric=disabled"
          "--setting"
          $"web.pcslRoot={pcslRoot}" ]

    let startInfo = ProcessStartInfo("dotnet")
    startInfo.WorkingDirectory <- repoRoot
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    startInfo.Environment["WebSharperBuildService"] <- "false"
    startInfo.Environment["WebSharperBuildServiceLogging"] <- "false"
    args |> List.iter startInfo.ArgumentList.Add

    let proc = new Process()
    proc.StartInfo <- startInfo

    if not (proc.Start()) then
        failwith "Failed to start codex.fs.host web shell."

    proc

let awaitHttpOk (client: HttpClient) (url: string) =
    let deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds 30.0
    let mutable success = false
    let mutable lastError = ""

    while not success && DateTimeOffset.UtcNow < deadline do
        try
            use response = client.GetAsync(url).GetAwaiter().GetResult()
            success <- response.StatusCode = HttpStatusCode.OK
            if not success then
                lastError <- $"status={(int response.StatusCode)}"
        with ex ->
            lastError <- ex.Message

        if not success then
            Thread.Sleep 500

    if not success then
        failwith $"Timed out waiting for {url}; last={lastError}"

let postArtifactReply (client: HttpClient) =
    let payload =
        JsonSerializer.Serialize(
            {| fromId = "agent.codexfs.foreman"
               toId = "user.web"
               body = artifactReplyBody
               tags = [| "codex.fs"; "webr007"; "artifact-ref" |] |})

    use content = new StringContent(payload, Encoding.UTF8, "application/json")
    use response = client.PostAsync(hostUrl.TrimEnd('/') + "/chat/api/send", content).GetAwaiter().GetResult()
    let body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    if response.StatusCode <> HttpStatusCode.OK then
        failwith $"POST /chat/api/send failed status={(int response.StatusCode)} body={body}"

    requireContains "chat send reply" body "msg-http-"
    body

let awaitUnit (task: System.Threading.Tasks.Task) =
    task.GetAwaiter().GetResult()

let awaitValue (task: System.Threading.Tasks.Task<'T>) =
    task.GetAwaiter().GetResult()

let runBrowserCheck () =
    let playwright = Playwright.CreateAsync() |> awaitValue
    let launchOptions = BrowserTypeLaunchOptions(Headless = true)

    if not (String.IsNullOrWhiteSpace browserExecutablePath) then
        if not (File.Exists browserExecutablePath) then
            failwith $"Browser executable does not exist: {browserExecutablePath}"

        launchOptions.ExecutablePath <- browserExecutablePath

    let browser = playwright.Chromium.LaunchAsync(launchOptions) |> awaitValue

    try
        let context =
            browser.NewContextAsync(BrowserNewContextOptions(ViewportSize = ViewportSize(Width = 1280, Height = 760)))
            |> awaitValue

        let page = context.NewPageAsync() |> awaitValue
        page.SetDefaultTimeout(30000.0f)
        page.GotoAsync(hostUrl.TrimEnd('/') + "/chat") |> awaitValue |> ignore
        page.WaitForSelectorAsync("[data-testid='chat-draft']") |> awaitValue |> ignore
        page.WaitForSelectorAsync("[data-testid='chat-participant'][data-participant-id='agent.codexfs.foreman']") |> awaitValue |> ignore
        page.ClickAsync("[data-testid='chat-participant'][data-participant-id='agent.codexfs.foreman']") |> awaitUnit
        page.WaitForSelectorAsync("[data-testid='codexfs-artifact-reply']") |> awaitValue |> ignore

        let artifactText = page.TextContentAsync("[data-testid='codexfs-artifact-reply']") |> awaitValue
        let manifestText = page.TextContentAsync("[data-testid='codexfs-artifact-manifest']") |> awaitValue
        let noteText = page.TextContentAsync("[data-testid='codexfs-artifact-note']") |> awaitValue
        let summaryText = page.TextContentAsync("[data-testid='codexfs-artifact-summary']") |> awaitValue

        requireContains "browser artifact reply" artifactText runId
        requireContains "browser artifact manifest" manifestText manifestRelative
        requireContains "browser artifact note" noteText noteRelative
        requireContains "browser artifact summary" summaryText "WEBR-007 rendered real actor artifact refs"

        let screenshotPath = Path.Combine(screenshotDir, "webr007-artifact-refs.png")
        page.ScreenshotAsync(PageScreenshotOptions(Path = screenshotPath, FullPage = true)) |> awaitUnit
        screenshotPath
    finally
        browser.CloseAsync() |> awaitUnit
        playwright.Dispose()

let httpClient = new HttpClient()
httpClient.Timeout <- TimeSpan.FromSeconds 10.0
let hostProcess = startHostProcess ()

let mutable hostStdout = ""
let mutable hostStderr = ""

try
    awaitHttpOk httpClient (hostUrl.TrimEnd('/') + "/chat")
    let sendReply = postArtifactReply httpClient
    let screenshotPath = runBrowserCheck ()

    printfn "TC-WEBR-007 artifact refs in PTCS shell passed"
    printfn "hostUrl=%s" hostUrl
    printfn "artifactRoot=%s" artifactRoot
    printfn "manifest=%s" manifestPath
    printfn "final=%s" finalPath
    printfn "note=%s" notePath
    printfn "runId=%s" runId
    printfn "screenshot=%s" screenshotPath
    printfn "chatSendReplyBytes=%d" sendReply.Length
    printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
    printfn "testStdoutBytes=%d testStderrBytes=%d" testStdout.Length testStderr.Length
finally
    if not hostProcess.HasExited then
        try
            hostProcess.Kill(true)
        with _ ->
            ()

    try
        hostStdout <- hostProcess.StandardOutput.ReadToEnd()
        hostStderr <- hostProcess.StandardError.ReadToEnd()
    with _ ->
        ()

    hostProcess.Dispose()
    httpClient.Dispose()
