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

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyPtcsActorFabricForeman.fsx")

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
let testsProject = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "codex.fs.Tests.fsproj")
let testsProgram = Path.Combine(repoRoot, "tests", "codex.fs.Tests", "Program.fs")
let ptcsProject = Path.Combine(repoRoot, "src", "codex.fs.ptcs", "codex.fs.ptcs.fsproj")
let actorBinding = Path.Combine(repoRoot, "src", "codex.fs.ptcs", "ActorFabricBinding.fs")
let webRewriteWbs = Path.Combine(repoRoot, "doc", "WBS.WEBR-001.md")
let webRewriteTest = Path.Combine(repoRoot, "doc", "Test.WEBR-001.md")

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

let actorBindingText = readText "ActorFabricBinding.fs" actorBinding
let ptcsProjectText = readText "codex.fs.ptcs.fsproj" ptcsProject
let testsText = readText "tests Program.fs" testsProgram
let wbsText = readText "WBS.WEBR-001.md" webRewriteWbs
let testText = readText "Test.WEBR-001.md" webRewriteTest

requireAll
    "ActorFabricBinding.fs"
    actorBindingText
    [ "module ActorFabricBinding"
      "type WorkerParticipantSpec"
      "type EnsureParticipantRegistered"
      "type SpawnWorkerParticipant"
      "type WorkerParticipantRegistered"
      "type WorkerParticipantSpawned"
      "type CodexWorkerActor"
      "inherit ReceiveActor"
      "MessageFabricBinding.defaultBinding"
      "MessageFabricBinding.registerParticipantAsync"
      "spawnWorker" ]

requireNotContains "ActorFabricBinding.fs" actorBindingText "fake mailbox"
requireContains "codex.fs.ptcs.fsproj" ptcsProjectText "<Compile Include=\"ActorFabricBinding.fs\" />"

requireAll
    "tests Program.fs"
    testsText
    [ "CommSpaActorFabric.startWithOptions"
      "ActorFabricBinding.spawnWorker"
      "ActorFabricBinding.WorkerParticipantSpawned"
      "ListParticipantsAsync(Some \"agent\", Some true)"
      "TC-ACTOR-002 PTCS ActorFabric Foreman/Worker participants passed" ]

requireAll
    "WBS/Test docs"
    (wbsText + "\n" + testText)
    [ "ACTOR-002"
      "misc/verifyPtcsActorFabricForeman.fsx"
      "| ACTOR-002 | Implement PTCS ActorFabric Foreman/Worker proof"
      "| T-ACTOR-002 | ACTOR-002 | `misc/verifyPtcsActorFabricForeman.fsx`"
      "Pass" ]

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
requireContains "dotnet run tests stdout" runStdout "TC-ACTOR-002 PTCS ActorFabric Foreman/Worker participants passed"

printfn "TC-ACTOR-002 PTCS ActorFabric Foreman/Worker verifier passed"
printfn "repoRoot=%s" repoRoot
printfn "actorBinding=%s" actorBinding
printfn "testsProject=%s" testsProject
printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
printfn "runStdoutBytes=%d runStderrBytes=%d" runStdout.Length runStderr.Length
