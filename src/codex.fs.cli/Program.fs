namespace CodexFs.Cli

open System
open System.Net.Http
open System.Threading

module Program =

    /// Entry point for the compiled codex.fs.cli terminal client.
    [<EntryPoint>]
    let main argv =
        if argv.Length = 0 then
            printfn "%s" (Cli.helpText ())
            0
        else
            match Cli.tryParseSessionSend argv with
            | Ok(Some sendOptions) ->
                use handler = new HttpClientHandler(UseProxy = false)
                use client = new HttpClient(handler, true)
                let result = CliHttp.sendSessionMessageAsync client CancellationToken.None sendOptions |> fun task -> task.GetAwaiter().GetResult()

                if result.IsSuccess then
                    printfn "%s" result.Body
                    0
                else
                    eprintfn "%s" result.Body
                    1
            | Ok None ->
                printfn "Command parsed. Runtime execution is implemented by later WBS items."
                0
            | Error message ->
                eprintfn "%s" message
                2
