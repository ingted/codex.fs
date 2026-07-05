#r "nuget: FAkka.Argu, 10.1.301"
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

type VerifyArgument =
    | Repo_Root of path: string
    | Configuration of value: string
    | No_Restore
    | Host_Address of value: string
    | Host_Port of value: int
    | Host_Run_Seconds of value: int

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repo_Root _ -> "Root path of the public codex.fs repo."
            | Configuration _ -> "Build configuration."
            | No_Restore -> "Pass --no-restore to dotnet build/run."
            | Host_Address _ -> "LAN/browser host address, or auto."
            | Host_Port _ -> "PTCS web shell port; 0 reserves a free port."
            | Host_Run_Seconds _ -> "Bounded host process lifetime."

let defaultArgumentsText =
    """
--repo-root "G:/codex.fs/src/codex.fs"
--configuration Debug
--host-address auto
--host-port 0
--host-run-seconds 300
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyAiIntentBridge.fsx")

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
    |> Option.defaultValue 300
    |> max 120

let requestedHostPort =
    results.TryGetResult VerifyArgument.Host_Port
    |> Option.defaultValue 0

let strictUtf8 = UTF8Encoding(false, true)
let solution = Path.Combine(repoRoot, "codex.fs.slnx")
let hostToolProject = Path.Combine(repoRoot, "src", "codex.fs.host.tool", "codex.fs.host.tool.fsproj")

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
let verifierToken = "CODEXFS_BRIDGE_" + runSuffix
let expectedDate = DateTimeOffset.Now.ToString("yyyy-MM-dd")
let promptText =
    $"hi 請用 powershell 執行 Get-Date -Format yyyy-MM-dd 取日期時間，最後只回覆 token {verifierToken} 與日期 {expectedDate}。"

let artifactRoot = Path.Combine(repoRoot, ".codex.fs", "webr009-artifacts", "webr009-" + runSuffix)
let pcslRoot = Path.Combine(repoRoot, ".codex.fs", "webr009-pcsl", "pcsl-" + runSuffix)

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

let jsonString value = JsonSerializer.Serialize(value)

let aiIntentJson () =
    let tags = """["codex.fs","ai-chat","intent","webr009"]"""

    "{"
    + "\"schema\":" + jsonString "codex.fs.web.ai-intent.v1" + ","
    + "\"target\":{\"mode\":\"foreman\",\"scope\":\"direct\",\"participantId\":\"agent.codexfs.foreman\",\"groupId\":\"\"},"
    + "\"perspective\":{\"mode\":\"self\",\"participantId\":\"\",\"senderPolicy\":\"current-user\"},"
    + "\"engine\":{\"engine\":\"codex\",\"model\":\"gpt-5-codex\",\"reasoning\":\"medium\"},"
    + "\"invocation\":{\"mode\":\"exec\",\"approval\":\"never\"},"
    + "\"body\":" + jsonString promptText + ","
    + "\"tags\":" + tags
    + "}"

let appendIntentAsync (client: HttpClient) =
    task {
        let requestJson =
            "{"
            + "\"pageId\":\"codexfs-ai-chat\","
            + "\"keyJson\":\"\\\"agent.codexfs.foreman\\\"\","
            + "\"valueText\":" + jsonString (aiIntentJson ()) + ","
            + "\"direction\":null,"
            + "\"tags\":[\"codex.fs\",\"webr009\",\"ai-intent\"]"
            + "}"

        use content = new StringContent(requestJson, strictUtf8, "application/json")
        use! response = client.PostAsync(hostUrl.TrimEnd('/') + "/pages/api/append", content)
        let! body = response.Content.ReadAsStringAsync()

        if not response.IsSuccessStatusCode then
            failwith $"append intent failed status={(int response.StatusCode)} body={body}"

        return body
    }

let waitForFinalArtifact () =
    let deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds 240.0
    let mutable found: string option = None

    while found.IsNone && DateTimeOffset.UtcNow < deadline do
        if Directory.Exists artifactRoot then
            let files = Directory.GetFiles(artifactRoot, "final.md", SearchOption.AllDirectories)

            found <-
                files
                |> Array.sortByDescending File.GetLastWriteTimeUtc
                |> Array.tryFind (fun path ->
                    let text = File.ReadAllText(path, strictUtf8)
                    text.Contains(verifierToken, StringComparison.Ordinal)
                    && text.Contains(expectedDate, StringComparison.Ordinal))

        if found.IsNone then
            Thread.Sleep 1000

    found |> Option.defaultWith (fun () -> failwith $"Timed out waiting for final artifact containing {verifierToken} and {expectedDate}.")

let requireContains label (content: string) (needle: string) =
    if not (content.Contains(needle, StringComparison.Ordinal)) then
        failwith $"{label} missing required text: {needle}"

let renderedArguments (renderedText: string) =
    use document = JsonDocument.Parse(renderedText)
    let root = document.RootElement
    let fileName = root.GetProperty("fileName").GetString()

    let arguments =
        root.GetProperty("arguments").EnumerateArray()
        |> Seq.map (fun item -> item.GetString())
        |> Seq.map (fun value -> if isNull value then "" else value)
        |> Seq.toList

    fileName, arguments

let httpClient = new HttpClient()
httpClient.Timeout <- TimeSpan.FromSeconds 10.0
let hostProcess = startHostProcess ()
let hostStdoutTask = hostProcess.StandardOutput.ReadToEndAsync()
let hostStderrTask = hostProcess.StandardError.ReadToEndAsync()

try
    awaitHttpOk httpClient (hostUrl.TrimEnd('/') + "/chat")
    let appendReply = (appendIntentAsync httpClient).GetAwaiter().GetResult()
    requireContains "append reply" appendReply "codex.fs.web.ai-intent.v1"

    let finalPath = waitForFinalArtifact ()
    let finalText = File.ReadAllText(finalPath, strictUtf8)
    requireContains "final artifact token" finalText verifierToken
    requireContains "final artifact date" finalText expectedDate

    let runDirectory = Path.GetDirectoryName finalPath
    let renderedPath = Path.Combine(runDirectory, "rendered-argv.json")
    let renderedText = File.ReadAllText(renderedPath, strictUtf8)
    let renderedFileName, arguments = renderedArguments renderedText

    let renderedExecutableName = Path.GetFileName(renderedFileName)

    if not (String.Equals(renderedExecutableName, "codex", StringComparison.OrdinalIgnoreCase)
            || String.Equals(renderedExecutableName, "codex.exe", StringComparison.OrdinalIgnoreCase)) then
        failwith $"Expected codex executable; actual={renderedFileName}"

    if arguments |> List.tryHead <> Some "exec" then
        let renderedArgsText = String.concat " " arguments
        failwith $"Expected codex exec argv; actual={renderedArgsText}"

    if not (arguments |> List.exists ((=) "--dangerously-bypass-approvals-and-sandbox")) then
        failwith "Expected codex approval/sandbox bypass flag."

    if arguments |> List.exists ((=) "--model") then
        failwith "Expected unsupported subscription model gpt-5-codex to be normalized to Codex CLI default."

    if not (arguments |> List.exists ((=) "--output-last-message")) then
        failwith "Expected codex output-last-message argv."

    printfn "TC-WEBR-009 AI intent bridge passed"
    printfn "hostUrl=%s" hostUrl
    printfn "verifierToken=%s" verifierToken
    printfn "expectedDate=%s" expectedDate
    printfn "artifactRoot=%s" artifactRoot
    printfn "final=%s" finalPath
    printfn "renderedArgv=%s" renderedPath
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
