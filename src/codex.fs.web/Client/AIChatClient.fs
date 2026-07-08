namespace CodexFs.Web.Client

open CodexFs.Web
open System
open WebSharper
open WebSharper.JavaScript
open WebSharper.JavaScript.Dom

[<JavaScript>]
module AIChatClient =

    /// Client-side marker used by verifier tooling to confirm the generated bundle loaded.
    [<Literal>]
    let loadedMarkerName = "CodexFsAiChatLoaded"

    [<JavaScript>]
    type AppendInputContextDto =
        { shape: string
          selectedKeyJson: string
          selectedKeys: string[]
          valueText: string
          submit: obj -> unit
          setValue: obj -> unit }

    [<JavaScript>]
    type AiTargetIntentDto =
        { mode: string
          scope: string
          participantId: string
          groupId: string }

    [<JavaScript>]
    type AiPerspectiveIntentDto =
        { mode: string
          participantId: string
          senderPolicy: string }

    [<JavaScript>]
    type AiEngineIntentDto =
        { engine: string
          model: string
          reasoning: string }

    [<JavaScript>]
    type AiInvocationIntentDto =
        { mode: string
          approval: string }

    [<JavaScript>]
    type AiChatIntentDto =
        { schema: string
          target: AiTargetIntentDto
          perspective: AiPerspectiveIntentDto
          engine: AiEngineIntentDto
          invocation: AiInvocationIntentDto
          body: string
          tags: string[] }

    [<JavaScript>]
    type AiChatSubmitPayloadDto =
        { valueText: string
          keyJson: string }

    [<JavaScript>]
    type ArtifactReplyDto =
        { runId: string
          outcome: string
          manifestPath: string
          finalPath: string
          stdoutPath: string
          stderrPath: string
          notePath: string
          summary: string }

    [<JavaScript>]
    type ArtifactReadRequestDto =
        { path: string
          maxBytes: int }

    [<JavaScript>]
    type ProjectedReplyDto =
        { messageId: string
          fromId: string
          body: string
          createdAtUtc: string }

    let doc = JS.Document

    let aiIntentBridgeParticipantId = "user.codexfs.web.ai-intent"

    let artifactReadEndpoint = "/client-extensions/codexfs-ai-chat/artifact/read"

    let artifactReadMaxBytes = 131072

    let asText (value: string) =
        if isNull value || JS.TypeOf(box value) = JS.Kind.Undefined then "" else value

    let isBlank value =
        String.IsNullOrWhiteSpace(asText value)

    let sameTextInvariant left right =
        (asText left).ToLower() = (asText right).ToLower()

    let element tag className textValue =
        let node = doc.CreateElement tag

        if not (isBlank className) then
            node.ClassName <- className

        if not (isNull textValue) then
            node.TextContent <- textValue

        node

    let setTestId id (node: #Element) =
        if not (isBlank id) then
            node.SetAttribute("data-testid", id)

        node

    let setStyle cssText (node: #Element) =
        node.SetAttribute("style", cssText)
        node

    let elementValue (node: #Element) =
        (node |> As<HTMLInputElement>).Value

    let setElementValue (node: #Element) value =
        (node |> As<HTMLInputElement>).Value <- value

    let append (parent: Node) (children: Node[]) =
        children |> Array.iter (fun child -> parent.AppendChild child |> ignore)
        parent

    let clearChildren (node: #Element) =
        JS.Inline("while($0.firstChild){$0.removeChild($0.firstChild);}", node)

    let urlEncode (value: string) =
        JS.Inline<string>("encodeURIComponent($0)", asText value)

    let getJson (url: string) (onOk: obj -> unit) (onError: string -> unit) =
        JS.Inline(
            "(function(url,onOk,onError){var options={cache:'no-store'};globalThis.fetch(url,options).then(function(response){return response.text().then(function(body){if(response.ok){onOk(JSON.parse(body||'{}'));}else{onError(body||('GET '+url+' '+response.status));}});})['catch'](function(error){onError(String(error&&error.message?error.message:error));});})($0,$1,$2)",
            url,
            onOk,
            onError)

    let postJson (url: string) (body: string) (onOk: obj -> unit) (onError: string -> unit) =
        JS.Inline(
            "(function(url,body,onOk,onError){var options={method:'POST',cache:'no-store',headers:{'content-type':'application/json'},body:body};globalThis.fetch(url,options).then(function(response){return response.text().then(function(text){if(response.ok){onOk(JSON.parse(text||'{}'));}else{onError(text||('POST '+url+' '+response.status));}});})['catch'](function(error){onError(String(error&&error.message?error.message:error));});})($0,$1,$2,$3)",
            url,
            body,
            onOk,
            onError)

    let optionNode value label =
        let node = doc.CreateElement "option" :?> HTMLOptionElement
        node.Value <- value
        node.TextContent <- label
        node

    let select testId options defaultValue =
        let node = doc.CreateElement "select" :?> HTMLSelectElement
        node.ClassName <- "codexfs-ai-select"
        setStyle "width:100%;height:32px;box-sizing:border-box;" node |> ignore
        setTestId testId node |> ignore

        options
        |> Array.iter (fun (value, label) -> node.AppendChild(optionNode value label) |> ignore)

        setElementValue node defaultValue
        node

    let input inputType testId placeholder value =
        let node = doc.CreateElement "input" :?> HTMLInputElement
        node.Type <- inputType
        node.ClassName <- "codexfs-ai-input"
        node.Placeholder <- placeholder
        node.Value <- value
        setStyle "width:100%;height:34px;box-sizing:border-box;" node |> ignore
        setTestId testId node |> ignore
        node

    let textarea testId placeholder value =
        let node = doc.CreateElement "textarea" :?> HTMLTextAreaElement
        node.ClassName <- "codexfs-ai-prompt"
        node.Placeholder <- placeholder
        node.Value <- value
        setStyle "grid-column:1 / -1;width:100%;height:96px;min-height:96px;box-sizing:border-box;resize:vertical;" node
        |> ignore
        setTestId testId node |> ignore
        node

    let button className testId label =
        let node = doc.CreateElement "button" :?> HTMLButtonElement
        node.Type <- "button"
        node.ClassName <- className
        node.TextContent <- label
        setStyle "position:static;transform:none;margin:0;width:96px;height:36px;min-height:36px;max-height:36px;box-sizing:border-box;align-self:start;display:inline-flex;align-items:center;justify-content:center;flex:0 0 96px;" node
        |> ignore
        setTestId testId node |> ignore
        node

    let field labelText (control: Node) =
        let wrapper =
            element "label" "codexfs-ai-field" null
            |> setStyle "display:flex;flex-direction:column;gap:4px;min-width:0;font-size:12px;line-height:1.25;"

        let caption = element "span" "codexfs-ai-label" labelText
        setStyle "display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;" caption
        |> ignore
        append wrapper [| caption :> Node; control |] |> ignore
        wrapper

    let valueBetweenMarkerAndNextSeparator (marker: string) (text: string) =
        let source = asText text
        let index = source.IndexOf(marker)

        if index < 0 then
            ""
        else
            let start = index + marker.Length
            let next = source.IndexOf("; ", start)
            let value =
                if next < 0 then
                    source.Substring start
                else
                    source.Substring(start, next - start)

            value.Trim()

    let valueAfterMarker (marker: string) (text: string) =
        let source = asText text
        let index = source.IndexOf(marker)

        if index < 0 then
            ""
        else
            source.Substring(index + marker.Length).Trim()

    let normalizedArtifactReplySource (text: string) =
        let source = asText text |> _.Trim()

        if source.StartsWith("run ") then
            source, valueAfterMarker "summary=" source
        else
            let marker = "[codex.fs run "
            let markerIndex = source.IndexOf(marker)

            if markerIndex < 0 then
                source, ""
            else
                let markerEnd = source.IndexOf("]", markerIndex)
                let summary = source.Substring(0, markerIndex).Trim()

                let commandText =
                    if markerEnd < 0 then
                        source.Substring(markerIndex + marker.Length).Trim().TrimEnd([| ']' |])
                    else
                        source.Substring(markerIndex + marker.Length, markerEnd - markerIndex - marker.Length).Trim()

                let command = "run " + commandText

                command, summary

    let parseArtifactReply (text: string) =
        let source, projectedSummary = normalizedArtifactReplySource text

        if not (source.StartsWith("run ")) then
            None
        else
            let headerEnd = source.IndexOf(";")
            let header =
                if headerEnd < 0 then
                    source
                else
                    source.Substring(0, headerEnd)

            let parts = header.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
            let manifestPath = valueBetweenMarkerAndNextSeparator "manifest=" source

            if parts.Length < 3 || isBlank manifestPath then
                None
            else
                Some
                    { runId = parts[1]
                      outcome = parts[2]
                      manifestPath = manifestPath
                      finalPath = valueBetweenMarkerAndNextSeparator "final=" source
                      stdoutPath = valueBetweenMarkerAndNextSeparator "stdout=" source
                      stderrPath = valueBetweenMarkerAndNextSeparator "stderr=" source
                      notePath = valueBetweenMarkerAndNextSeparator "note=" source
                      summary =
                        if isBlank projectedSummary then
                            valueAfterMarker "summary=" source
                        else
                            projectedSummary }

    let artifactRow testId labelText value =
        let row =
            element "div" "codexfs-artifact-row" null
            |> setStyle "display:grid;grid-template-columns:74px minmax(0,1fr);gap:8px;align-items:start;min-width:0;"
            |> setTestId testId

        let labelNode =
            element "span" "codexfs-artifact-label" labelText
            |> setStyle "font-weight:600;color:#3d4852;white-space:nowrap;"

        let valueNode =
            element "code" "codexfs-artifact-value" (asText value)
            |> setStyle "font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:12px;line-height:1.35;white-space:normal;overflow-wrap:anywhere;color:#172033;"

        valueNode.SetAttribute("title", asText value)
        append row [| labelNode :> Node; valueNode :> Node |] |> ignore
        row

    let compactButton className testId label =
        let node = doc.CreateElement "button" :?> HTMLButtonElement
        node.Type <- "button"
        node.ClassName <- className
        node.TextContent <- label
        setStyle "height:30px;min-height:30px;box-sizing:border-box;border:1px solid #9fb2c8;border-radius:6px;background:#f7fafc;color:#172033;padding:0 10px;font-size:12px;line-height:1;font-weight:600;cursor:pointer;display:inline-flex;align-items:center;justify-content:center;white-space:nowrap;" node
        |> ignore
        setTestId testId node |> ignore
        node

    let readArtifactText path onOk onError =
        if isBlank path then
            onError "Artifact path is blank."
        else
            let request: ArtifactReadRequestDto =
                { path = asText path
                  maxBytes = artifactReadMaxBytes }

            postJson
                artifactReadEndpoint
                (JSON.Stringify request)
                (fun data ->
                    let status = JS.Inline<string>("String(($0&&($0.status||$0.Status))||'')", data)
                    let text = JS.Inline<string>("String(($0&&($0.text||$0.Text))||'')", data)
                    let error = JS.Inline<string>("String(($0&&($0.error||$0.Error))||'')", data)
                    let truncated = JS.Inline<bool>("!!($0&&($0.truncated||$0.Truncated))", data)
                    let bytes = JS.Inline<int>("Number(($0&&($0.bytes||$0.Bytes))||0)", data)

                    if sameTextInvariant status "ok" then
                        onOk text truncated bytes
                    else
                        onError (if isBlank error then "Artifact read failed." else error))
                onError

    let removeExistingStdioPanels () =
        JS.Inline(
            "(function(){Array.prototype.slice.call(document.querySelectorAll('[data-testid=\"codexfs-stdio-panel\"]')).forEach(function(node){if(node&&node.parentNode){node.parentNode.removeChild(node);}});})()")

    let enablePanelDrag (panel: Element) (handle: Element) =
        JS.Inline(
            "(function(panel,handle){var dragging=false,startX=0,startY=0,startLeft=0,startTop=0;function down(e){if(e.button!==0){return;}var r=panel.getBoundingClientRect();dragging=true;startX=e.clientX;startY=e.clientY;startLeft=r.left;startTop=r.top;panel.style.left=r.left+'px';panel.style.top=r.top+'px';panel.style.right='auto';panel.style.bottom='auto';e.preventDefault();}function move(e){if(!dragging){return;}var maxLeft=Math.max(0,window.innerWidth-panel.offsetWidth);var maxTop=Math.max(0,window.innerHeight-panel.offsetHeight);panel.style.left=Math.min(Math.max(0,startLeft+e.clientX-startX),maxLeft)+'px';panel.style.top=Math.min(Math.max(0,startTop+e.clientY-startY),maxTop)+'px';}function up(){dragging=false;}handle.addEventListener('mousedown',down);window.addEventListener('mousemove',move);window.addEventListener('mouseup',up);})($0,$1)",
            panel,
            handle)

    let openRunArtifactPanel (reply: ArtifactReplyDto) initialKind =
        removeExistingStdioPanels ()

        let panel =
            element "aside" "codexfs-stdio-panel" null
            |> setStyle "position:fixed;right:24px;bottom:24px;width:min(720px,calc(100vw - 48px));height:min(380px,calc(100vh - 48px));min-width:320px;min-height:190px;max-width:calc(100vw - 24px);max-height:calc(100vh - 24px);display:flex;flex-direction:column;resize:both;overflow:hidden;background:#ffffff;color:#172033;border:1px solid #7e94ad;border-radius:8px;box-shadow:0 18px 50px rgba(15,23,42,0.28);z-index:2147483000;"
            |> setTestId "codexfs-stdio-panel"

        panel.SetAttribute("data-run-id", reply.runId)

        let header =
            element "div" "codexfs-stdio-header" null
            |> setStyle "display:flex;align-items:center;gap:8px;padding:8px 10px;background:#24384f;color:#ffffff;cursor:move;user-select:none;min-height:40px;box-sizing:border-box;"
            |> setTestId "codexfs-stdio-drag-handle"

        let title =
            element "div" "codexfs-stdio-title" ("Run " + reply.runId)
            |> setStyle "flex:1 1 auto;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;font-size:13px;line-height:1.2;font-weight:700;"

        let closeButton = compactButton "codexfs-stdio-close" "codexfs-stdio-close" "Close"
        setStyle "height:28px;min-height:28px;border-color:#c9d6e6;background:#ffffff;color:#172033;padding:0 10px;" closeButton
        |> ignore
        closeButton.AddEventListener("click", Action<Event>(fun _ ->
            JS.Inline("if($0&&$0.parentNode){$0.parentNode.removeChild($0);}", panel)))

        append header [| title :> Node; closeButton :> Node |] |> ignore

        let tabRow =
            element "div" "codexfs-stdio-tabs" null
            |> setStyle "display:flex;gap:6px;align-items:center;padding:8px 10px;border-bottom:1px solid #d8e4f2;background:#f7fafc;box-sizing:border-box;overflow:auto;"

        let state =
            element "div" "codexfs-stdio-state" "No artifact loaded."
            |> setStyle "font-size:12px;line-height:1.35;color:#40546a;padding:7px 10px;border-bottom:1px solid #d8e4f2;min-height:30px;box-sizing:border-box;overflow-wrap:anywhere;"
            |> setTestId "codexfs-stdio-state"

        let content =
            element "pre" "codexfs-stdio-content" ""
            |> setStyle "flex:1 1 auto;margin:0;padding:10px;overflow:auto;background:#fbfdff;color:#172033;font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:12px;line-height:1.45;white-space:pre-wrap;overflow-wrap:anywhere;box-sizing:border-box;"
            |> setTestId "codexfs-stdio-content"

        let setActiveTab (active: HTMLButtonElement) =
            let buttons =
                JS.Inline<HTMLButtonElement[]>("Array.prototype.slice.call($0.querySelectorAll('button'))", tabRow)

            buttons
            |> Array.iter (fun button ->
                if JS.Inline<bool>("$0===$1", button, active) then
                    button.SetAttribute("data-active", "true")
                    JS.Inline("$0.style.background=$1;$0.style.borderColor=$2", button, "#dbeafe", "#6ea8fe")
                else
                    button.RemoveAttribute("data-active")
                    JS.Inline("$0.style.background=$1;$0.style.borderColor=$2", button, "#f7fafc", "#9fb2c8"))

        let loadArtifact label path activeButton =
            setActiveTab activeButton
            state.TextContent <- "Loading " + label + "..."
            content.TextContent <- ""

            readArtifactText
                path
                (fun text truncated bytes ->
                    let suffix =
                        if truncated then
                            $" ({bytes} bytes; showing first {artifactReadMaxBytes} bytes)"
                        else
                            $" ({bytes} bytes)"

                    state.TextContent <- label + suffix
                    content.TextContent <- if isBlank text then "(empty artifact)" else text)
                (fun error ->
                    state.TextContent <- "Artifact read failed."
                    content.TextContent <- asText error)

        let artifactTab testId label path =
            let tab = compactButton "codexfs-stdio-tab" testId label

            if isBlank path then
                tab.Disabled <- true
                tab.Title <- label + " artifact path is not available."
                JS.Inline("$0.style.opacity='0.55';$0.style.cursor='not-allowed';", tab)
            else
                tab.AddEventListener("click", Action<Event>(fun event ->
                    event.PreventDefault()
                    loadArtifact label path tab))

            tab

        let finalTab = artifactTab "codexfs-stdio-tab-final" "Final" reply.finalPath
        let stdoutTab = artifactTab "codexfs-stdio-tab-stdout" "Stdout" reply.stdoutPath
        let stderrTab = artifactTab "codexfs-stdio-tab-stderr" "Stderr" reply.stderrPath
        let noteTab = artifactTab "codexfs-stdio-tab-note" "Note" reply.notePath

        append tabRow [| finalTab :> Node; stdoutTab :> Node; stderrTab :> Node; noteTab :> Node |]
        |> ignore

        append panel [| header :> Node; tabRow :> Node; state :> Node; content :> Node |] |> ignore
        doc.Body.AppendChild panel |> ignore
        enablePanelDrag panel header

        match asText initialKind with
        | "stderr" when not (isBlank reply.stderrPath) -> loadArtifact "Stderr" reply.stderrPath stderrTab
        | "final" when not (isBlank reply.finalPath) -> loadArtifact "Final" reply.finalPath finalTab
        | "note" when not (isBlank reply.notePath) -> loadArtifact "Note" reply.notePath noteTab
        | _ when not (isBlank reply.stdoutPath) -> loadArtifact "Stdout" reply.stdoutPath stdoutTab
        | _ when not (isBlank reply.finalPath) -> loadArtifact "Final" reply.finalPath finalTab
        | _ -> content.TextContent <- "No artifact path is available for this run."

    let renderArtifactReply (text: string) =
        match parseArtifactReply text with
        | None -> None
        | Some reply ->
            let root =
                element "section" "codexfs-artifact-reply message-body" null
                |> setStyle "display:flex;flex-direction:column;gap:8px;box-sizing:border-box;width:100%;white-space:normal;background:#ffffff;border:1px solid #c8d7ea;border-radius:6px;padding:10px 12px;color:#172033;max-height:none;overflow:visible;"
                |> setTestId "codexfs-artifact-reply"

            root.SetAttribute("data-run-id", reply.runId)
            root.SetAttribute("data-outcome", reply.outcome)
            root.SetAttribute("data-manifest-path", reply.manifestPath)
            root.SetAttribute("data-final-path", reply.finalPath)
            root.SetAttribute("data-stdout-path", reply.stdoutPath)
            root.SetAttribute("data-stderr-path", reply.stderrPath)
            root.SetAttribute("data-note-path", reply.notePath)

            let replyText =
                if isBlank reply.summary then
                    $"run {reply.runId} {reply.outcome}"
                else
                    reply.summary

            let message =
                element "div" "codexfs-artifact-summary codexfs-artifact-reply-text" (asText replyText)
                |> setStyle "font-size:13px;line-height:1.45;overflow-wrap:anywhere;white-space:pre-wrap;"
                |> setTestId "codexfs-artifact-summary"

            let actionRow =
                element "div" "codexfs-artifact-actions" null
                |> setStyle "display:flex;flex-wrap:wrap;gap:8px;align-items:center;"
                |> setTestId "codexfs-artifact-actions"

            let stdioButton = compactButton "codexfs-artifact-stdio-open" "codexfs-stdio-open" "Open stdio"
            stdioButton.AddEventListener("click", Action<Event>(fun event ->
                event.PreventDefault()
                openRunArtifactPanel reply "stdout"))
            append actionRow [| stdioButton :> Node |] |> ignore

            let details =
                element "details" "codexfs-artifact-details" null
                |> setStyle "border-top:1px solid #d8e4f2;padding-top:7px;"
                |> setTestId "codexfs-artifact-details"

            let detailsSummary =
                element "summary" "codexfs-artifact-details-toggle" $"Run artifacts ({reply.outcome})"
                |> setStyle "cursor:pointer;font-size:12px;line-height:1.35;color:#40546a;font-weight:600;user-select:none;"
                |> setTestId "codexfs-artifact-details-toggle"

            let refs =
                element "div" "codexfs-artifact-refs" null
                |> setStyle "display:flex;flex-direction:column;gap:7px;margin-top:8px;"
                |> setTestId "codexfs-artifact-refs"

            append
                refs
                [| artifactRow "codexfs-artifact-run" "run" reply.runId :> Node
                   artifactRow "codexfs-artifact-outcome" "outcome" reply.outcome :> Node
                   artifactRow "codexfs-artifact-manifest" "manifest" reply.manifestPath :> Node
                   artifactRow "codexfs-artifact-final" "final" reply.finalPath :> Node
                   artifactRow "codexfs-artifact-stdout" "stdout" reply.stdoutPath :> Node
                   artifactRow "codexfs-artifact-stderr" "stderr" reply.stderrPath :> Node
                   artifactRow "codexfs-artifact-note" "note" reply.notePath :> Node |]
            |> ignore

            append details [| detailsSummary :> Node; refs :> Node |] |> ignore

            append
                root
                [| message :> Node
                   actionRow :> Node
                   details :> Node |]
            |> ignore

            Some(root :> Node)

    let latestReplyMessage (targetParticipantId: string) (data: obj) =
        JS.Inline<obj>(
            "(function(data,target){var messages=(data&&data.messages)||[];var expected=String(target||'').toLowerCase();for(var i=messages.length-1;i>=0;i--){var m=messages[i]||{};if(String(m.fromId||'').toLowerCase()===expected){return m;}}return null;})($0,$1)",
            data,
            targetParticipantId)

    let projectedReplyFromMessage (message: obj) =
        { messageId = JS.Inline<string>("String(($0&&$0.messageId)||'')", message)
          fromId = JS.Inline<string>("String(($0&&$0.fromId)||'')", message)
          body = JS.Inline<string>("String(($0&&$0.body)||'')", message)
          createdAtUtc = JS.Inline<string>("String(($0&&$0.createdAtUtc)||'')", message) }

    let tryLatestProjectedReply targetParticipantId onOk onError =
        let url =
            "/chat/api/thread?participantId="
            + urlEncode aiIntentBridgeParticipantId
            + "&peerId="
            + urlEncode targetParticipantId

        getJson
            url
            (fun data ->
                let message = latestReplyMessage targetParticipantId data

                if isNull (box message) then
                    onOk None
                else
                    onOk(Some(projectedReplyFromMessage message)))
            onError

    let targetScope mode =
        match asText mode with
        | "public" -> "public"
        | "group" -> "group"
        | _ -> "direct"

    let targetParticipantId mode value =
        match asText mode with
        | "foreman" -> "agent.codexfs.foreman"
        | "participant" -> asText value |> _.Trim()
        | _ -> ""

    let targetGroupId mode value =
        if sameTextInvariant mode "group" then
            asText value |> _.Trim()
        else
            ""

    let perspectivePolicy mode =
        if sameTextInvariant mode "participant-readonly" then "read-only" else "current-user"

    let promptFromExistingValue (valueText: string) =
        let valueText = asText valueText

        if valueText.Length > 0 && valueText[0] = '{' then
            ""
        else
            valueText

    let buildIntentJson
        targetMode
        targetValue
        perspectiveMode
        perspectiveValue
        engine
        model
        reasoning
        invocationMode
        approval
        prompt
        =
        let intent: AiChatIntentDto =
            { schema = Package.intentSchema
              target =
                { mode = targetMode
                  scope = targetScope targetMode
                  participantId = targetParticipantId targetMode targetValue
                  groupId = targetGroupId targetMode targetValue }
              perspective =
                { mode = perspectiveMode
                  participantId =
                    if sameTextInvariant perspectiveMode "participant-readonly" then
                        asText perspectiveValue |> _.Trim()
                    else
                        ""
                  senderPolicy = perspectivePolicy perspectiveMode }
              engine =
                { engine = engine
                  model = model
                  reasoning = reasoning }
              invocation =
                { mode = invocationMode
                  approval = approval }
              body = prompt
              tags = [| "codex.fs"; "ai-chat"; "intent" |] }

        JSON.Stringify intent

    let renderAppendInput (ctx: obj) =
        let context = ctx |> As<AppendInputContextDto>

        if not (sameTextInvariant context.shape "codexfs-ai-chat") then
            None
        else
            let root = element "div" "codexfs-ai-controls" null |> setTestId "codexfs-ai-controls"
            setStyle
                "display:flex;flex-direction:column;gap:10px;width:100%;box-sizing:border-box;min-height:0;overflow:auto;background:#fff;position:relative;z-index:3;padding:8px 0;"
                root
            |> ignore
            root.SetAttribute("data-intent-schema", Package.intentSchema)
            root.SetAttribute("data-metadata-schema", Package.metadataSchema)

            let fieldsGrid =
                element "div" "codexfs-ai-fields" null
                |> setStyle "display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px 12px;align-items:end;width:100%;box-sizing:border-box;min-height:0;"

            let targetMode =
                select
                    "codexfs-ai-target-mode"
                    [| "foreman", "Foreman"
                       "participant", "Worker"
                       "public", "Public"
                       "group", "Group" |]
                    "foreman"

            let targetValue =
                input "text" "codexfs-ai-target-value" "agent.* participant or group id" ""

            let perspectiveMode =
                select
                    "codexfs-ai-perspective-mode"
                    [| "self", "My view"
                       "participant-readonly", "Participant view" |]
                    "self"

            let perspectiveValue =
                input "text" "codexfs-ai-perspective-value" "participant id for read-only perspective" ""

            let engine =
                select "codexfs-ai-engine" [| "codex", "Codex"; "agy", "Agy" |] "codex"

            let model =
                select "codexfs-ai-model" [| "default", "Default" |] "default"

            let reasoning =
                select "codexfs-ai-reasoning" [| "medium", "Medium"; "high", "High"; "xhigh", "XHigh" |] "medium"

            let invocationMode =
                select "codexfs-ai-invocation-mode" [| "exec", "Exec"; "print", "Print" |] "exec"

            let approval =
                select "codexfs-ai-approval" [| "never", "Never"; "on-request", "On request" |] "never"

            let prompt =
                textarea "codexfs-ai-prompt" "Prompt to send through PTCS MessageFabric" (promptFromExistingValue context.valueText)

            let status = element "div" "codexfs-ai-status" "" |> setTestId "codexfs-ai-status"
            setStyle "min-height:36px;display:flex;align-items:center;font-size:13px;line-height:1.35;min-width:0;overflow-wrap:anywhere;" status
            |> ignore
            let send = button "primary codexfs-ai-send" "codexfs-ai-send" "Send"

            let actionRow =
                element "div" "codexfs-ai-action-row" null
                |> setStyle "grid-column:1 / -1;display:flex;align-items:flex-start;gap:12px;min-height:40px;width:100%;box-sizing:border-box;"

            append actionRow [| send :> Node; status :> Node |] |> ignore

            let output =
                element "section" "codexfs-ai-output" null
                |> setStyle "grid-column:1 / -1;display:flex;flex-direction:column;gap:6px;min-height:42px;max-height:240px;overflow:auto;border:1px solid #c8d7ea;border-radius:6px;background:#f8fbff;padding:8px 10px;box-sizing:border-box;margin-top:0;"
                |> setTestId "codexfs-ai-output"

            let outputState =
                element "div" "codexfs-ai-output-state" "No output yet."
                |> setStyle "font-size:13px;line-height:1.35;color:#40546a;"
                |> setTestId "codexfs-ai-output-state"

            let outputThread =
                element "div" "codexfs-ai-output-thread" ""
                |> setStyle "font-size:12px;line-height:1.35;color:#64748b;overflow-wrap:anywhere;"
                |> setTestId "codexfs-ai-output-thread"

            let outputMessage =
                element "div" "codexfs-ai-output-message" ""
                |> setStyle "font-size:13px;line-height:1.45;color:#172033;overflow-wrap:anywhere;"
                |> setTestId "codexfs-ai-output-message"

            append output [| outputState :> Node; outputThread :> Node; outputMessage :> Node |]
            |> ignore

            let mutable outputPollHandle: obj = null

            let liveElement testId (fallback: Element) =
                let selector = "[data-testid='" + asText testId + "']"
                let current =
                    JS.Inline<Element>(
                        "(function(selector){var nodes=Array.prototype.slice.call(document.querySelectorAll(selector)).filter(function(node){return node&&node.isConnected;});return nodes.length===0?null:nodes[nodes.length-1];})($0)",
                        selector)

                if isNull (box current) then fallback else current

            let currentOutputState () =
                liveElement "codexfs-ai-output-state" outputState

            let currentOutputThread () =
                liveElement "codexfs-ai-output-thread" outputThread

            let currentOutputMessage () =
                liveElement "codexfs-ai-output-message" outputMessage

            let setOutputState text =
                (currentOutputState()).TextContent <- asText text

            let setOutputThread text =
                (currentOutputThread()).TextContent <- asText text

            let stopOutputPolling () =
                if not (isNull outputPollHandle) then
                    JS.Inline("clearInterval($0)", outputPollHandle)
                    outputPollHandle <- null

            let renderPlainOutput text =
                let node =
                    element "pre" "codexfs-ai-output-plain" (asText text)
                    |> setStyle "margin:0;white-space:pre-wrap;overflow-wrap:anywhere;font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:13px;line-height:1.45;"

                node

            let setOutputMessage body =
                let target = currentOutputMessage ()
                clearChildren target

                match renderArtifactReply body with
                | Some rendered -> target.AppendChild rendered |> ignore
                | None -> target.AppendChild(renderPlainOutput body) |> ignore

            let startOutputProjection targetParticipantId baselineReplyId =
                stopOutputPolling ()
                let mutable attempts = 0
                setOutputThread (aiIntentBridgeParticipantId + " <-> " + targetParticipantId)

                let poll () =
                    attempts <- attempts + 1
                    setOutputThread (aiIntentBridgeParticipantId + " <-> " + targetParticipantId)
                    setOutputState "Waiting for runtime reply..."

                    tryLatestProjectedReply
                        targetParticipantId
                        (fun reply ->
                            match reply with
                            | Some latest when not (isBlank latest.messageId) && not (sameTextInvariant latest.messageId baselineReplyId) ->
                                stopOutputPolling ()
                                setOutputState "Runtime reply received."
                                setOutputMessage latest.body
                            | _ when attempts >= 120 ->
                                stopOutputPolling ()
                                setOutputState "Timed out waiting for runtime reply. Check the Chat tab or artifact logs."
                            | _ -> ())
                        (fun error ->
                            stopOutputPolling ()
                            setOutputState ("Output projection failed: " + asText error))

                poll ()
                outputPollHandle <- JS.Window.SetInterval((fun () -> poll ()), 2000)

            let submitWithOutputProjection targetParticipantId submitAction =
                if isBlank targetParticipantId then
                    setOutputThread "Projection for public/group target is not implemented in this slice."
                    setOutputState "Intent submitted; open the Chat tab for public/group replies."
                    submitAction ()
                else
                    setOutputThread (aiIntentBridgeParticipantId + " <-> " + targetParticipantId)
                    setOutputState "Submitting intent and preparing output projection..."
                    clearChildren (currentOutputMessage ())

                    tryLatestProjectedReply
                        targetParticipantId
                        (fun baseline ->
                            let baselineReplyId =
                                match baseline with
                                | Some reply -> reply.messageId
                                | None -> ""

                            submitAction ()
                            startOutputProjection targetParticipantId baselineReplyId)
                        (fun _ ->
                            submitAction ()
                            startOutputProjection targetParticipantId "")

            let updateTargetPlaceholder () =
                match elementValue targetMode with
                | "participant" -> targetValue.Placeholder <- "agent.codexfs.worker..."
                | "group" -> targetValue.Placeholder <- "codexfs.session.<id>"
                | _ -> targetValue.Placeholder <- "not required"

            targetMode.AddEventListener("change", Action<Event>(fun _ -> updateTargetPlaceholder ()))
            updateTargetPlaceholder ()

            send.AddEventListener(
                "click",
                Action<Event>(fun _ ->
                    let promptText = asText prompt.Value |> _.Trim()
                    let targetText = asText targetValue.Value |> _.Trim()
                    let perspectiveText = asText perspectiveValue.Value |> _.Trim()

                    if isBlank promptText then
                        status.TextContent <- "Prompt is required."
                    elif sameTextInvariant (elementValue targetMode) "participant" && isBlank targetText then
                        status.TextContent <- "Worker participant id is required."
                    elif sameTextInvariant (elementValue targetMode) "group" && isBlank targetText then
                        status.TextContent <- "Group id is required."
                    elif sameTextInvariant (elementValue perspectiveMode) "participant-readonly" && isBlank perspectiveText then
                        status.TextContent <- "Perspective participant id is required."
                    else
                        let valueText =
                            buildIntentJson
                                (elementValue targetMode)
                                targetText
                                (elementValue perspectiveMode)
                                perspectiveText
                                (elementValue engine)
                                (elementValue model)
                                (elementValue reasoning)
                                (elementValue invocationMode)
                                (elementValue approval)
                                promptText

                        let projectionTargetParticipantId =
                            targetParticipantId (elementValue targetMode) targetText

                        let submissionKeyJson =
                            if not (isBlank context.selectedKeyJson) then
                                context.selectedKeyJson
                            elif not (isBlank projectionTargetParticipantId) then
                                JSON.Stringify [| projectionTargetParticipantId |]
                            else
                                context.selectedKeyJson

                        let payload: AiChatSubmitPayloadDto =
                            { valueText = valueText
                              keyJson = submissionKeyJson }

                        submitWithOutputProjection
                            projectionTargetParticipantId
                            (fun () ->
                                context.submit(box payload)
                                status.TextContent <- "Intent submitted; waiting for output.")))

            append
                fieldsGrid
                [| field "Target" (targetMode :> Node) :> Node
                   field "Target id" (targetValue :> Node) :> Node
                   field "Perspective" (perspectiveMode :> Node) :> Node
                   field "Perspective id" (perspectiveValue :> Node) :> Node
                   field "Engine" (engine :> Node) :> Node
                   field "Model" (model :> Node) :> Node
                   field "Reasoning" (reasoning :> Node) :> Node
                   field "Invocation" (invocationMode :> Node) :> Node
                   field "Approval" (approval :> Node) :> Node |]
            |> ignore

            append
                root
                [| fieldsGrid :> Node
                   prompt :> Node
                   actionRow :> Node
                   output :> Node |]
            |> ignore

            Some(root :> Node)

    let registerAppendInputRenderer name priority renderer =
        let register = JS.Global?PulseTradeRegisterAppendInputRenderer
        if JS.TypeOf register = JS.Kind.Function then
            JS.Inline("window.PulseTradeRegisterAppendInputRenderer($0, $1, $2)", name, priority, renderer)

    let registerMessageRenderer name priority renderer =
        let register = JS.Global?PulseTradeRegisterRenderer
        if JS.TypeOf register = JS.Kind.Function then
            JS.Inline("window.PulseTradeRegisterRenderer($0, $1, $2)", name, priority, renderer)

    let enhanceMessageBody (node: Element) =
        if isNull (box node) then
            ()
        else
            node.SetAttribute("data-codexfs-artifact-scanned", "1")

            match renderArtifactReply node.TextContent with
            | None -> ()
            | Some rendered ->
                let parent = node.ParentNode

                if not (isNull (box parent)) then
                    parent.ReplaceChild(rendered, node) |> ignore

    let enhanceExistingMessageBodies () =
        let nodes =
            JS.Inline<Element[]>("Array.prototype.slice.call(document.querySelectorAll('pre.message-body:not([data-codexfs-artifact-scanned])'))")

        nodes |> Array.iter enhanceMessageBody

    let register () =
        JS.Inline("window[$0] = true", loadedMarkerName)
        registerAppendInputRenderer "codexfs-ai-chat-append-input" 120 renderAppendInput
        registerMessageRenderer "codexfs-artifact-reply" 140 renderArtifactReply
        enhanceExistingMessageBodies ()
        JS.Window.SetInterval((fun () -> enhanceExistingMessageBodies ()), 1000) |> ignore

    /// WebSharper bundle entrypoint. Registers PTCS append-page controls for codex.fs AI chat.
    [<SPAEntryPoint>]
    let Main () =
        register ()
