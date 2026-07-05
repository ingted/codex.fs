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
