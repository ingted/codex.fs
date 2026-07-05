#r "nuget: FAkka.Argu, 10.1.301"
#load "ParseLine.fsx"

open System
open System.IO
open System.Text
open Argu

type VerifyArgument =
    | Ptcs_Root of path: string
    | Ptcs_Host_Root of path: string
    | Dynamic_Root of path: string
    | Codexfs_Root of path: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Ptcs_Root _ -> "Root path of PulseTrade.Comm.Spa source."
            | Ptcs_Host_Root _ -> "Root path of PulseTrade.Comm.Spa.Host source."
            | Dynamic_Root _ -> "Root path of PulseTrade.Comm.Spa.Dynamic source."
            | Codexfs_Root _ -> "Root path of the public codex.fs repo."

let defaultArgumentsText =
    """
--ptcs-root "G:/PulseTrade2.fs/Libs/PulseTrade.Comm.Spa"
--ptcs-host-root "G:/PulseTrade.fs/Libs/PulseTrade.Comm/src/PulseTrade.Comm.Spa.Host"
--dynamic-root "C:/Users/Administrator/test_gemini/PulseTrade.Comm.Spa.Dynamic/src"
--codexfs-root "G:/codex.fs/src/codex.fs"
"""

let parser = ArgumentParser.Create<VerifyArgument>(programName = "verifyPtcsClassicShellInventory.fsx")

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

let required name value =
    match value with
    | Some value when not (String.IsNullOrWhiteSpace value) -> Path.GetFullPath value
    | _ -> failwith $"Missing required argument: {name}"

let ptcsRoot = required "--ptcs-root" (results.TryGetResult VerifyArgument.Ptcs_Root)
let ptcsHostRoot = required "--ptcs-host-root" (results.TryGetResult VerifyArgument.Ptcs_Host_Root)
let dynamicRoot = required "--dynamic-root" (results.TryGetResult VerifyArgument.Dynamic_Root)
let codexfsRoot = required "--codexfs-root" (results.TryGetResult VerifyArgument.Codexfs_Root)

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

let ptcsFsproj = readText "PTCS fsproj" (Path.Combine(ptcsRoot, "PulseTrade.Comm.Spa.fsproj"))
let ptcsServer = readText "PTCS Server.fs" (Path.Combine(ptcsRoot, "Server.fs"))
let ptcsClient = readText "PTCS Client.fs" (Path.Combine(ptcsRoot, "Client.fs"))
let ptcsStore = readText "PTCS Store.fs" (Path.Combine(ptcsRoot, "Store.fs"))
let ptcsDomain = readText "PTCS Domain.fs" (Path.Combine(ptcsRoot, "Domain.fs"))
let ptcsMessageFabric = readText "PTCS MessageFabric.fs" (Path.Combine(ptcsRoot, "MessageFabric.fs"))
let ptcsBrowserVerifier = readText "PTCS browser verifier" (Path.Combine(ptcsRoot, "Scripts", "verify.browserUi.playwright.fsx"))
let ptcsHostProgram = readText "PTCS Host Program.fs" (Path.Combine(ptcsHostRoot, "Program.fs"))
let dynamicFsproj = readText "Dynamic fsproj" (Path.Combine(dynamicRoot, "PulseTrade.Comm.Spa.Dynamic.fsproj"))
let dynamicExtension = readText "Dynamic Extension.fs" (Path.Combine(dynamicRoot, "Server", "Extension.fs"))
let codexHostControl = readText "codex.fs HostControl.fs" (Path.Combine(codexfsRoot, "src", "codex.fs.host", "HostControl.fs"))

requireAll
    "PTCS package shape"
    ptcsFsproj
    [ "<Version>0.2.5-beta71</Version>"
      "<WebSharperProject>Html</WebSharperProject>"
      "<WebSharperRunCompiler>true</WebSharperRunCompiler>"
      "<PackageReference Include=\"Akka.Cluster.Sharding\""
      "<PackageReference Include=\"PulseTrade.Comm.Actor.Registry\"" ]

requireAll
    "PTCS classic routes"
    ptcsServer
    [ "path \"/\" >=> redirect \"/chat\""
      "path \"/chat\" >=> page options"
      "path \"/sets\" >=> page options"
      "path \"/actors\" >=> page options"
      "path \"/chat/api/agents\" >=> agentsApi options.Hub"
      "path \"/chat/api/thread\" >=> threadApi options"
      "path \"/chat/api/send\" >=> sendApi options"
      "path \"/sync/ws\" >=> streamWebSocket options"
      "clientExtensionScriptAsset"
      "clientExtensionJsonPostEndpoint" ]

requireAll
    "PTCS chat APIs"
    ptcsServer
    [ "hub.ListParticipants(Some \"agent\", Some true)"
      "Domain.fixedPublicParticipant now"
      "durableThread options.Hub participantId peerId"
      "Json.deserialize<ChatSendRequest>"
      "authorizeAcl options ctx PtcsAcl.actionChatSend" ]

requireAll
    "PTCS classic DOM"
    ptcsClient
    [ "page.ClassName <- \"page chat-grid\""
      "setTestId (\"nav-\" + label.ToLower())"
      "setTestId \"chat-work\""
      "setTestId \"chat-pending-state\""
      "setId \"thread-list\" |> setTestId \"thread-list\""
      "setTestId \"chat-composer\""
      "setTestId \"chat-draft\""
      "setTestId \"chat-send\""
      "setTestId \"chat-participant\""
      "\"/chat/api/agents\""
      "\"/chat/api/thread?participantId=\""
      "\"chat-send\""
      "sendChatSyncFrame" ]

requireAll
    "PTCS browser verifier"
    ptcsBrowserVerifier
    [ "page.GotoAsync(baseUrl + \"/chat\")"
      "waitForSelector page \"[data-testid='chat-draft']\""
      "waitForSelector page \"[data-testid='chat-participant']\""
      "page.FillAsync(\"[data-testid='chat-draft']\", wsChatBody)"
      "page.ClickAsync(\"[data-testid='chat-send']\")"
      "document.querySelector('[data-testid=\\\"thread-list\\\"]')" ]

requireAll
    "PTCS CommHub participant/message"
    ptcsStore
    [ "member _.RegisterParticipant(args: RegisterParticipantArgs)"
      "member _.ListParticipants(kind: string option, includeOffline: bool option)"
      "member _.SendMessage(args: SendMessageArgs)"
      "member _.Thread(participantId: string, peerId: string, afterMessageId: string option)"
      "member _.RegisterClientExtension(registration: ClientExtensionRegistration)"
      "member _.RegisterClientExtensionScriptAsset(asset: ClientExtensionScriptAsset)"
      "member _.RegisterClientExtensionJsonPostHandler(urlPath: string, handler: string -> string)" ]

requireAll
    "PTCS extension DTO"
    ptcsDomain
    [ "type ClientAppendPageShapeRegistration"
      "type AppendPageShapeTemplateRegistration"
      "type ClientExtensionRegistration"
      "ExtensionId: string"
      "MetadataJson: string option"
      "ScriptUrls: string list"
      "AppendPageShapes: ClientAppendPageShapeRegistration list"
      "type ClientExtensionScriptAsset" ]

requireAll
    "PTCS MessageFabric"
    ptcsMessageFabric
    [ "type MessageFabricScope"
      "| Public of channel: string option"
      "| Direct of toParticipantId: MessageFabricParticipantId"
      "| Group of groupId: string"
      "member _.RegisterParticipantAsync(args: RegisterParticipantArgs)"
      "member this.SendAsync(args: MessageFabricAppend)"
      "member _.PollInboxAsync(query: MessageFabricInboxQuery)"
      "member this.WaitInboxAsync(wait: MessageFabricInboxWait)"
      "member _.AckAsync(ack: MessageFabricInboxAck)"
      "member this.DrainInboxAsync(query: MessageFabricInboxQuery)" ]

requireAll
    "Dynamic bundle project"
    dynamicFsproj
    [ "<WebSharperProject>Bundle</WebSharperProject>"
      "<WebSharperBundleOutputDir>wwwroot\\js</WebSharperBundleOutputDir>"
      "<WebSharperRunCompiler>true</WebSharperRunCompiler>"
      "<GeneratePackageOnBuild>true</GeneratePackageOnBuild>"
      "<PackageReference Include=\"PulseTrade.Comm.Spa\" Version=\"[0.2.5-beta71]\" />"
      "<Compile Include=\"Server/Extension.fs\" />"
      "<Compile Include=\"Client/DynamicRenderer.fs\" />"
      "<Compile Include=\"Client/ArguFormRenderer.fs\" />"
      "<Compile Include=\"Client/ActorDynamicTab.fs\" />" ]

requireAll
    "Dynamic extension seam"
    dynamicExtension
    [ "member this.useDynamicSdui(actorSystem: ActorSystem, metadata: DynamicArguMetadata, registrations: DynamicArguTemplateRegistration seq)"
      "this.RegisterClientExtensionJsonPostHandler("
      "this.RegisterClientExtensionScriptAsset asset"
      "this.RegisterClientExtension"
      "ExtensionId = \"pulse-trade-comm-spa-dynamic\""
      "DisplayName = Some \"PulseTrade.Comm.Spa.Dynamic\""
      "ScriptUrls = scripts"
      "AppendPageShapes ="
      "Dynamic browser bundle 必須由 WebSharper/F# 產生後再接入" ]

requireAll
    "PTCS Host composition"
    ptcsHostProgram
    [ "let createExtensionActorFabric (hub: CommHub)"
      "OptionalClientExtensions.tryLoadDynamicApi options"
      "OptionalClientExtensions.useDynamicSdui options api hub fabric.System registrations"
      "Server.startWithSharing"
      "commSpaHost.routes chat=%s/chat sets=%s/sets actors=%s/actors" ]

requireAll
    "codex.fs standalone cut-list"
    codexHostControl
    [ "let LegacyChat = \"/chat\""
      "let DiagnosticsSessionSend = \"/diagnostics/session-send\""
      "Use PTCS chat"
      "Product browser chat belongs to the PTCS WebSharper chat room."
      "This standalone host provides HTTP control and diagnostics only."
      "Return a guard page that points browser chat users to PTCS Web."
      "Return a diagnostic form for sending prompts through standalone host MessageFabric." ]

printfn "TC-WEBR-002 PTCS classic shell inventory passed"
printfn "ptcsRoot=%s" ptcsRoot
printfn "ptcsHostRoot=%s" ptcsHostRoot
printfn "dynamicRoot=%s" dynamicRoot
printfn "codexfsRoot=%s" codexfsRoot
