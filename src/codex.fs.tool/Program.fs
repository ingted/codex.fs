namespace CodexFs.Tool

module Program =

    /// Entry point for the short `codex.fs` alias over the canonical CLI command surface.
    [<EntryPoint>]
    let main (argv: string array) =
        CodexFs.Cli.ProgramCore.run CodexFs.Cli.Cli.ShortProgramName argv
