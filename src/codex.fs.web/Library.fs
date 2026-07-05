namespace CodexFs.Web

/// Public marker and constants for the codex.fs PTCS WebSharper extension package.
module Package =

    /// Stable NuGet package identifier.
    [<Literal>]
    let id = "codex.fs.web"

    /// Stable PTCS client extension id registered through CommHub.
    [<Literal>]
    let extensionId = "codex-fs-ai-chat"

    /// Initial metadata schema for the AI chat extension.
    [<Literal>]
    let metadataSchema = "codex.fs.web.ai-chat.v1"

    /// Browser-to-runtime intent payload schema emitted by the AI chat controls.
    [<Literal>]
    let intentSchema = "codex.fs.web.ai-intent.v1"
