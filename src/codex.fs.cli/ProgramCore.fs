namespace CodexFs.Cli

open System.IO
open System.Net.Http
open System.Threading

module ProgramCore =

    /// Return true when argv asks for root help without entering command dispatch.
    let isRootHelp (argv: string array) =
        argv.Length = 0
        || (argv.Length = 1
            && (argv[0] = "help" || argv[0] = "--help" || argv[0] = "-h" || argv[0] = "/?"))

    /// Run the CLI command surface with a display program name selected by the shim.
    let run programName (argv: string array) =
        if isRootHelp argv then
            printfn "%s" (Cli.helpTextFor programName)
            0
        else
            match Cli.tryParseSessionSendFor programName argv with
            | Error message ->
                eprintfn "%s" message
                2
            | Ok(Some sendOptions) ->
                use handler = new HttpClientHandler(UseProxy = false)
                use client = new HttpClient(handler, true)

                match Cli.tryResolvePromptText File.ReadAllText sendOptions.Prompt with
                | Error message ->
                    eprintfn "%s" message
                    2
                | Ok promptText ->
                    let resolvedSendOptions = { sendOptions with Prompt = promptText }
                    let result =
                        CliHttp.sendSessionMessageAsync client CancellationToken.None resolvedSendOptions
                        |> fun task -> task.GetAwaiter().GetResult()

                    if result.IsSuccess then
                        printfn "%s" result.Body
                        0
                    else
                        eprintfn "%s" result.Body
                        1
            | Ok None ->
                match Cli.tryParseSessionReadFor programName argv with
                | Error message ->
                    eprintfn "%s" message
                    2
                | Ok(Some readCommand) ->
                    use handler = new HttpClientHandler(UseProxy = false)
                    use client = new HttpClient(handler, true)

                    let result =
                        match readCommand with
                        | Cli.SessionStatus options -> CliHttp.getSessionStatusAsync client CancellationToken.None options
                        | Cli.SessionAttach options -> CliHttp.attachSessionAsync client CancellationToken.None options
                        | Cli.SessionDrain options -> CliHttp.drainSessionAsync client CancellationToken.None options
                        |> fun task -> task.GetAwaiter().GetResult()

                    if result.IsSuccess then
                        printfn "%s" result.Body
                        0
                    else
                        eprintfn "%s" result.Body
                        1
                | Ok None ->
                    match Cli.tryParseHostStatusFor programName argv with
                    | Error message ->
                        eprintfn "%s" message
                        2
                    | Ok(Some options) ->
                        use handler = new HttpClientHandler(UseProxy = false)
                        use client = new HttpClient(handler, true)
                        let result =
                            CliHttp.getHostStatusAsync client CancellationToken.None options
                            |> fun task -> task.GetAwaiter().GetResult()

                        if result.IsSuccess then
                            printfn "%s" result.Body
                            0
                        else
                            eprintfn "%s" result.Body
                            1
                    | Ok None ->
                        printfn "Command parsed. Runtime execution is implemented by later WBS items."
                        0
