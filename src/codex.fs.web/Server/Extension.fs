namespace CodexFs.Web.Server

open System
open System.IO
open System.Text
open System.Text.Json
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
      RegisterAppendPageShape: bool
      /// Optional artifact root used by the same-origin artifact read handler.
      ArtifactRoot: string option
      /// Maximum bytes returned by one artifact read response.
      ArtifactReadMaxBytes: int }

/// Defaults and helpers for `AIChatExtensionOptions`.
module AIChatExtensionOptions =

    /// Default same-origin asset route prefix.
    [<Literal>]
    let defaultAssetRoutePrefix = "/client-extensions/codexfs-ai-chat"

    /// Default same-origin artifact read path under the AI chat route prefix.
    [<Literal>]
    let defaultArtifactReadPath = "/client-extensions/codexfs-ai-chat/artifact/read"

    /// Default maximum artifact bytes returned by the browser read handler.
    [<Literal>]
    let defaultArtifactReadMaxBytes = 131072

    /// Default metadata JSON. It is intentionally small and secret-free.
    [<Literal>]
    let defaultMetadataJson =
        """{"schema":"codex.fs.web.ai-chat.v1","extensionId":"codex-fs-ai-chat","intentSchema":"codex.fs.web.ai-intent.v1","defaultTarget":"foreman","defaultPerspective":"self","defaultEngine":"agy","defaultModel":"default","defaultReasoning":"high","artifactReadEndpoint":"/client-extensions/codexfs-ai-chat/artifact/read","targetModes":[{"mode":"foreman","scope":"direct","participantId":"agent.codexfs.foreman","requiresValue":false},{"mode":"participant","scope":"direct","participantId":"","requiresValue":true},{"mode":"public","scope":"public","participantId":"","requiresValue":false},{"mode":"group","scope":"group","participantId":"","requiresValue":true}],"perspectiveModes":[{"mode":"self","senderPolicy":"current-user","requiresValue":false},{"mode":"participant-readonly","senderPolicy":"read-only","requiresValue":true}],"engineOptions":[{"engine":"agy","models":["default"],"reasoning":["medium","high","xhigh"]},{"engine":"codex","models":["default"],"reasoning":["medium","high","xhigh"]}],"invocationOptions":[{"name":"mode","values":["exec","print"]},{"name":"approval","values":["never","on-request"]}]}"""

    /// Default registration options.
    let defaults =
        { ExtensionId = Package.extensionId
          DisplayName = "codex.fs AI Chat"
          AssetRoutePrefix = defaultAssetRoutePrefix
          MetadataJson = Some defaultMetadataJson
          RegisterAppendPageShape = true
          ArtifactRoot = None
          ArtifactReadMaxBytes = defaultArtifactReadMaxBytes }

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

    /// Return the same-origin artifact read handler path for a route prefix.
    let artifactReadPath routePrefix =
        normalizeAssetRoutePrefix routePrefix + "/artifact/read"

/// Same-origin artifact read handler used by the browser bundle for expandable run stdio.
module AIChatArtifactRead =

    type ArtifactReadResponse =
        { status: string
          path: string
          text: string
          truncated: bool
          bytes: int64
          error: string }

    let jsonOptions = JsonSerializerOptions()

    let response status path text truncated bytes error =
        JsonSerializer.Serialize(
            { status = status
              path = path
              text = text
              truncated = truncated
              bytes = bytes
              error = error },
            jsonOptions
        )

    let ok path text truncated bytes =
        response "ok" path text truncated bytes ""

    let error path message =
        response "error" path "" false 0L message

    let stringProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object
           && element.TryGetProperty(name, &value)
           && value.ValueKind = JsonValueKind.String then
            value.GetString()
            |> Option.ofObj
            |> Option.defaultValue ""
            |> Some
        else
            None

    let intProperty (name: string) (element: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.ValueKind = JsonValueKind.Object
           && element.TryGetProperty(name, &value)
           && value.ValueKind = JsonValueKind.Number then
            match value.TryGetInt32() with
            | true, parsed -> Some parsed
            | _ -> None
        else
            None

    let parseRequest (bodyText: string) =
        if String.IsNullOrWhiteSpace bodyText then
            Error "Request body is required."
        else
            try
                use document = JsonDocument.Parse bodyText
                let root = document.RootElement

                match stringProperty "path" root with
                | Some path when not (String.IsNullOrWhiteSpace path) ->
                    Ok(path.Trim(), intProperty "maxBytes" root)
                | _ -> Error "Artifact path is required."
            with ex ->
                Error($"Artifact read request JSON could not be parsed: {ex.Message}")

    let safeRootPrefix (root: string) =
        let fullRoot = Path.GetFullPath root
        fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + string Path.DirectorySeparatorChar

    let resolveSafePath artifactRoot relativePath =
        if String.IsNullOrWhiteSpace artifactRoot then
            Error "Artifact root is not configured."
        elif String.IsNullOrWhiteSpace relativePath then
            Error "Artifact path is required."
        elif Path.IsPathFullyQualified relativePath || Path.IsPathRooted relativePath then
            Error "Artifact path must be relative."
        else
            try
                let root = Path.GetFullPath artifactRoot
                let normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)
                let fullPath = Path.GetFullPath(Path.Combine(root, normalizedRelative))
                let rootPrefix = safeRootPrefix root

                if fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) then
                    Ok fullPath
                else
                    Error "Artifact path must stay under artifact root."
            with ex ->
                Error($"Artifact path could not be resolved: {ex.Message}")

    let readArtifactText maxBytes fullPath =
        if not (File.Exists fullPath) then
            Error "Artifact file was not found."
        else
            try
                let fileInfo = FileInfo fullPath
                let effectiveMaxBytes = max 1024 maxBytes
                let bytesToRead = min fileInfo.Length (int64 effectiveMaxBytes)
                let buffer = Array.zeroCreate<byte> (int bytesToRead)

                use stream = File.OpenRead fullPath
                let read = stream.Read(buffer, 0, buffer.Length)
                let text = Encoding.UTF8.GetString(buffer, 0, read)
                Ok(text, fileInfo.Length > int64 read, fileInfo.Length)
            with ex ->
                Error($"Artifact file could not be read: {ex.Message}")

    let handleJson artifactRoot configuredMaxBytes bodyText =
        match parseRequest bodyText with
        | Error message -> error "" message
        | Ok(relativePath, requestedMaxBytes) ->
            let maxBytes =
                requestedMaxBytes
                |> Option.filter (fun value -> value > 0)
                |> Option.map (fun value -> min value configuredMaxBytes)
                |> Option.defaultValue configuredMaxBytes

            match resolveSafePath artifactRoot relativePath with
            | Error message -> error relativePath message
            | Ok fullPath ->
                match readArtifactText maxBytes fullPath with
                | Error message -> error relativePath message
                | Ok(text, truncated, bytes) -> ok relativePath text truncated bytes

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
            let artifactReadPath = AIChatExtensionOptions.artifactReadPath routePrefix
            let scripts = AIChatAssets.registerGeneratedScripts this options

            this.RegisterClientExtensionJsonPostHandler(metadataPath, fun _ -> metadataJson) |> ignore

            match options.ArtifactRoot |> Option.filter (String.IsNullOrWhiteSpace >> not) with
            | Some artifactRoot ->
                this.RegisterClientExtensionJsonPostHandler(
                    artifactReadPath,
                    AIChatArtifactRead.handleJson artifactRoot options.ArtifactReadMaxBytes
                )
                |> ignore
            | None -> ()

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
                      ValuePlaceholder = Some "Prompt; controls emit codex.fs.web.ai-intent.v1 JSON"
                      DefaultKey = Some "\"agent.codexfs.foreman\""
                      Tags = [ "codex.fs"; "ai-chat"; "agent" ] }
                |> ignore

            this

        /// Register the codex.fs AI chat WebSharper bundle with default options.
        member this.useAIChat() =
            this.useAIChat(AIChatExtensionOptions.defaults)
