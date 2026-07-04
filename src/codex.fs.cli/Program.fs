namespace CodexFs.Cli

module Program =

    /// Return true when argv asks for root help without entering command dispatch.
    let isRootHelp (argv: string array) =
        ProgramCore.isRootHelp argv

    /// Entry point for the compiled `codex.fs.cli` terminal client.
    [<EntryPoint>]
    let main (argv: string array) =
        ProgramCore.run Cli.CanonicalProgramName argv
