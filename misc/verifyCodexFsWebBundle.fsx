#r "nuget: FAkka.Argu, 10.1.301"
#r "System.IO.Compression.FileSystem"
#load "ParseLine.fsx"

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
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
            | No_Restore -> "Pass --no-restore to dotnet build."

let defaultArgumentsText =
    """
--repo-root "G:/codex.fs/src/codex.fs"
--configuration Debug
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyCodexFsWebBundle.fsx")

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

let projectDir = Path.Combine(repoRoot, "src", "codex.fs.web")
let projectPath = Path.Combine(projectDir, "codex.fs.web.fsproj")
let wsConfigPath = Path.Combine(projectDir, "wsconfig.json")
let strictUtf8 = UTF8Encoding(false, true)

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

let runDotnetBuild () =
    let startInfo = ProcessStartInfo("dotnet")
    startInfo.WorkingDirectory <- repoRoot
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    startInfo.Environment["WebSharperBuildService"] <- "false"
    startInfo.Environment["WebSharperBuildServiceLogging"] <- "false"
    startInfo.ArgumentList.Add("build")
    startInfo.ArgumentList.Add(projectPath)
    startInfo.ArgumentList.Add("--configuration")
    startInfo.ArgumentList.Add(configuration)

    if results.Contains VerifyArgument.No_Restore then
        startInfo.ArgumentList.Add("--no-restore")

    use proc = new Process()
    proc.StartInfo <- startInfo

    if not (proc.Start()) then
        failwith "Failed to start dotnet build."

    let stdout = proc.StandardOutput.ReadToEnd()
    let stderr = proc.StandardError.ReadToEnd()
    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        failwith $"dotnet build failed exit={proc.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}"

    stdout, stderr

let fsproj = readText "codex.fs.web fsproj" projectPath
let wsconfig = readText "codex.fs.web wsconfig" wsConfigPath
let serverExtension = readText "codex.fs.web server extension" (Path.Combine(projectDir, "Server", "Extension.fs"))
let clientEntry = readText "codex.fs.web client entry" (Path.Combine(projectDir, "Client", "AIChatClient.fs"))
let slnx = readText "solution" (Path.Combine(repoRoot, "codex.fs.slnx"))

requireAll
    "codex.fs.web fsproj"
    fsproj
    [ "<PackageId>codex.fs.web</PackageId>"
      "<AssemblyName>CodexFs.Web</AssemblyName>"
      "<RootNamespace>CodexFs.Web</RootNamespace>"
      "<WebSharperProject>Bundle</WebSharperProject>"
      "<WebSharperBundleOutputDir>wwwroot\\js</WebSharperBundleOutputDir>"
      "<WebSharperRunCompiler>true</WebSharperRunCompiler>"
      "<GeneratePackageOnBuild>true</GeneratePackageOnBuild>"
      "<PackageReference Include=\"PulseTrade.Comm.Spa\" Version=\"[0.2.5-beta71]\" />"
      "<PackageReference Include=\"WebSharper.FSharp\" Version=\"10.1.5.674\" />"
      "<Compile Include=\"Server\\Extension.fs\" />"
      "<Compile Include=\"Client\\AIChatClient.fs\" />" ]

requireAll
    "codex.fs.web wsconfig"
    wsconfig
    [ "\"buildService\": false"
      "\"buildServiceLogging\": false" ]

requireContains "solution" slnx "src/codex.fs.web/codex.fs.web.fsproj"

requireAll
    "codex.fs.web server extension"
    serverExtension
    [ "member this.useAIChat(options: AIChatExtensionOptions)"
      "RegisterClientExtensionJsonPostHandler"
      "RegisterClientExtensionScriptAsset"
      "RegisterClientExtension"
      "ExtensionId = options.ExtensionId"
      "Shape = \"codexfs-ai-chat\"" ]

requireAll
    "codex.fs.web client entry"
    clientEntry
    [ "[<SPAEntryPoint>]"
      "let Main ()"
      "CodexFsAiChatLoaded" ]

let handWrittenJs =
    Directory.GetFiles(projectDir, "*.js", SearchOption.AllDirectories)
    |> Array.filter (fun path ->
        let relativePath = Path.GetRelativePath(projectDir, path).Replace("\\", "/")
        not (relativePath.StartsWith("wwwroot/js/", StringComparison.OrdinalIgnoreCase))
        && not (relativePath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
        && not (relativePath.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)))

if handWrittenJs.Length > 0 then
    failwith ("Hand-written JavaScript files are not allowed: " + String.Join("; ", handWrittenJs))

let stdout, stderr = runDotnetBuild ()

let generatedDir = Path.Combine(projectDir, "wwwroot", "js")

if not (Directory.Exists generatedDir) then
    failwith $"Generated WebSharper bundle directory not found: {generatedDir}"

let generatedJs =
    Directory.GetFiles(generatedDir, "*.js", SearchOption.AllDirectories)
    |> Array.map Path.GetFullPath
    |> Array.sort

if generatedJs.Length = 0 then
    failwith $"No generated JavaScript bundle files found under {generatedDir}"

let mainBundle =
    generatedJs
    |> Array.tryFind (fun path ->
        let fileName = Path.GetFileName path
        fileName.Equals("CodexFs.Web.js", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("codex.fs.web.js", StringComparison.OrdinalIgnoreCase))

match mainBundle with
| Some _ -> ()
| None -> failwith "Expected generated main bundle CodexFs.Web.js."

let packageDir = Path.Combine(projectDir, "bin", configuration)
let packagePath =
    Directory.GetFiles(packageDir, "codex.fs.web.*.nupkg", SearchOption.TopDirectoryOnly)
    |> Array.sort
    |> Array.tryLast
    |> Option.defaultWith (fun () -> failwith $"Generated nupkg not found under {packageDir}")

let readPackageEntries path =
    use packageArchive = ZipFile.OpenRead(path)
    packageArchive.Entries
    |> Seq.map (fun entry -> entry.FullName.Replace("\\", "/"))
    |> Seq.toArray

let packageEntries = readPackageEntries packagePath

let requirePackageEntry label (predicate: string -> bool) =
    if not (packageEntries |> Array.exists predicate) then
        let sample = packageEntries |> Array.truncate 40 |> String.concat "\n"
        failwith $"{label} not found in nupkg {packagePath}. Entry sample:\n{sample}"

requirePackageEntry
    "CodexFs.Web.js package content"
    (fun entry -> entry.Equals("content/wwwroot/js/CodexFs.Web.js", StringComparison.OrdinalIgnoreCase))

requirePackageEntry
    "CodexFs.Web.head.js package content"
    (fun entry -> entry.Equals("content/wwwroot/js/CodexFs.Web.head.js", StringComparison.OrdinalIgnoreCase))

printfn "TC-WEBR-003 codex.fs.web WebSharper bundle passed"
printfn "repoRoot=%s" repoRoot
printfn "project=%s" projectPath
printfn "generatedDir=%s" generatedDir
printfn "generatedJs=%d" generatedJs.Length
printfn "package=%s" packagePath
printfn "stdoutBytes=%d stderrBytes=%d" stdout.Length stderr.Length
