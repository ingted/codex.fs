module PL

open System.Text

let parseLine (delimiters: char[]) (quoteChar: char option) (escapeCharOpt: char option) (ifMergeDelimiters: bool) (inputLine: string) =
    let escapeChar = defaultArg escapeCharOpt '\\'
    let result = ResizeArray<string>()
    let field = StringBuilder()
    let mutable inQuotes = false
    let mutable escaped = false
    let mutable fieldStarted = false

    let addField () =
        if fieldStarted || not ifMergeDelimiters then
            result.Add(field.ToString())
            field.Clear() |> ignore
            fieldStarted <- false

    for c in inputLine do
        if escaped then
            field.Append c |> ignore
            fieldStarted <- true
            escaped <- false
        elif c = escapeChar then
            escaped <- true
            fieldStarted <- true
        elif quoteChar = Some c then
            inQuotes <- not inQuotes
            fieldStarted <- true
        elif not inQuotes && Array.contains c delimiters then
            addField ()
        else
            field.Append c |> ignore
            fieldStarted <- true

    if escaped then
        field.Append escapeChar |> ignore

    if inQuotes then
        failwith "parseLine failed: quote not finished"

    addField ()
    result.ToArray()
