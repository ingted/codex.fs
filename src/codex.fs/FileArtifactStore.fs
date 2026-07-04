namespace CodexFs

open System
open System.IO
open System.Security.Cryptography
open System.Text
open CodexFs.Artifacts
open CodexFs.Domain

/// File-system backed artifact write primitives.
module FileArtifactStore =

    /// Configuration for the file artifact store.
    type FileArtifactStoreConfig =
        { /// Root directory that contains all session artifacts.
          ArtifactRoot: string }

    /// Result of writing one artifact file.
    type StoredArtifact =
        { /// Manifest reference for the written artifact.
          Reference: ArtifactRef
          /// Absolute path written on the local file system.
          AbsolutePath: string }

    /// Returns the normalized artifact root path.
    let artifactRoot (config: FileArtifactStoreConfig) =
        if String.IsNullOrWhiteSpace config.ArtifactRoot then
            invalidArg (nameof config.ArtifactRoot) "Artifact root is required."

        Path.GetFullPath config.ArtifactRoot

    /// Returns the run directory for a session and run.
    let runDirectory config (SessionId sessionId) (RunId runId) =
        Path.Combine(artifactRoot config, "sessions", sessionId, "runs", runId)
        |> Path.GetFullPath

    /// Normalizes and validates a caller-provided artifact relative path.
    let normalizeRelativePath (relativePath: string) =
        if String.IsNullOrWhiteSpace relativePath then
            invalidArg (nameof relativePath) "Artifact relative path is required."

        let normalized =
            relativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar)

        if Path.IsPathRooted normalized then
            invalidArg (nameof relativePath) "Artifact path must be relative."

        let parts =
            normalized.Split(
                [| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |],
                StringSplitOptions.RemoveEmptyEntries
            )

        if parts.Length = 0 then
            invalidArg (nameof relativePath) "Artifact relative path is empty."

        if parts |> Array.exists (fun part -> part = "." || part = "..") then
            invalidArg (nameof relativePath) "Artifact relative path cannot contain '.' or '..' segments."

        Path.Combine parts

    /// Resolves a validated artifact path under the run directory.
    let resolveArtifactPath config sessionId runId relativePath =
        let runDir = runDirectory config sessionId runId
        let safeRelativePath = normalizeRelativePath relativePath
        let fullPath = Path.Combine(runDir, safeRelativePath) |> Path.GetFullPath
        let comparison =
            if OperatingSystem.IsWindows() then
                StringComparison.OrdinalIgnoreCase
            else
                StringComparison.Ordinal

        let runDirWithSeparator =
            if runDir.EndsWith(string Path.DirectorySeparatorChar, comparison) then
                runDir
            else
                runDir + string Path.DirectorySeparatorChar

        if not (fullPath.StartsWith(runDirWithSeparator, comparison)) then
            invalidArg (nameof relativePath) "Artifact path must stay under the run directory."

        fullPath

    /// Computes a lowercase SHA-256 hex digest for bytes.
    let sha256Hex (bytes: byte array) =
        if isNull bytes then
            nullArg (nameof bytes)

        SHA256.HashData bytes
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    /// Writes bytes as a new artifact and returns its artifact reference.
    let writeBytes config sessionId runId kind relativePath (bytes: byte array) createdUtc =
        if isNull bytes then
            nullArg (nameof bytes)

        let fullPath = resolveArtifactPath config sessionId runId relativePath
        let parent = Path.GetDirectoryName fullPath

        if String.IsNullOrWhiteSpace parent then
            invalidOp "Artifact parent directory could not be resolved."

        Directory.CreateDirectory parent |> ignore

        use stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)
        stream.Write(bytes, 0, bytes.Length)

        let root = artifactRoot config
        let relativeStoredPath = Path.GetRelativePath(root, fullPath)

        { Reference =
            { Kind = kind
              Path = relativeStoredPath
              Sha256 = sha256Hex bytes
              Size = int64 bytes.LongLength
              CreatedUtc = createdUtc }
          AbsolutePath = fullPath }

    /// Writes UTF-8 text without BOM as a new artifact and returns its artifact reference.
    let writeText config sessionId runId kind relativePath (text: string) createdUtc =
        if isNull text then
            nullArg (nameof text)

        let utf8 = UTF8Encoding(false, true)
        writeBytes config sessionId runId kind relativePath (utf8.GetBytes text) createdUtc
