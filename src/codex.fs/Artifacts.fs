namespace CodexFs

open System
open CodexFs.Domain

/// Artifact manifest contracts for one codex.fs engine run.
module Artifacts =

    /// Known artifact categories emitted by a run.
    type ArtifactKind =
        /// Normalized run request JSON.
        | RequestJson
        /// PTCS message batch captured for prompt construction.
        | PtcsMessageBatchJsonl
        /// Prompt markdown submitted to the engine.
        | PromptMarkdown
        /// Rendered command arguments captured as JSON.
        | RenderedArgvJson
        /// Raw or redacted standard output log, according to profile.
        | StdoutLog
        /// Raw or redacted standard error log, according to profile.
        | StderrLog
        /// Structured engine event stream, when supported.
        | EventJsonl
        /// Final engine message markdown.
        | FinalMarkdown
        /// Normalized run result JSON.
        | ResultJson
        /// Compacted history markdown.
        | CompactionMarkdown

    /// Reference to a single artifact file.
    type ArtifactRef =
        { /// Artifact category.
          Kind: ArtifactKind
          /// Artifact path recorded by the artifact store.
          Path: string
          /// SHA-256 hash of the artifact bytes.
          Sha256: string
          /// Artifact size in bytes.
          Size: int64
          /// UTC creation time.
          CreatedUtc: DateTimeOffset }

    /// Manifest describing all artifacts produced by one run.
    type ArtifactManifest =
        { /// Run identity.
          RunId: RunId
          /// Session identity.
          SessionId: SessionId
          /// Engine family used by this run.
          Engine: EngineKind
          /// Concrete engine surface id.
          SurfaceId: string
          /// PTCS messages used for this run.
          PtcsMessages: PtcsMessageRef list
          /// Optional PTCS durable task reference.
          PtcsTask: PtcsTaskRef option
          /// UTC start time.
          StartedUtc: DateTimeOffset
          /// UTC completion time, when terminal.
          CompletedUtc: DateTimeOffset option
          /// Terminal run outcome.
          Outcome: RunOutcome
          /// Artifact references included in the manifest.
          Artifacts: ArtifactRef list }
