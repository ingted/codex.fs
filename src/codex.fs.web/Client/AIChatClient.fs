namespace CodexFs.Web.Client

open WebSharper
open WebSharper.JavaScript

[<JavaScript>]
module AIChatClient =

    /// Client-side marker used by verifier tooling to confirm the generated bundle loaded.
    [<Literal>]
    let loadedMarkerName = "CodexFsAiChatLoaded"

    /// WebSharper bundle entrypoint. Full UI controls are implemented by later WEBR slices.
    [<SPAEntryPoint>]
    let Main () =
        ()
