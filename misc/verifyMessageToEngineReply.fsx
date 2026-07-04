#r "nuget: FAkka.Argu, 10.1.301"
#r "nuget: PulseTrade.Comm.Spa, 0.2.5-beta71"
#I "../src/codex.fs/bin/Debug/net10.0"
#I "../src/codex.fs.ptcs/bin/Debug/net10.0"
#I "../src/codex.fs.host/bin/Debug/net10.0"
#r "codex.fs.dll"
#r "codex.fs.ptcs.dll"
#r "codex.fs.host.dll"
#load "ParseLine.fsx"

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Argu
open CodexFs
open CodexFs.Domain
open CodexFs.Host
open CodexFs.Ptcs
open PulseTrade.Comm.Spa

type VerifyArgument =
    | Session of sessionId: string
    | Artifact_Root of path: string
    | Workdir of path: string
    | Engine of engine: string
    | Executable of path: string
    | Timeout of text: string
    | Prompt_Token of token: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Session _ -> "Target session id for the verifier run."
            | Artifact_Root _ -> "Artifact root for verifier output; default is ignored by Git."
            | Workdir _ -> "Working directory passed to the engine process."
            | Engine _ -> "Engine family; E2E-002 currently expects agy."
            | Executable _ -> "Engine executable path or command."
            | Timeout _ -> "Engine timeout as TimeSpan text."
            | Prompt_Token _ -> "Non-secret token the real engine must echo in final output."

let defaultArgumentsText =
    """
--session e2e002-default
--artifact-root ".codex.fs/e2e002-artifacts"
--workdir "."
--engine agy
--executable agy
--timeout 00:02:00
--prompt-token CODEXFS_E2E_REPLY
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyMessageToEngineReply.fsx")

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

let requiredValue name value =
    match value with
    | Some value -> value
    | None -> failwith $"Missing required argument: {name}"

let sessionId = requiredValue "--session" (results.TryGetResult VerifyArgument.Session)
let artifactRoot = requiredValue "--artifact-root" (results.TryGetResult VerifyArgument.Artifact_Root)
let workdir = requiredValue "--workdir" (results.TryGetResult VerifyArgument.Workdir)
let engineText = requiredValue "--engine" (results.TryGetResult VerifyArgument.Engine)
let executable = requiredValue "--executable" (results.TryGetResult VerifyArgument.Executable)
let timeout = TimeSpan.Parse(requiredValue "--timeout" (results.TryGetResult VerifyArgument.Timeout))
let promptToken = requiredValue "--prompt-token" (results.TryGetResult VerifyArgument.Prompt_Token)

let engine =
    match HostConfig.parseEngineKind engineText with
    | Ok value -> value
    | Error message -> failwith message

if engine <> Agy then
    failwith "E2E-002 verifier currently supports --engine agy."

let absoluteArtifactRoot = Path.GetFullPath artifactRoot
let absoluteWorkdir = Path.GetFullPath workdir
Directory.CreateDirectory absoluteArtifactRoot |> ignore

let runSuffix = Guid.NewGuid().ToString("N").Substring(0, 8)
let verifierToken = $"{promptToken}_{runSuffix}"
let userParticipantId = $"user.codexfs.e2e.{runSuffix}"

let settings =
    Map.ofList
        [ "artifact.root", absoluteArtifactRoot
          "engine.default", "agy"
          "engine.enabled", "agy"
          "engine.agy.executable", executable
          "timeout.default", timeout.ToString()
          "ptcs.fabricMode", "package-owned"
          "ptcs.sessionParticipantPrefix", "agent.codexfs.e2e"
          "ptcs.replyParticipantId", "agent.codexfs.e2e.host"
          "ptcs.defaultInboxLimit", "10"
          "message.maxPendingPerTurn", "10" ]

let runtime =
    match HostConfig.loadFromMap settings |> HostRuntime.tryCreateFromLoadResult with
    | Ok value -> HostRuntime.startInProcessMessageFabric DateTimeOffset.UtcNow value
    | Error issues -> failwith $"Host runtime config failed: {issues}"

let fabric =
    runtime.MessageFabric |> Option.defaultWith (fun () -> failwith "Expected package-owned MessageFabric.")

let sessionParticipantId = HostControl.sessionParticipantId runtime.Config sessionId
let sessionBinding = HostControl.sessionBinding runtime.Config sessionId
let userBinding = MessageFabricBinding.defaultBinding userParticipantId

let runTask (task: Task<'T>) =
    task.GetAwaiter().GetResult()

let userRegistration =
    { MessageFabricBinding.defaultRegistration with
        Kind = Some "user"
        DisplayName = Some userParticipantId }

runTask (MessageFabricBinding.registerParticipantAsync fabric userBinding userRegistration) |> ignore
runTask (MessageFabricBinding.registerParticipantAsync fabric sessionBinding MessageFabricBinding.defaultRegistration) |> ignore

let promptBody = $"Reply exactly: {verifierToken}"

runTask
    (MessageFabricBinding.sendAsync
        fabric
        { FromParticipantId = userParticipantId
          Scope = MessageFabricScope.Direct sessionParticipantId
          Body = promptBody
          Tags = [ "codex.fs"; "e2e002"; "verify" ]
          CorrelationId = Some $"e2e002-{runSuffix}"
          CreatedAtUtc = None })
|> ignore

let cycleResult =
    runTask
        (SessionEngineCycle.runSingleCycleAsync
            runtime
            { SessionId = sessionId
              Engine = Some Agy
              ExecutablePath = Some executable
              WorkingDirectory = Some absoluteWorkdir
              ArtifactRoot = Some absoluteArtifactRoot
              Timeout = Some timeout
              SystemInstruction =
                Some "This is a codex.fs verifier. Find the latest PTCS message body and reply with exactly the requested token, with no explanation."
              AdditionalDirectories = [] }
            CancellationToken.None)

if cycleResult.Status <> "completed" then
    failwith $"Expected completed cycle; actual={cycleResult.Status}; reply={cycleResult.ReplyBody}"

if cycleResult.ConsumedMessageCount < 1 then
    failwith "Expected at least one consumed PTCS message."

if String.IsNullOrWhiteSpace cycleResult.ArtifactManifestPath then
    failwith "Expected artifact manifest path."

if String.IsNullOrWhiteSpace cycleResult.FinalMessagePath then
    failwith "Expected final message artifact path."

let manifestPath = Path.Combine(absoluteArtifactRoot, cycleResult.ArtifactManifestPath)
let finalPath = Path.Combine(absoluteArtifactRoot, cycleResult.FinalMessagePath)

if not (File.Exists manifestPath) then
    failwith $"Manifest not found: {manifestPath}"

if not (File.Exists finalPath) then
    failwith $"Final message not found: {finalPath}"

let finalText = File.ReadAllText(finalPath, UTF8Encoding(false, true))

if not (finalText.Contains(verifierToken, StringComparison.Ordinal)) then
    failwith $"Final message did not contain verifier token. finalPath={finalPath}"

let replyBatch =
    runTask
        (MessageFabricBinding.waitInboxAsync
            fabric
            userBinding
            None
            (TimeSpan.FromSeconds 10.0)
            (TimeSpan.FromMilliseconds 100.0)
            (Some CancellationToken.None))

let reply =
    replyBatch.Messages
    |> List.tryFind (fun message -> message.MessageId = cycleResult.ReplyMessageId)
    |> Option.defaultWith (fun () -> failwith "Expected reply message in user inbox.")

if not (reply.Body.Contains(cycleResult.ArtifactManifestPath, StringComparison.Ordinal)) then
    failwith "Reply body did not contain artifact manifest reference."

let afterAck = runTask (MessageFabricBinding.pollInboxAsync fabric sessionBinding None)

if afterAck.Messages.Length <> 0 then
    failwith $"Expected session inbox to be empty after ack; count={afterAck.Messages.Length}"

let stopped = HostRuntime.stop runtime

if stopped.MessageFabric.IsSome then
    failwith "Expected HostRuntime.stop to clear MessageFabric."

printfn "TC-E2E-002 message to engine reply passed"
printfn "sessionId=%s" sessionId
printfn "sessionParticipantId=%s" sessionParticipantId
printfn "replyMessageId=%s" cycleResult.ReplyMessageId
printfn "artifactRoot=%s" absoluteArtifactRoot
printfn "manifest=%s" manifestPath
printfn "final=%s" finalPath
