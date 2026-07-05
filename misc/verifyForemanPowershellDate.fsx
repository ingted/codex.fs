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
--host-run-seconds 360
--browser-executable-path "{defaultBrowserPath}"
--screenshot-dir "G:/codex.fs/src/codex.fs/.playwright-mcp/e2e005"
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyForemanPowershellDate.fsx")

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

let argv = if externalArgv.Length > 0 then externalArgv else defaultArgv
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
    |> Option.defaultValue 360
    |> max 120

let requestedHostPort =
    results.TryGetResult VerifyArgument.Host_Port
    |> Option.defaultValue 0

let browserExecutablePath =
    results.TryGetResult VerifyArgument.Browser_Executable_Path
    |> Option.defaultValue defaultBrowserPath

let screenshotDir =
    results.TryGetResult VerifyArgument.Screenshot_Dir
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue (Path.Combine(repoRoot, ".playwright-mcp", "e2e005"))
    |> Path.GetFullPath

Directory.CreateDirectory screenshotDir |> ignore

let strictUtf8 = UTF8Encoding(false, true)
let solution = Path.Combine(repoRoot, "codex.fs.slnx")
let hostToolProject = Path.Combine(repoRoot, "src", "codex.fs.host.tool", "codex.fs.host.tool.fsproj")

let readText label path =
    let fullPath = Path.GetFullPath path

    if not (File.Exists fullPath) then
        failwith $"{label} not found: {fullPath}"

    File.ReadAllText(fullPath, strictUtf8)

let requireContains label (content: string) (needle: string) =
    if not (content.Contains(needle, StringComparison.Ordinal)) then
        failwith $"{label} missing required text: {needle}"

let requireNonBlank label (value: string) =
    if String.IsNullOrWhiteSpace value then
        failwith $"{label} is blank."

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
let runSuffix = Guid.NewGuid().ToString("N").Substring(0, 12)
let expectedDate = DateTimeOffset.Now.ToString("yyyy-MM-dd")
let promptText = $"hi 請用 powershell 執行 Get-Date -Format yyyy-MM-dd 取日期時間，最後只回覆取得的日期，應該是 {expectedDate}。"
let artifactRoot = Path.Combine(repoRoot, ".codex.fs", "e2e005-artifacts", "e2e005-" + runSuffix)
let pcslRoot = Path.Combine(repoRoot, ".codex.fs", "e2e005-pcsl", "pcsl-" + runSuffix)

let buildArgs =
    [ yield "build"
      yield solution
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess (TimeSpan.FromMinutes 6.0) "dotnet build solution" "dotnet" buildArgs

let startHostProcess () =
    let args =
        [ yield "run"
          yield "--project"
          yield hostToolProject
          yield "--configuration"
          yield configuration
          if results.Contains VerifyArgument.No_Restore then
              yield "--no-restore"
          yield "--"
          yield "start"
          yield "--run-seconds"
          yield string hostRunSeconds
          yield "--setting"
          yield "web.profile=ptcs-webshell"
          yield "--setting"
          yield $"web.bindAddress={hostAddress}"
          yield "--setting"
          yield $"web.port={hostPort}"
          yield "--setting"
          yield $"web.advertiseUri={hostUrl}"
          yield "--setting"
          yield "web.allowLoopbackOnly=false"
          yield "--setting"
          yield "web.actorFabric=auto-local"
          yield "--setting"
          yield $"web.pcslRoot={pcslRoot}"
          yield "--setting"
          yield $"artifact.root={artifactRoot}"
          yield "--setting"
          yield "engine.default=agy"
          yield "--setting"
          yield "timeout.default=00:03:00" ]

    let startInfo = ProcessStartInfo("dotnet")
    startInfo.WorkingDirectory <- repoRoot
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    startInfo.Environment["WebSharperBuildService"] <- "false"
    startInfo.Environment["WebSharperBuildServiceLogging"] <- "false"
    args |> List.iter (fun arg -> startInfo.ArgumentList.Add arg)

    let proc = new Process()
    proc.StartInfo <- startInfo

    if not (proc.Start()) then
        failwith "Failed to start codex.fs.host web shell."

    proc

let awaitHttpOk (client: HttpClient) (url: string) =
    let deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds 60.0
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

let awaitUnit (task: System.Threading.Tasks.Task) = task.GetAwaiter().GetResult()
let awaitValue (task: System.Threading.Tasks.Task<'T>) = task.GetAwaiter().GetResult()

let pathFromArtifactRow (page: IPage) testId =
    let selector = $"[data-testid='{testId}'] code"
    let value = page.TextContentAsync(selector) |> awaitValue
    let text = if isNull value then "" else value.Trim()
    requireNonBlank testId text
    text

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
        page.SetDefaultTimeout(240000.0f)
        page.GotoAsync(hostUrl.TrimEnd('/') + "/chat") |> awaitValue |> ignore
        page.WaitForSelectorAsync("[data-testid='chat-draft']") |> awaitValue |> ignore
        page.WaitForSelectorAsync("[data-testid='chat-participant'][data-participant-id='agent.codexfs.foreman']") |> awaitValue |> ignore
        page.ClickAsync("[data-testid='chat-participant'][data-participant-id='agent.codexfs.foreman']") |> awaitUnit
        page.FillAsync("[data-testid='chat-draft']", promptText) |> awaitUnit
        page.ClickAsync("[data-testid='chat-send']") |> awaitUnit
        page.WaitForSelectorAsync("[data-testid='codexfs-artifact-reply']") |> awaitValue |> ignore

        let artifactText = page.TextContentAsync("[data-testid='codexfs-artifact-reply']") |> awaitValue
        requireContains "browser artifact reply date" artifactText expectedDate

        let runId = pathFromArtifactRow page "codexfs-artifact-run"
        let outcome = pathFromArtifactRow page "codexfs-artifact-outcome"
        let manifestRelative = pathFromArtifactRow page "codexfs-artifact-manifest"
        let finalRelative = pathFromArtifactRow page "codexfs-artifact-final"
        let noteRelative = pathFromArtifactRow page "codexfs-artifact-note"

        requireContains "browser artifact outcome" outcome "completed"
        requireContains "browser artifact manifest" manifestRelative "manifest.json"
        requireContains "browser artifact final" finalRelative "final.md"
        requireContains "browser artifact note" noteRelative "note.md"

        let screenshotPath = Path.Combine(screenshotDir, "e2e005-foreman-powershell-date.png")
        page.ScreenshotAsync(PageScreenshotOptions(Path = screenshotPath, FullPage = true)) |> awaitUnit

        runId, manifestRelative, finalRelative, noteRelative, screenshotPath
    finally
        browser.CloseAsync() |> awaitUnit
        playwright.Dispose()

let assertRenderedArgvPolicy finalRelative =
    let runDirectory =
        Path.GetDirectoryName(Path.Combine(artifactRoot, finalRelative))

    requireNonBlank "runDirectory" runDirectory
    let renderedPath = Path.Combine(runDirectory, "rendered-argv.json")
    let renderedText = readText "rendered argv" renderedPath
    requireContains "rendered argv danger flag" renderedText "--dangerously-skip-permissions"
    requireContains "rendered argv print" renderedText "--print"

    use document = JsonDocument.Parse renderedText

    let arguments =
        document.RootElement.GetProperty("arguments").EnumerateArray()
        |> Seq.choose (fun item ->
            if item.ValueKind = JsonValueKind.String then
                Some(item.GetString())
            else
                None)
        |> Seq.toList

    let dangerIndex = arguments |> List.findIndex ((=) "--dangerously-skip-permissions")
    let printIndex = arguments |> List.findIndex ((=) "--print")

    if dangerIndex > printIndex then
        failwith $"Expected danger flag before --print in {renderedPath}."

    renderedPath

let httpClient = new HttpClient()
httpClient.Timeout <- TimeSpan.FromSeconds 10.0
let hostProcess = startHostProcess ()
let hostStdoutTask = hostProcess.StandardOutput.ReadToEndAsync()
let hostStderrTask = hostProcess.StandardError.ReadToEndAsync()

try
    awaitHttpOk httpClient (hostUrl.TrimEnd('/') + "/chat")
    let runId, manifestRelative, finalRelative, noteRelative, screenshotPath = runBrowserCheck ()
    let manifestPath = Path.Combine(artifactRoot, manifestRelative)
    let finalPath = Path.Combine(artifactRoot, finalRelative)
    let notePath = Path.Combine(artifactRoot, noteRelative)
    let renderedPath = assertRenderedArgvPolicy finalRelative

    let finalText = readText "final artifact" finalPath
    let noteText = readText "note artifact" notePath
    let manifestText = readText "manifest artifact" manifestPath

    requireContains "final artifact date" finalText expectedDate
    requireContains "note title" noteText "# codex.fs run note"
    requireContains "manifest rendered argv" manifestText "rendered-argv.json"

    printfn "TC-E2E-005 Foreman PowerShell date prompt passed"
    printfn "hostUrl=%s" hostUrl
    printfn "expectedDate=%s" expectedDate
    printfn "artifactRoot=%s" artifactRoot
    printfn "runId=%s" runId
    printfn "manifest=%s" manifestPath
    printfn "final=%s" finalPath
    printfn "note=%s" notePath
    printfn "renderedArgv=%s" renderedPath
    printfn "screenshot=%s" screenshotPath
    printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
finally
    if not hostProcess.HasExited then
        try
            hostProcess.Kill(true)
        with _ ->
            ()

    try
        hostProcess.WaitForExit(10000) |> ignore
    with _ ->
        ()

    let hostStdout =
        try
            hostStdoutTask.GetAwaiter().GetResult()
        with _ ->
            ""

    let hostStderr =
        try
            hostStderrTask.GetAwaiter().GetResult()
        with _ ->
            ""

    if not (String.IsNullOrWhiteSpace hostStdout) then
        printfn "hostStdoutBytes=%d" hostStdout.Length

    if not (String.IsNullOrWhiteSpace hostStderr) then
        printfn "hostStderrBytes=%d" hostStderr.Length

    httpClient.Dispose()
    hostProcess.Dispose()
