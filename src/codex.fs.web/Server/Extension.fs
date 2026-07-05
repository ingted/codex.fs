namespace CodexFs.Web.Server

open System
open System.IO
open CodexFs.Web
open PulseTrade.Comm.Spa

/// Server-side registration options for the codex.fs AI chat client extension.
type AIChatExtensionOptions =
    { /// Stable PTCS client extension id.
      ExtensionId: string
      /// Operator-facing extension name.
      DisplayName: string
      /// Same-origin URL prefix used for generated script assets and fixed JSON handlers.
      AssetRoutePrefix: string
      /// Optional extension metadata JSON consumed by the WebSharper client bundle.
      MetadataJson: string option
      /// True when the extension should expose an append-page shape placeholder.
      RegisterAppendPageShape: bool }

/// Defaults and helpers for `AIChatExtensionOptions`.
module AIChatExtensionOptions =

    /// Default same-origin asset route prefix.
    [<Literal>]
    let defaultAssetRoutePrefix = "/client-extensions/codexfs-ai-chat"

    /// Default metadata JSON. It is intentionally small and secret-free.
    [<Literal>]
    let defaultMetadataJson =
        """{"schema":"codex.fs.web.ai-chat.v1","extensionId":"codex-fs-ai-chat","defaultTarget":"foreman"}"""

    /// Default registration options.
    let defaults =
        { ExtensionId = Package.extensionId
          DisplayName = "codex.fs AI Chat"
          AssetRoutePrefix = defaultAssetRoutePrefix
          MetadataJson = Some defaultMetadataJson
          RegisterAppendPageShape = true }

    /// Normalize the route prefix to a same-origin absolute path without a trailing slash.
    let normalizeAssetRoutePrefix value =
        let text =
            if String.IsNullOrWhiteSpace value then
                defaultAssetRoutePrefix
            else
                value.Trim()

        let text = if text.StartsWith("/", StringComparison.Ordinal) then text else "/" + text
        text.TrimEnd('/')

    /// Return defaults when caller passes a null record from an interop boundary.
    let orDefaults options =
        if isNull (box options) then defaults else options

/// Generated asset discovery and registration.
module AIChatAssets =

    /// Candidate generated asset directories for local build output and NuGet content files.
    let candidateGeneratedAssetDirectories (assemblyDirectory: string) =
        [ Path.Combine(assemblyDirectory, "wwwroot", "js")
          Path.Combine(assemblyDirectory, "..", "..", "..", "wwwroot", "js")
          Path.Combine(assemblyDirectory, "..", "..", "content", "wwwroot", "js")
          Path.Combine(assemblyDirectory, "..", "..", "contentFiles", "any", "net10.0", "wwwroot", "js")
          Path.Combine(assemblyDirectory, "..", "..", "contentFiles", "any", "any", "wwwroot", "js") ]
        |> List.map Path.GetFullPath

    /// Find the generated WebSharper asset directory if it exists.
    let tryFindGeneratedAssetDirectory assemblyDirectory =
        candidateGeneratedAssetDirectories assemblyDirectory
        |> List.tryFind Directory.Exists

    /// Create an extension URL from a generated asset relative path.
    let assetUrl (routePrefix: string) (relativePath: string) =
        let prefix = AIChatExtensionOptions.normalizeAssetRoutePrefix routePrefix
        let relativePath = relativePath.Replace("\\", "/").TrimStart('/')
        prefix + "/js/" + relativePath

    /// Register generated JavaScript assets and return the script URLs that should be loaded by PTCS.
    let registerGeneratedScripts (hub: CommHub) (options: AIChatExtensionOptions) =
        let assembly = typeof<AIChatExtensionOptions>.Assembly
        let assemblyDirectory = Path.GetDirectoryName assembly.Location

        match tryFindGeneratedAssetDirectory assemblyDirectory with
        | None -> []
        | Some jsDirectory ->
            let files =
                Directory.GetFiles(jsDirectory, "*.js", SearchOption.AllDirectories)
                |> Array.sort
                |> Array.toList

            let urls =
                files
                |> List.map (fun file ->
                    let relativePath = Path.GetRelativePath(jsDirectory, file).Replace("\\", "/")
                    let url = assetUrl options.AssetRoutePrefix relativePath
                    let asset: ClientExtensionScriptAsset =
                        { Url = url
                          ContentType = "application/javascript"
                          Content = File.ReadAllText file }

                    hub.RegisterClientExtensionScriptAsset asset |> ignore
                    url, relativePath)

            let headScripts =
                urls
                |> List.filter (fun (_, relativePath) -> relativePath.EndsWith(".head.js", StringComparison.OrdinalIgnoreCase))
                |> List.map fst

            let mainScripts =
                urls
                |> List.filter (fun (_, relativePath) ->
                    (relativePath.EndsWith("CodexFs.Web.js", StringComparison.OrdinalIgnoreCase)
                     || relativePath.EndsWith("codex.fs.web.js", StringComparison.OrdinalIgnoreCase))
                    && not (relativePath.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)))
                |> List.map fst

            match headScripts @ mainScripts with
            | [] ->
                urls
                |> List.filter (fun (_, relativePath) -> not (relativePath.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)))
                |> List.map fst
            | scripts -> scripts

[<AutoOpen>]
module CommHubExtensions =

    type CommHub with
        /// Register the codex.fs AI chat WebSharper bundle and extension metadata on an existing PTCS hub.
        member this.useAIChat(options: AIChatExtensionOptions) =
            let options = AIChatExtensionOptions.orDefaults options
            let routePrefix = AIChatExtensionOptions.normalizeAssetRoutePrefix options.AssetRoutePrefix
            let metadataJson = options.MetadataJson |> Option.defaultValue AIChatExtensionOptions.defaultMetadataJson
            let metadataPath = routePrefix + "/metadata"
            let scripts = AIChatAssets.registerGeneratedScripts this options

            this.RegisterClientExtensionJsonPostHandler(metadataPath, fun _ -> metadataJson) |> ignore

            let appendPageShapes =
                if options.RegisterAppendPageShape then
                    [ { Shape = "codexfs-ai-chat"
                        Label = Some "AI Chat"
                        Badge = Some "ai"
                        ClassName = Some "codexfs-ai-chat" } ]
                else
                    []

            this.RegisterClientExtension
                { ExtensionId = options.ExtensionId
                  DisplayName = Some options.DisplayName
                  MetadataJson = Some metadataJson
                  ScriptUrls = scripts
                  AppendPageShapes = appendPageShapes }
            |> ignore

            if options.RegisterAppendPageShape then
                this.RegisterAppendPageShapeTemplate
                    { Shape = "codexfs-ai-chat"
                      Description = Some "codex.fs AI chat participant surface."
                      KeyPlaceholder = Some "\"agent.codexfs.foreman\""
                      ValuePlaceholder = Some "Prompt or intent metadata"
                      DefaultKey = Some "\"agent.codexfs.foreman\""
                      Tags = [ "codex.fs"; "ai-chat"; "agent" ] }
                |> ignore

            this

        /// Register the codex.fs AI chat WebSharper bundle with default options.
        member this.useAIChat() =
            this.useAIChat(AIChatExtensionOptions.defaults)
