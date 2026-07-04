namespace CodexFs.Cli

open System

module Program =

    /// Entry point for the compiled codex.fs.cli terminal client.
    [<EntryPoint>]
    let main argv =
        if argv.Length = 0 then
            printfn "%s" (Cli.helpText ())
            0
        else
            match Cli.tryParse argv with
            | Ok() ->
                printfn "Command parsed. Runtime execution is implemented by later WBS items."
                0
            | Error message ->
                eprintfn "%s" message
                2
