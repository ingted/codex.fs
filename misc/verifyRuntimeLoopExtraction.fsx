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
    | Engine of value: string
    | Executable of path: string
    | Timeout of value: string
    | Artifact_Root of path: string
    | Skip_Engine_E2E

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Repo_Root _ -> "Root path of the public codex.fs repo."
            | Configuration _ -> "Build configuration."
            | No_Restore -> "Pass --no-restore to dotnet build and dotnet run."
            | Engine _ -> "Engine family used by the delegated real-path engine verifier."
            | Executable _ -> "Engine executable path or command used by the delegated real-path verifier."
            | Timeout _ -> "Engine timeout as TimeSpan text."
            | Artifact_Root _ -> "Artifact root for delegated runtime verifier output."
            | Skip_Engine_E2E -> "Skip delegated real engine E2E only when the executable is unavailable; source and compiled tests still run."

let defaultArgumentsText =
    """
--repo-root "G:/codex.fs/src/codex.fs"
--configuration Debug
--engine agy
--executable agy
--timeout 00:02:00
--artifact-root ".codex.fs/runtime002-artifacts"
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyRuntimeLoopExtraction.fsx")

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

let engine =
    results.TryGetResult VerifyArgument.Engine
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue "agy"

let executable =
    results.TryGetResult VerifyArgument.Executable
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue engine

let timeout =
    results.TryGetResult VerifyArgument.Timeout
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue "00:02:00"
    |> TimeSpan.Parse

let artifactRoot =
    results.TryGetResult VerifyArgument.Artifact_Root
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.defaultValue ".codex.fs/runtime002-artifacts"
    |> fun path -> if Path.IsPathRooted path then path else Path.Combine(repoRoot, path)
    |> Path.GetFullPath

let strictUtf8 = UTF8Encoding(false, true)
let testsProject = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "codex.fs.Tests.fsproj")
let testsProgram = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "Program.fs")
let runtimeModule = Path.Combine(repoRoot, "src", "codex.fs", "RuntimePromptLoop.fs")
let coreProject = Path.Combine(repoRoot, "src", "codex.fs", "codex.fs.fsproj")
let hostCycle = Path.Combine(repoRoot, "src", "codex.fs.host", "SessionEngineCycle.fs")
let delegatedVerifier = Path.Combine(repoRoot, "misc", "verifyMessageToEngineReply.fsx")

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
        failwith $"{label} still contains forbidden text: {needle}"

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

let runtimeText = readText "RuntimePromptLoop.fs" runtimeModule
let coreProjectText = readText "codex.fs.fsproj" coreProject
let hostCycleText = readText "SessionEngineCycle.fs" hostCycle
let testsText = readText "tests Program.fs" testsProgram

requireAll
    "RuntimePromptLoop.fs"
    runtimeText
    [ "module RuntimePromptLoop"
      "type RuntimePromptInput"
      "type RuntimePromptPlan"
      "type AgyPrintExecutionInput"
      "type RuntimeExecutionPlan"
      "type RuntimeReplyIntent"
      "type RuntimeReadyToAckBoundary"
      "let planPrompt"
      "let planAgyPrintExecution"
      "let replyIntent"
      "let readyToAckBoundaryText" ]

requireContains "codex.fs.fsproj" coreProjectText "<Compile Include=\"RuntimePromptLoop.fs\" />"

requireAll
    "SessionEngineCycle.fs"
    hostCycleText
    [ "RuntimePromptLoop.planPrompt"
      "RuntimePromptLoop.planAgyPrintExecution"
      "RuntimePromptLoop.processOutcome"
      "RuntimePromptLoop.replyIntent"
      "RuntimePromptLoop.readyToAckBoundaryText" ]

requireNotContains "SessionEngineCycle.fs" hostCycleText "let messageBatchJsonl"
requireNotContains "SessionEngineCycle.fs" hostCycleText "let requestText"
requireNotContains "SessionEngineCycle.fs" hostCycleText "let renderedCommandText"
requireNotContains "SessionEngineCycle.fs" hostCycleText "let boundaryText"

requireAll
    "tests Program.fs"
    testsText
    [ "TC-RUNTIME-002 runtime prompt-loop plan passed"
      "CodexFs.RuntimePromptLoop.planPrompt"
      "CodexFs.RuntimePromptLoop.planAgyPrintExecution"
      "CodexFs.RuntimePromptLoop.readyToAckBoundaryText" ]

let buildArgs =
    [ yield "build"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess (TimeSpan.FromMinutes 5.0) "dotnet build tests" "dotnet" buildArgs

let runArgs =
    [ yield "run"
      yield "--project"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let runStdout, runStderr = runProcess (TimeSpan.FromMinutes 5.0) "dotnet run tests" "dotnet" runArgs
requireContains "dotnet run tests stdout" runStdout "TC-RUNTIME-002 runtime prompt-loop plan passed"

let mutable delegatedStdoutBytes = 0
let mutable delegatedStderrBytes = 0

if not (results.Contains VerifyArgument.Skip_Engine_E2E) then
    Directory.CreateDirectory artifactRoot |> ignore

    let sessionSuffix = Guid.NewGuid().ToString("N").Substring(0, 8)
    let sessionId = $"runtime002-{sessionSuffix}"

    let delegatedArgs =
        [ "fsi"
          "--exec"
          delegatedVerifier
          "--"
          "--session"
          sessionId
          "--artifact-root"
          artifactRoot
          "--workdir"
          repoRoot
          "--engine"
          engine
          "--executable"
          executable
          "--timeout"
          timeout.ToString()
          "--prompt-token"
          "CODEXFS_RUNTIME002" ]

    let delegatedStdout, delegatedStderr =
        runProcess (timeout + TimeSpan.FromMinutes 3.0) "delegated real MessageFabric engine verifier" "dotnet" delegatedArgs

    delegatedStdoutBytes <- delegatedStdout.Length
    delegatedStderrBytes <- delegatedStderr.Length
    requireContains "delegated verifier stdout" delegatedStdout "TC-E2E-002 message to engine reply passed"
    requireContains "delegated verifier stdout" delegatedStdout "TC-OPS-002 recovery/ack ordering passed"

printfn "TC-RUNTIME-002 runtime prompt-loop extraction verifier passed"
printfn "repoRoot=%s" repoRoot
printfn "runtimeModule=%s" runtimeModule
printfn "testsProject=%s" testsProject
printfn "artifactRoot=%s" artifactRoot
printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
printfn "runStdoutBytes=%d runStderrBytes=%d" runStdout.Length runStderr.Length
printfn "delegatedStdoutBytes=%d delegatedStderrBytes=%d" delegatedStdoutBytes delegatedStderrBytes
