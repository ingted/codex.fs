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
    | Existing_Host_Url of value: string
    | Existing_Artifact_Root of path: string
    | Browser_Executable_Path of path: string
    | Screenshot_Dir of path: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repo_Root _ -> "Root path of the public codex.fs repo."
            | Configuration _ -> "Build configuration."
            | No_Restore -> "Pass --no-restore to dotnet build and host run."
            | Host_Address _ -> "LAN/browser host address, or auto."
            | Host_Port _ -> "PTCS web shell port; 0 reserves a free port."
            | Host_Run_Seconds _ -> "Bounded host process lifetime."
            | Existing_Host_Url _ -> "Use an already-running PTCS web shell URL instead of starting a temporary host."
            | Existing_Artifact_Root _ -> "Artifact root used by the existing host; required for full artifact file verification when --existing-host-url is used."
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
--screenshot-dir "G:/codex.fs/src/codex.fs/.playwright-mcp/webr010"
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyAiIntentOutputProjection.fsx")

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

let existingHostUrl =
    results.TryGetResult VerifyArgument.Existing_Host_Url
    |> Option.map (fun value -> value.Trim().TrimEnd('/'))
    |> Option.filter (String.IsNullOrWhiteSpace >> not)

let useExistingHost = existingHostUrl.IsSome

let existingArtifactRoot =
    results.TryGetResult VerifyArgument.Existing_Artifact_Root
    |> Option.map Path.GetFullPath
    |> Option.filter (String.IsNullOrWhiteSpace >> not)

let browserExecutablePath =
    results.TryGetResult VerifyArgument.Browser_Executable_Path
    |> Option.defaultValue defaultBrowserPath

let screenshotDir =
    results.TryGetResult VerifyArgument.Screenshot_Dir
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue (Path.Combine(repoRoot, ".playwright-mcp", "webr010"))
    |> Path.GetFullPath

Directory.CreateDirectory screenshotDir |> ignore

let strictUtf8 = UTF8Encoding(false, true)
let solution = Path.Combine(repoRoot, "codex.fs.slnx")
let hostToolProject = Path.Combine(repoRoot, "src", "codex.fs.host.tool", "codex.fs.host.tool.fsproj")
let aiChatClient = Path.Combine(repoRoot, "src", "codex.fs.web", "Client", "AIChatClient.fs")
let generatedMainJs = Path.Combine(repoRoot, "src", "codex.fs.web", "wwwroot", "js", "CodexFs.Web.js")
let wbsDetail = Path.Combine(repoRoot, "doc", "WBS.WEBR-010.md")
let testDetail = Path.Combine(repoRoot, "doc", "Test.WEBR-010.md")

let readText label path =
    let fullPath = Path.GetFullPath path

    if not (File.Exists fullPath) then
        failwith $"{label} not found: {fullPath}"

    File.ReadAllText(fullPath, strictUtf8)

let requireContains label (content: string) (needle: string) =
    if not (content.Contains(needle, StringComparison.Ordinal)) then
        failwith $"{label} missing required text: {needle}"

let requireNotContains label (content: string) (needle: string) =
    if content.Contains(needle, StringComparison.Ordinal) then
        failwith $"{label} contains forbidden text: {needle}"

let requireAll label content needles =
    needles |> List.iter (requireContains label content)

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
    args |> List.iter startInfo.ArgumentList.Add

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
let hostUrl = existingHostUrl |> Option.defaultValue $"http://{hostAddress}:{hostPort}"
let runSuffix = Guid.NewGuid().ToString("N").Substring(0, 12)
let promptToken = "CODEXFS_WEBR010_" + runSuffix
let expectedDate = DateTimeOffset.Now.ToString("yyyy-MM-dd")
let promptText =
    $"hi 請用 powershell 執行 Get-Date -Format yyyy-MM-dd，最後回覆 token {promptToken} 與日期 {expectedDate}。"

let artifactRoot =
    existingArtifactRoot
    |> Option.defaultValue (Path.Combine(repoRoot, ".codex.fs", "webr010-artifacts", "webr010-" + runSuffix))

let pcslRoot = Path.Combine(repoRoot, ".codex.fs", "webr010-pcsl", "pcsl-" + runSuffix)

let clientSource = readText "AIChatClient.fs" aiChatClient
let wbsText = readText "WBS.WEBR-010.md" wbsDetail
let testText = readText "Test.WEBR-010.md" testDetail

requireAll
    "AIChatClient.fs"
    clientSource
    [ "aiIntentBridgeParticipantId"
      "codexfs-ai-output"
      "codexfs-ai-output-state"
      "codexfs-ai-output-thread"
      "codexfs-ai-output-message"
      "/chat/api/thread?participantId="
      "Runtime reply received."
      "Timed out waiting for runtime reply."
      "codexfs-artifact-reply" ]

requireAll
    "WBS/Test WEBR-010"
    (wbsText + "\n" + testText)
    [ "WEBR-010"
      "T-WEBR-010"
      "misc/verifyAiIntentOutputProjection.fsx"
      "Raw intent JSON display does not count as output." ]

let buildArgs =
    [ yield "build"
      yield solution
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess (TimeSpan.FromMinutes 6.0) "dotnet build solution" "dotnet" buildArgs
let generatedText = readText "CodexFs.Web.js" generatedMainJs

requireAll
    "generated CodexFs.Web.js"
    generatedText
    [ "codexfs-ai-output"
      "codexfs-ai-output-message"
      "/chat/api/thread?participantId="
      "user.codexfs.web.ai-intent" ]

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
          yield "engine.default=codex"
          yield "--setting"
          yield "timeout.default=00:04:00" ]

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

let awaitUnit (task: System.Threading.Tasks.Task) =
    task.GetAwaiter().GetResult()

let awaitValue (task: System.Threading.Tasks.Task<'T>) =
    task.GetAwaiter().GetResult()

let lastLocator (page: IPage) selector =
    page.Locator(selector).Last

let lastInside (locator: ILocator) (selector: string) =
    locator.Locator(selector).Last

let setSelectValue (page: IPage) (testId: string) (value: string) =
    let locator = lastLocator page $"[data-testid='{testId}']"
    locator.SelectOptionAsync(value)
    |> awaitValue
    |> ignore

let textContentOrEmpty (page: IPage) selector =
    try
        let locator = lastLocator page selector
        let value = locator.TextContentAsync() |> awaitValue
        if isNull value then "" else value
    with _ ->
        ""

let requireVerticalGap label (upper: ILocator) (lower: ILocator) minGap =
    let elementDebug (locator: ILocator) =
        try
            let script =
                """el => {
  const r = el.getBoundingClientRect();
  const cs = getComputedStyle(el);
  const parent = el.parentElement;
  const pr = parent ? parent.getBoundingClientRect() : null;
  const pcs = parent ? getComputedStyle(parent) : null;
  return JSON.stringify({
    tag: el.tagName,
    cls: el.className,
    text: (el.textContent || '').slice(0, 160),
    rect: { x: r.x, y: r.y, width: r.width, height: r.height, bottom: r.bottom },
    style: { display: cs.display, position: cs.position, margin: cs.margin, transform: cs.transform, gridColumn: cs.gridColumn, gridRow: cs.gridRow, alignSelf: cs.alignSelf, top: cs.top, bottom: cs.bottom, height: cs.height, minHeight: cs.minHeight, overflow: cs.overflow, padding: cs.padding },
    parent: parent ? { tag: parent.tagName, cls: parent.className, rect: { x: pr.x, y: pr.y, width: pr.width, height: pr.height, bottom: pr.bottom }, style: { display: pcs.display, position: pcs.position, margin: pcs.margin, transform: pcs.transform, gridColumn: pcs.gridColumn, gridRow: pcs.gridRow, alignSelf: pcs.alignSelf, top: pcs.top, bottom: pcs.bottom, height: pcs.height, minHeight: pcs.minHeight, overflow: pcs.overflow, padding: pcs.padding } } : null
  });
}"""

            locator.EvaluateAsync<string>(script) |> awaitValue
        with ex ->
            ex.Message

    let upperBox = upper.BoundingBoxAsync() |> awaitValue
    let lowerBox = lower.BoundingBoxAsync() |> awaitValue

    if isNull (box upperBox) || isNull (box lowerBox) then
        failwith $"{label} could not read bounding boxes."

    let actualGap = float (lowerBox.Y - (upperBox.Y + upperBox.Height))

    if actualGap < minGap then
        failwith $"{label} overlap or insufficient gap. gap={actualGap}; required={minGap}\nupper={elementDebug upper}\nlower={elementDebug lower}"

let waitForOutputText (page: IPage) =
    let deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds 240.0
    let mutable outputText = ""
    let mutable stateText = ""

    while String.IsNullOrWhiteSpace outputText && DateTimeOffset.UtcNow < deadline do
        stateText <- textContentOrEmpty page "[data-testid='codexfs-ai-output-state']"
        outputText <- textContentOrEmpty page "[data-testid='codexfs-ai-output-message']"

        if String.IsNullOrWhiteSpace outputText then
            Thread.Sleep 2000

    if String.IsNullOrWhiteSpace outputText then
        failwith $"Timed out waiting for visible output projection; state={stateText}"

    outputText, stateText

let pathFromArtifactRow (page: IPage) testId =
    let value = textContentOrEmpty page $"[data-testid='{testId}'] code"
    let text = if isNull value then "" else value.Trim()
    requireNonBlank testId text
    text

let httpGetText (url: string) =
    use client = new HttpClient()
    client.Timeout <- TimeSpan.FromSeconds 10.0
    client.GetStringAsync(url).GetAwaiter().GetResult()

let waitHttpContains label (url: string) (needle: string) timeout =
    let deadline = DateTimeOffset.UtcNow + timeout
    let mutable latest = ""
    let mutable found = false

    while not found && DateTimeOffset.UtcNow < deadline do
        try
            latest <- httpGetText url
            found <- latest.Contains(needle, StringComparison.Ordinal)
        with ex ->
            latest <- ex.Message

        if not found then
            Thread.Sleep 1000

    if not found then
        let snippet =
            if isNull latest then
                ""
            elif latest.Length > 1200 then
                latest.Substring(0, 1200)
            else
                latest

        failwith $"{label} did not contain expected text within {timeout}. url={url} snippet={snippet}"

    latest

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
            browser.NewContextAsync(BrowserNewContextOptions(ViewportSize = ViewportSize(Width = 1360, Height = 820)))
            |> awaitValue

        let page = context.NewPageAsync() |> awaitValue
        page.SetDefaultTimeout(120000.0f)
        page.GotoAsync(hostUrl.TrimEnd('/') + "/page/codexfs-ai-chat") |> awaitValue |> ignore
        let controlsLocator = lastLocator page "[data-testid='codexfs-ai-controls']"
        controlsLocator.WaitForAsync(LocatorWaitForOptions(State = Nullable WaitForSelectorState.Visible)) |> awaitUnit
        let outputLocator = lastInside controlsLocator "[data-testid='codexfs-ai-output']"
        outputLocator.WaitForAsync(LocatorWaitForOptions(State = Nullable WaitForSelectorState.Visible)) |> awaitUnit

        setSelectValue page "codexfs-ai-target-mode" "foreman"
        setSelectValue page "codexfs-ai-engine" "codex"
        setSelectValue page "codexfs-ai-invocation-mode" "exec"
        setSelectValue page "codexfs-ai-approval" "never"
        let promptLocator = lastLocator page "[data-testid='codexfs-ai-prompt']"
        promptLocator.FillAsync(promptText) |> awaitUnit
        let sendLocator = lastInside controlsLocator "[data-testid='codexfs-ai-send']"
        sendLocator.ClickAsync() |> awaitUnit

        let appendStateUrl = hostUrl.TrimEnd('/') + "/pages/api/state?pageId=codexfs-ai-chat&limit=20"
        waitHttpContains "append page state" appendStateUrl promptToken (TimeSpan.FromSeconds 20.0) |> ignore
        let bridgeThreadUrl =
            hostUrl.TrimEnd('/')
            + "/chat/api/thread?participantId="
            + Uri.EscapeDataString("user.codexfs.web.ai-intent")
            + "&peerId="
            + Uri.EscapeDataString("agent.codexfs.foreman")

        waitHttpContains "AI intent bridge thread" bridgeThreadUrl promptToken (TimeSpan.FromSeconds 30.0) |> ignore

        let outputText, stateText = waitForOutputText page
        requireContains "visible output token" outputText promptToken
        requireContains "visible output date" outputText expectedDate
        requireContains "visible output state" stateText "Runtime reply received."
        requireNotContains "visible output" outputText "codex.fs.web.ai-intent.v1"

        let artifactLocator = lastLocator page "[data-testid='codexfs-artifact-reply']"
        artifactLocator.WaitForAsync(LocatorWaitForOptions(State = Nullable WaitForSelectorState.Visible)) |> awaitUnit
        requireVerticalGap "Send button to output panel" sendLocator outputLocator 6.0
        let artifactText = textContentOrEmpty page "[data-testid='codexfs-artifact-reply']"
        requireContains "artifact reply token" artifactText promptToken
        requireContains "artifact reply date" artifactText expectedDate

        let threadText = textContentOrEmpty page "[data-testid='codexfs-ai-output-thread']"
        requireContains "output thread bridge participant" threadText "user.codexfs.web.ai-intent"
        requireContains "output thread foreman participant" threadText "agent.codexfs.foreman"

        let outcome = pathFromArtifactRow page "codexfs-artifact-outcome"
        let manifestRelative = pathFromArtifactRow page "codexfs-artifact-manifest"
        let finalRelative = pathFromArtifactRow page "codexfs-artifact-final"
        let noteRelative = pathFromArtifactRow page "codexfs-artifact-note"
        requireContains "artifact outcome" outcome "completed"
        requireContains "artifact manifest" manifestRelative "manifest.json"
        requireContains "artifact final" finalRelative "final.md"
        requireContains "artifact note" noteRelative "note.md"

        let screenshotFileName =
            if useExistingHost then
                "webr010-ai-intent-output-projection-live18488.png"
            else
                "webr010-ai-intent-output-projection.png"

        let screenshotPath = Path.Combine(screenshotDir, screenshotFileName)
        page.ScreenshotAsync(PageScreenshotOptions(Path = screenshotPath, FullPage = true)) |> awaitUnit
        outputText, manifestRelative, finalRelative, noteRelative, screenshotPath
    finally
        browser.CloseAsync() |> awaitUnit
        playwright.Dispose()

let httpClient = new HttpClient()
httpClient.Timeout <- TimeSpan.FromSeconds 10.0
let hostProcess =
    if useExistingHost then
        None
    else
        Some(startHostProcess ())

let hostStdoutTask = hostProcess |> Option.map (fun proc -> proc.StandardOutput.ReadToEndAsync())
let hostStderrTask = hostProcess |> Option.map (fun proc -> proc.StandardError.ReadToEndAsync())

try
    awaitHttpOk httpClient (hostUrl.TrimEnd('/') + "/page/codexfs-ai-chat")
    let outputText, manifestRelative, finalRelative, noteRelative, screenshotPath = runBrowserCheck ()
    let manifestPath = Path.Combine(artifactRoot, manifestRelative)
    let finalPath = Path.Combine(artifactRoot, finalRelative)
    let notePath = Path.Combine(artifactRoot, noteRelative)

    if not (File.Exists manifestPath) then
        failwith $"Manifest artifact missing: {manifestPath}"

    if not (File.Exists finalPath) then
        failwith $"Final artifact missing: {finalPath}"

    if not (File.Exists notePath) then
        failwith $"Note artifact missing: {notePath}"

    let finalText = readText "final artifact" finalPath
    requireContains "final artifact token" finalText promptToken
    requireContains "final artifact date" finalText expectedDate
    let renderedArgvPath = Path.Combine(Path.GetDirectoryName finalPath, "rendered-argv.json")
    let renderedArgvText = readText "rendered argv artifact" renderedArgvPath
    requireContains "rendered argv codex exec" renderedArgvText "\"exec\""
    requireContains "rendered argv output last message" renderedArgvText "--output-last-message"

    printfn "TC-WEBR-010 AI intent output projection passed"
    printfn "hostUrl=%s" hostUrl
    printfn "pageUrl=%s" (hostUrl.TrimEnd('/') + "/page/codexfs-ai-chat")
    printfn "promptToken=%s" promptToken
    printfn "expectedDate=%s" expectedDate
    printfn "artifactRoot=%s" artifactRoot
    printfn "manifest=%s" manifestPath
    printfn "final=%s" finalPath
    printfn "note=%s" notePath
    printfn "renderedArgv=%s" renderedArgvPath
    printfn "screenshot=%s" screenshotPath
    printfn "outputBytes=%d" outputText.Length
    printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
finally
    match hostProcess with
    | Some proc when not proc.HasExited ->
        try
            proc.Kill(true)
        with _ ->
            ()
    | _ ->
        ()

    try
        hostProcess |> Option.iter (fun proc -> proc.WaitForExit(10000) |> ignore)
    with _ ->
        ()

    let hostStdout =
        try
            hostStdoutTask
            |> Option.map (fun task -> task.GetAwaiter().GetResult())
            |> Option.defaultValue ""
        with _ ->
            ""

    let hostStderr =
        try
            hostStderrTask
            |> Option.map (fun task -> task.GetAwaiter().GetResult())
            |> Option.defaultValue ""
        with _ ->
            ""

    hostProcess |> Option.iter (fun proc -> proc.Dispose())
    httpClient.Dispose()

    if hostStdout.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase)
       || hostStderr.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase) then
        failwith $"Host process emitted unhandled exception.\nSTDOUT:\n{hostStdout}\nSTDERR:\n{hostStderr}"

    if not (String.IsNullOrWhiteSpace hostStdout) then
        printfn "hostStdoutBytes=%d" hostStdout.Length

    if not (String.IsNullOrWhiteSpace hostStderr) then
        printfn "hostStderrBytes=%d" hostStderr.Length
