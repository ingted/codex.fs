#r "nuget: FAkka.Argu, 10.1.301"
#load "ParseLine.fsx"

open System
open System.Diagnostics
open System.IO
open System.Text
open Argu

type VerifyArgument =
    | Repo_Root of path: string
    | Configuration of value: string
    | No_Restore

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repo_Root _ -> "Root path of the public codex.fs repo."
            | Configuration _ -> "Build configuration."
            | No_Restore -> "Pass --no-restore to dotnet build and dotnet run."

let defaultArgumentsText =
    """
--repo-root "G:/codex.fs/src/codex.fs"
--configuration Debug
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyActorRuntimeArtifactProvider.fsx")

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

let strictUtf8 = UTF8Encoding(false, true)
let solution = Path.Combine(repoRoot, "codex.fs.slnx")
let testsProject = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "codex.fs.Tests.fsproj")
let testsProgram = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "Program.fs")
let ptcsProject = Path.Combine(repoRoot, "src", "codex.fs.ptcs", "codex.fs.ptcs.fsproj")
let runtimeCycle = Path.Combine(repoRoot, "src", "codex.fs.ptcs", "RuntimeMessageFabricCycle.fs")
let actorBinding = Path.Combine(repoRoot, "src", "codex.fs.ptcs", "ActorFabricBinding.fs")
let hostCycle = Path.Combine(repoRoot, "src", "codex.fs.host", "SessionEngineCycle.fs")
let rfc = Path.Combine(repoRoot, "doc", "RFC", "RFC-ACTOR-0002.actor-runtime-artifact-provider.md")
let wbs = Path.Combine(repoRoot, "doc", "WBS.md")
let wbsDetail = Path.Combine(repoRoot, "doc", "WBS.ACTOR-003.md")
let test = Path.Combine(repoRoot, "doc", "Test.md")
let testDetail = Path.Combine(repoRoot, "doc", "Test.ACTOR-003.md")
let verification = Path.Combine(repoRoot, "doc", "Verification.md")

let readText label path =
    let fullPath = Path.GetFullPath path

    if not (File.Exists fullPath) then
        failwith $"{label} not found: {fullPath}"

    File.ReadAllText(fullPath, strictUtf8)

let requireContains label (content: string) (needle: string) =
    if not (content.Contains(needle, StringComparison.Ordinal)) then
        failwith $"{label} missing required text: {needle}"

let requireNotContains label (content: string) (needle: string) =
    if content.Contains(needle, StringComparison.OrdinalIgnoreCase) then
        failwith $"{label} contains forbidden text: {needle}"

let requireAll label content needles =
    needles |> List.iter (requireContains label content)

let runProcess (timeoutLimit: TimeSpan) label fileName args =
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

    if not (proc.WaitForExit(timeoutLimit)) then
        try
            proc.Kill(entireProcessTree = true)
        with _ ->
            ()

        failwith $"{label} timed out after {timeoutLimit}."

    let stdout = stdoutTask.GetAwaiter().GetResult()
    let stderr = stderrTask.GetAwaiter().GetResult()

    if proc.ExitCode <> 0 then
        failwith $"{label} failed exit={proc.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}"

    stdout, stderr

let valueAfterPrefix prefix (text: string) =
    text.Split([| "\r\n"; "\n" |], StringSplitOptions.None)
    |> Array.tryFind (fun line -> line.StartsWith(prefix, StringComparison.Ordinal))
    |> Option.map (fun line -> line.Substring(prefix.Length).Trim())
    |> Option.defaultWith (fun () -> failwith $"Missing output line prefix: {prefix}")

let runtimeText = readText "RuntimeMessageFabricCycle.fs" runtimeCycle
let actorText = readText "ActorFabricBinding.fs" actorBinding
let hostText = readText "SessionEngineCycle.fs" hostCycle
let ptcsProjectText = readText "codex.fs.ptcs.fsproj" ptcsProject
let testsText = readText "tests Program.fs" testsProgram
let docsText =
    [ rfc; wbs; wbsDetail; test; testDetail; verification ]
    |> List.map (readText "ACTOR-003 docs")
    |> String.concat "\n"

requireAll
    "RuntimeMessageFabricCycle.fs"
    runtimeText
    [ "module RuntimeMessageFabricCycle"
      "type RuntimeCycleOptions"
      "type RuntimeCycleResult"
      "RuntimePromptLoop.planPrompt"
      "RuntimePromptLoop.planAgyPrintExecution"
      "ProcessRunner.runAsync"
      "MessageFabricBinding.sendDirectReplyAsync"
      "RunNoteMarkdown"
      "RuntimePromptLoop.readyToAckBoundaryText"
      "MessageFabricBinding.ackInboxAsync" ]

requireNotContains "RuntimeMessageFabricCycle.fs" runtimeText "fake mailbox"

requireAll
    "ActorFabricBinding.fs"
    actorText
    [ "type RunRuntimeCycle"
      "type RuntimeCycleCompleted"
      "HandleRunRuntimeCycle"
      "RuntimeMessageFabricCycle.runSingleCycleAsync" ]

requireContains "codex.fs.ptcs.fsproj" ptcsProjectText "<Compile Include=\"RuntimeMessageFabricCycle.fs\" />"
requireContains "SessionEngineCycle.fs" hostText "RuntimeMessageFabricCycle.runSingleCycleAsync"
requireNotContains "SessionEngineCycle.fs" hostText "ProcessRunner.runAsync"
requireNotContains "SessionEngineCycle.fs" hostText "RuntimePromptLoop.planPrompt"

requireAll
    "tests Program.fs"
    testsText
    [ "Ask<ActorFabricBinding.RuntimeCycleCompleted>"
      "ActorFabricBinding.RunRuntimeCycle"
      "TC-ACTOR-003 actor runtime artifact provider passed"
      "actor003Manifest="
      "actor003Boundary="
      "actor003Final="
      "actor003Note="
      "actor003ReplyMessageId=" ]

requireAll
    "ACTOR-003 docs"
    docsText
    [ "RFC-ACTOR-0002"
      "ACTOR-003"
      "T-ACTOR-003"
      "misc/verifyActorRuntimeArtifactProvider.fsx"
      "WorkerActor invokes PTCS runtime artifact provider" ]

let buildArgs =
    [ yield "build"
      yield solution
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess (TimeSpan.FromMinutes 8.0) "dotnet build solution" "dotnet" buildArgs

let runArgs =
    [ yield "run"
      yield "--project"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let runStdout, runStderr = runProcess (TimeSpan.FromMinutes 10.0) "dotnet run tests" "dotnet" runArgs
requireContains "dotnet run tests stdout" runStdout "TC-ACTOR-003 actor runtime artifact provider passed"

let manifestPath = valueAfterPrefix "actor003Manifest=" runStdout
let boundaryPath = valueAfterPrefix "actor003Boundary=" runStdout
let finalPath = valueAfterPrefix "actor003Final=" runStdout
let notePath = valueAfterPrefix "actor003Note=" runStdout
let replyMessageId = valueAfterPrefix "actor003ReplyMessageId=" runStdout

for label, path in [ "manifest", manifestPath; "boundary", boundaryPath; "final", finalPath; "note", notePath ] do
    if not (File.Exists path) then
        failwith $"{label} artifact not found: {path}"

let boundaryText = File.ReadAllText(boundaryPath, strictUtf8)
let noteText = File.ReadAllText(notePath, strictUtf8)
requireContains "actor003 boundary" boundaryText "ready-to-ack"
requireContains "actor003 boundary" boundaryText replyMessageId
requireContains "actor003 boundary" boundaryText "note.md"
requireContains "actor003 note" noteText "codex.fs run note"

printfn "TC-ACTOR-003 actor runtime artifact provider verifier passed"
printfn "repoRoot=%s" repoRoot
printfn "runtimeCycle=%s" runtimeCycle
printfn "actorBinding=%s" actorBinding
printfn "manifest=%s" manifestPath
printfn "boundary=%s" boundaryPath
printfn "final=%s" finalPath
printfn "note=%s" notePath
printfn "replyMessageId=%s" replyMessageId
printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
printfn "runStdoutBytes=%d runStderrBytes=%d" runStdout.Length runStderr.Length
