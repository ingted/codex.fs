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

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyAiIntentControls.fsx")

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
let packageFile = Path.Combine(repoRoot, "src", "codex.fs.web", "Library.fs")
let extensionFile = Path.Combine(repoRoot, "src", "codex.fs.web", "Server", "Extension.fs")
let clientFile = Path.Combine(repoRoot, "src", "codex.fs.web", "Client", "AIChatClient.fs")
let generatedMainJs = Path.Combine(repoRoot, "src", "codex.fs.web", "wwwroot", "js", "CodexFs.Web.js")
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

let packageText = readText "Library.fs" packageFile
let extensionText = readText "Server/Extension.fs" extensionFile
let clientText = readText "Client/AIChatClient.fs" clientFile
let testsText = readText "tests Program.fs" testsProgram
let wbsText = readText "WBS.WEBR-001.md" webRewriteWbs
let testText = readText "Test.WEBR-001.md" webRewriteTest

requireAll
    "codex.fs.web source"
    (packageText + "\n" + extensionText + "\n" + clientText)
    [ "let intentSchema = \"codex.fs.web.ai-intent.v1\""
      "\"targetModes\""
      "\"perspectiveModes\""
      "\"engineOptions\""
      "\"invocationOptions\""
      "PulseTradeRegisterAppendInputRenderer"
      "codexfs-ai-controls"
      "codexfs-ai-target-mode"
      "codexfs-ai-perspective-mode"
      "codexfs-ai-reasoning"
      "AiChatIntentDto"
      "valueText"
      "keyJson" ]

requireNotContains "codex.fs.web source" clientText "codex exec"
requireNotContains "codex.fs.web source" clientText "--model"

requireAll
    "tests Program.fs"
    testsText
    [ "TC-WEBR-006 AI intent controls passed"
      "web shell ai intent metadata"
      "web shell main controls"
      "codexfs-ai-controls"
      "PulseTradeRegisterAppendInputRenderer"
      "Package.intentSchema" ]

requireAll
    "WBS/Test docs"
    (wbsText + "\n" + testText)
    [ "WEBR-006"
      "misc/verifyAiIntentControls.fsx" ]

let buildArgs =
    [ yield "build"
      yield solution
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let buildStdout, buildStderr = runProcess (TimeSpan.FromMinutes 6.0) "dotnet build solution" "dotnet" buildArgs

let generatedMainJsText = readText "CodexFs.Web.js" generatedMainJs

requireAll
    "generated CodexFs.Web.js"
    generatedMainJsText
    [ "PulseTradeRegisterAppendInputRenderer"
      "codexfs-ai-controls"
      "codexfs-ai-target-mode"
      "codexfs-ai-perspective-mode"
      "codexfs-ai-reasoning"
      "codex.fs.web.ai-intent.v1" ]

requireNotContains "generated CodexFs.Web.js" generatedMainJsText "codex exec"

let runArgs =
    [ yield "run"
      yield "--project"
      yield testsProject
      yield "--configuration"
      yield configuration
      if results.Contains VerifyArgument.No_Restore then
          yield "--no-restore" ]

let runStdout, runStderr = runProcess (TimeSpan.FromMinutes 6.0) "dotnet run tests" "dotnet" runArgs
requireContains "dotnet run tests stdout" runStdout "TC-WEBR-006 AI intent controls passed"

printfn "TC-WEBR-006 AI intent controls verifier passed"
printfn "repoRoot=%s" repoRoot
printfn "clientFile=%s" clientFile
printfn "generatedMainJs=%s" generatedMainJs
printfn "buildStdoutBytes=%d buildStderrBytes=%d" buildStdout.Length buildStderr.Length
printfn "runStdoutBytes=%d runStderrBytes=%d" runStdout.Length runStderr.Length
