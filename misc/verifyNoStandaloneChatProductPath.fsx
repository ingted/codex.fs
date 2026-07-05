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

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyNoStandaloneChatProductPath.fsx")

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
let hostControl = Path.Combine(repoRoot, "src", "codex.fs.host", "HostControl.fs")
let hostTool = Path.Combine(repoRoot, "src", "codex.fs.host.tool", "Program.fs")

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

let runProcess label fileName args =
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

    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failwith $"{label} failed exit={proc.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}"

    stdout, stderr

let hostControlText = readText "HostControl.fs" hostControl
let hostToolText = readText "host tool Program.fs" hostTool
let testsProgramText = readText "tests Program.fs" testsProgram

requireAll
    "HostControl.fs"
    hostControlText
    [ "standalone control host only exposes diagnostics"
      "web.profile=ptcs-webshell"
      "This standalone host provides HTTP control and diagnostics only."
      "codex.fs diagnostics session send"
      "Product browser chat belongs to the PTCS WebSharper chat room"
      "Return a guard page that points browser chat users to PTCS Web." ]

requireAll
    "host tool Program.fs"
    hostToolText
    [ "web.profile=ptcs-webshell"
      "profile=ptcs-webshell"
      "chatUri=" ]

requireAll
    "tests Program.fs"
    testsProgramText
    [ "TC-WEBR-008 no standalone chat product path passed"
      "host legacy chat no composer"
      "host legacy chat no manifest"
      "host legacy chat no prompt form"
      "host diagnostics no ptcs manifest"
      "web shell not guard page" ]

let buildArgs =
    [ yield "build"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess "dotnet build tests" "dotnet" buildArgs

let runArgs =
    [ yield "run"
      yield "--project"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let runStdout, runStderr = runProcess "dotnet run tests" "dotnet" runArgs

requireContains "dotnet run tests stdout" runStdout "TC-WEBR-008 no standalone chat product path passed"

printfn "TC-WEBR-008 no standalone chat product path verifier passed"
printfn "repoRoot=%s" repoRoot
printfn "testsProject=%s" testsProject
printfn "hostControl=%s" hostControl
printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
printfn "runStdoutBytes=%d runStderrBytes=%d" runStdout.Length runStderr.Length
