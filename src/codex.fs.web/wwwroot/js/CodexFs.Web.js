import Runtime from "./WebSharper.Core.JavaScript/Runtime.js"
Runtime.ScriptBasePath="/Scripts/";
import { MarkResizable, Lazy, Create } from "./WebSharper.Core.JavaScript/Runtime.js"
function isIDisposable(x){
  return"Dispose"in x;
}
function Main(){
  register();
}
function register(){
  globalThis.CodexFsAiChatLoaded=true;
  registerAppendInputRenderer("codexfs-ai-chat-append-input", 120, renderAppendInput);
  registerMessageRenderer("codexfs-artifact-reply", 140, renderArtifactReply);
  enhanceExistingMessageBodies();
  globalThis.setInterval(enhanceExistingMessageBodies, 1000);
}
function registerAppendInputRenderer(name, priority, renderer){
  if(Equals(typeof globalThis.PulseTradeRegisterAppendInputRenderer, "function"))globalThis.PulseTradeRegisterAppendInputRenderer(name, priority, renderer);
}
function renderAppendInput(ctx){
  if(!sameTextInvariant(ctx.shape, "codexfs-ai-chat"))return null;
  else {
    const root=setTestId("codexfs-ai-controls", element("div", "codexfs-ai-controls", null));
    setStyle("display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px 12px;align-items:end;width:100%;box-sizing:border-box;min-height:0;overflow:auto;background:#fff;position:relative;z-index:3;padding:8px 0;", root);
    root.setAttribute("data-intent-schema", "codex.fs.web.ai-intent.v1");
    root.setAttribute("data-metadata-schema", "codex.fs.web.ai-chat.v1");
    const targetMode=select("codexfs-ai-target-mode", [["foreman", "Foreman"], ["participant", "Worker"], ["public", "Public"], ["group", "Group"]], "foreman");
    const targetValue=input("text", "codexfs-ai-target-value", "agent.* participant or group id", "");
    const perspectiveMode=select("codexfs-ai-perspective-mode", [["self", "My view"], ["participant-readonly", "Participant view"]], "self");
    const perspectiveValue=input("text", "codexfs-ai-perspective-value", "participant id for read-only perspective", "");
    const engine=select("codexfs-ai-engine", [["agy", "Agy"], ["codex", "Codex"]], "agy");
    const model=select("codexfs-ai-model", [["default", "Default"]], "default");
    const reasoning=select("codexfs-ai-reasoning", [["medium", "Medium"], ["high", "High"], ["xhigh", "XHigh"]], "high");
    const invocationMode=select("codexfs-ai-invocation-mode", [["exec", "Exec"], ["print", "Print"]], "exec");
    const approval=select("codexfs-ai-approval", [["never", "Never"], ["on-request", "On request"]], "never");
    const prompt=textarea("codexfs-ai-prompt", "Prompt to send through PTCS MessageFabric", promptFromExistingValue(ctx.valueText));
    const status=setTestId("codexfs-ai-status", element("div", "codexfs-ai-status", ""));
    setStyle("grid-column:2 / -1;min-height:36px;display:flex;align-items:center;font-size:13px;line-height:1.35;", status);
    const send=button("primary codexfs-ai-send", "codexfs-ai-send", "Send");
    const updateTargetPlaceholder=() => {
      const m=elementValue(targetMode);
      if(m=="participant")targetValue.placeholder="agent.codexfs.worker...";
      else if(m=="group")targetValue.placeholder="codexfs.session.<id>";
      else targetValue.placeholder="not required";
    };
    targetMode.addEventListener("change", () => updateTargetPlaceholder());
    updateTargetPlaceholder();
    send.addEventListener("click", () => {
      const promptText=Trim(asText(prompt.value));
      const targetText=Trim(asText(targetValue.value));
      const perspectiveText=Trim(asText(perspectiveValue.value));
      return isBlank(promptText)?void(status.textContent="Prompt is required."):sameTextInvariant(elementValue(targetMode), "participant")&&isBlank(targetText)?void(status.textContent="Worker participant id is required."):sameTextInvariant(elementValue(targetMode), "group")&&isBlank(targetText)?void(status.textContent="Group id is required."):sameTextInvariant(elementValue(perspectiveMode), "participant-readonly")&&isBlank(perspectiveText)?void(status.textContent="Perspective participant id is required."):(ctx.submit(New_29(buildIntentJson(elementValue(targetMode), targetText, elementValue(perspectiveMode), perspectiveText, elementValue(engine), elementValue(model), elementValue(reasoning), elementValue(invocationMode), elementValue(approval), promptText), ctx.selectedKeyJson)),void(status.textContent="Intent submitted."));
    });
    append(root, [field("Target", targetMode), field("Target id", targetValue), field("Perspective", perspectiveMode), field("Perspective id", perspectiveValue), field("Engine", engine), field("Model", model), field("Reasoning", reasoning), field("Invocation", invocationMode), field("Approval", approval), prompt, send, status]);
    return Some(root);
  }
}
function registerMessageRenderer(name, priority, renderer){
  if(Equals(typeof globalThis.PulseTradeRegisterRenderer, "function"))globalThis.PulseTradeRegisterRenderer(name, priority, renderer);
}
function renderArtifactReply(text){
  const m=parseArtifactReply(text);
  if(m!=null&&m.$==1){
    const reply=m.$0;
    const root=setTestId("codexfs-artifact-reply", setStyle("display:flex;flex-direction:column;gap:7px;box-sizing:border-box;width:100%;white-space:normal;background:#f7fbff;border:1px solid #b8d7ef;border-radius:6px;padding:10px 12px;color:#172033;max-height:none;overflow:visible;", element("section", "codexfs-artifact-reply message-body", null)));
    root.setAttribute("data-run-id", reply.runId);
    root.setAttribute("data-outcome", reply.outcome);
    root.setAttribute("data-manifest-path", reply.manifestPath);
    root.setAttribute("data-final-path", reply.finalPath);
    root.setAttribute("data-note-path", reply.notePath);
    const header=setStyle("display:flex;flex-wrap:wrap;gap:8px;align-items:baseline;font-weight:700;", element("div", "codexfs-artifact-header", null));
    append(header, [element("span", "", "codex.fs artifact refs")]);
    append(root, [header, setTestId("codexfs-artifact-summary", setStyle("font-size:13px;line-height:1.45;overflow-wrap:anywhere;", element("div", "codexfs-artifact-summary", asText(reply.summary)))), artifactRow("codexfs-artifact-run", "run", reply.runId), artifactRow("codexfs-artifact-outcome", "outcome", reply.outcome), artifactRow("codexfs-artifact-manifest", "manifest", reply.manifestPath), artifactRow("codexfs-artifact-final", "final", reply.finalPath), artifactRow("codexfs-artifact-note", "note", reply.notePath)]);
    return Some(root);
  }
  else return null;
}
function enhanceExistingMessageBodies(){
  iter((n) => {
    enhanceMessageBody(n);
  }, Array.prototype.slice.call(document.querySelectorAll("pre.message-body:not([data-codexfs-artifact-scanned])")));
}
function sameTextInvariant(left, right){
  return asText(left).toLowerCase()==asText(right).toLowerCase();
}
function element(tag, className, textValue){
  const node=doc().createElement(tag);
  if(!isBlank(className))node.className=className;
  if(!(textValue==null))node.textContent=textValue;
  return node;
}
function setTestId(id, node){
  !isBlank(id)?node.setAttribute("data-testid", id):void 0;
  return node;
}
function setStyle(cssText, node){
  node.setAttribute("style", cssText);
  return node;
}
function select(testId, options, defaultValue){
  const node=doc().createElement("select");
  node.className="codexfs-ai-select";
  setStyle("width:100%;height:32px;box-sizing:border-box;", node);
  setTestId(testId, node);
  iter((_1) => {
    node.appendChild(optionNode(_1[0], _1[1]));
  }, options);
  setElementValue(node, defaultValue);
  return node;
}
function input(inputType, testId, placeholder, value){
  const node=doc().createElement("input");
  node.type=inputType;
  node.className="codexfs-ai-input";
  node.placeholder=placeholder;
  node.value=value;
  setStyle("width:100%;height:34px;box-sizing:border-box;", node);
  setTestId(testId, node);
  return node;
}
function textarea(testId, placeholder, value){
  const node=doc().createElement("textarea");
  node.className="codexfs-ai-prompt";
  node.placeholder=placeholder;
  node.value=value;
  setStyle("grid-column:1 / -1;width:100%;height:96px;min-height:96px;box-sizing:border-box;resize:vertical;", node);
  setTestId(testId, node);
  return node;
}
function promptFromExistingValue(valueText){
  const valueText_1=asText(valueText);
  return valueText_1.length>0&&valueText_1[0]==="{"?"":valueText_1;
}
function button(className, testId, label){
  const node=doc().createElement("button");
  node.type="button";
  node.className=className;
  node.textContent=label;
  setStyle("width:96px;height:36px;min-height:36px;max-height:36px;box-sizing:border-box;align-self:start;", node);
  setTestId(testId, node);
  return node;
}
function elementValue(node){
  return node.value;
}
function asText(value){
  return value==null||Equals(typeof value, "undefined")?"":value;
}
function isBlank(value){
  return IsNullOrWhiteSpace(asText(value));
}
function buildIntentJson(targetMode, targetValue, perspectiveMode, perspectiveValue, engine, model, reasoning, invocationMode, approval, prompt){
  return JSON.stringify(New_36("codex.fs.web.ai-intent.v1", New_37(targetMode, targetScope(targetMode), targetParticipantId(targetMode, targetValue), targetGroupId(targetMode, targetValue)), New_38(perspectiveMode, sameTextInvariant(perspectiveMode, "participant-readonly")?Trim(asText(perspectiveValue)):"", perspectivePolicy(perspectiveMode)), New_39(engine, model, reasoning), New_40(invocationMode, approval), prompt, ["codex.fs", "ai-chat", "intent"]));
}
function append(parent, children){
  iter((child) => {
    parent.appendChild(child);
  }, children);
  return parent;
}
function field(labelText, control){
  const wrapper=setStyle("display:flex;flex-direction:column;gap:4px;min-width:0;font-size:12px;line-height:1.25;", element("label", "codexfs-ai-field", null));
  const caption=element("span", "codexfs-ai-label", labelText);
  setStyle("display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;", caption);
  append(wrapper, [caption, control]);
  return wrapper;
}
function parseArtifactReply(text){
  const source=Trim(asText(text));
  if(!StartsWith(source, "run "))return null;
  else {
    const headerEnd=source.indexOf(";");
    const parts=SplitChars(headerEnd<0?source:Substring(source, 0, headerEnd), [" "], 1);
    const manifestPath=valueBetweenMarkerAndNextSeparator("manifest=", source);
    return length(parts)<3||isBlank(manifestPath)?null:Some(New_30(get(parts, 1), get(parts, 2), manifestPath, valueBetweenMarkerAndNextSeparator("final=", source), valueBetweenMarkerAndNextSeparator("note=", source), valueAfterMarker("summary=", source)));
  }
}
function artifactRow(testId, labelText, value){
  const row=setTestId(testId, setStyle("display:grid;grid-template-columns:74px minmax(0,1fr);gap:8px;align-items:start;min-width:0;", element("div", "codexfs-artifact-row", null)));
  const labelNode=setStyle("font-weight:600;color:#3d4852;white-space:nowrap;", element("span", "codexfs-artifact-label", labelText));
  const valueNode=setStyle("font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:12px;line-height:1.35;white-space:normal;overflow-wrap:anywhere;color:#172033;", element("code", "codexfs-artifact-value", asText(value)));
  valueNode.setAttribute("title", asText(value));
  append(row, [labelNode, valueNode]);
  return row;
}
function enhanceMessageBody(node){
  if(node==null){ }
  else {
    node.setAttribute("data-codexfs-artifact-scanned", "1");
    const m=renderArtifactReply(node.textContent);
    if(m!=null&&m.$==1){
      const rendered=m.$0;
      const parent=node.parentNode;
      if(!(parent==null))parent.replaceChild(rendered, node);
    }
    else void 0;
  }
}
function doc(){
  return _c_1.doc;
}
function optionNode(value, label){
  const node=doc().createElement("option");
  node.value=value;
  node.textContent=label;
  return node;
}
function setElementValue(node, value){
  node.value=value;
}
function targetScope(mode){
  const m=asText(mode);
  return m=="public"?"public":m=="group"?"group":"direct";
}
function targetParticipantId(mode, value){
  const m=asText(mode);
  return m=="foreman"?"agent.codexfs.foreman":m=="participant"?Trim(asText(value)):"";
}
function targetGroupId(mode, value){
  return sameTextInvariant(mode, "group")?Trim(asText(value)):"";
}
function perspectivePolicy(mode){
  return sameTextInvariant(mode, "participant-readonly")?"read-only":"current-user";
}
function valueBetweenMarkerAndNextSeparator(marker, text){
  const source=asText(text);
  const index=source.indexOf(marker);
  if(index<0)return"";
  else {
    const start=index+marker.length;
    const next=source.indexOf("; ", start);
    return Trim(next<0?source.substring(start):Substring(source, start, next-start));
  }
}
function valueAfterMarker(marker, text){
  const source=asText(text);
  const index=source.indexOf(marker);
  return index<0?"":Trim(source.substring(index+marker.length));
}
function Main_1(){
  let mountedPageElement, mountedAppendPageResolved, mounted, appendRegistryWsState, appendRegistryPageCount, appendRegistryMaxSequence, appendRegistrySocket, queuedAppendRegistryFrames, appendRegistrySubscribed, appendRegistryTailRequested;
  const loginRoot=doc_1().getElementById("ptcs-login-root");
  if(!(loginRoot==null))mountLogin(loginRoot);
  else {
    if(!(doc_1().body==null))doc_1().body.setAttribute("data-server-reality-id", currentServerRealityId());
    const trimmed=TrimEnd(asText_1(globalThis.location.pathname), ["/"]);
    const path=isBlank_1(trimmed)?"/chat":trimmed;
    mountedPageElement=null;
    mountedAppendPageResolved=false;
    const cacheKey_1=appendPagesDefinitionsCacheKey();
    mounted=false;
    appendRegistryWsState="idle";
    appendRegistryPageCount=0;
    appendRegistryMaxSequence=0n;
    const mountOnce=(pages) => {
      if(!mounted){
        let _1;
        mounted=true;
        const pages_1=arrayOrEmpty(pages);
        const p=shell(path, pages_1);
        const page=p[1];
        mountedPageElement=Some(page);
        mountedAppendPageResolved=false;
        setMain(p[0]);
        if(path=="/sets")_1=mountSets(page);
        else if(path=="/actors")_1=mountActors(page);
        else if(path=="/chat")_1=mountChat(page);
        else {
          const m=findAppendPage(path, pages_1);
          if(m==null)_1=mountUnknownPage(page, path);
          else {
            const definition=m.$0;
            _1=(mountedAppendPageResolved=true,mountAppendPage(page, definition));
          }
        }
        globalThis.setInterval(() => refreshAppendNav(path), 5000);
      }
    };
    const renderAppendRegistryHealth=() => {
      if(!(doc_1().body==null))doc_1().body.setAttribute("data-append-registry-ws-state", appendRegistryWsState);
      const node=doc_1().querySelector("[data-testid='append-registry-health']");
      if(!(node==null)){
        const x=setData("ws-state", appendRegistryWsState, node);
        const x_1=setData("page-count", String(appendRegistryPageCount), x);
        let _1=setData("max-sequence", String(appendRegistryMaxSequence), x_1);
        setData("cache-key", cacheKey_1, _1);
        node.setAttribute("title", "cacheKey="+String(cacheKey_1)+"\nwsState="+String(appendRegistryWsState)+"\npageCount="+String(appendRegistryPageCount)+"\nmaxSequence="+String(appendRegistryMaxSequence));
        node.textContent="append registry ws "+String(appendRegistryWsState)+" | pages "+String(appendRegistryPageCount)+" | seq "+String(appendRegistryMaxSequence);
      }
      else void 0;
    };
    const applyDefinitionsFromReply=(data) => {
      let _1;
      const data_1=data==null?emptyAppendPagesReply():data;
      appendRegistryPageCount=arrayOrEmpty(data_1.pages).length;
      const b=data_1.maxSequence;
      appendRegistryMaxSequence=Compare(appendRegistryMaxSequence, b)===1?appendRegistryMaxSequence:b;
      if(mounted){
        const nav=doc_1().getElementById("ptc-nav");
        if(!(nav==null))renderNav(nav, path, arrayOrEmpty(data_1.pages));
        if(path!="/sets"&&path!="/actors"&&path!="/chat"&&!mountedAppendPageResolved){
          const _2=findAppendPage(path, arrayOrEmpty(data_1.pages));
          if(mountedPageElement!=null&&mountedPageElement.$==1){
            if(_2!=null&&_2.$==1){
              const definition=_2.$0;
              const page=mountedPageElement.$0;
              _1=(clear(page),mountedAppendPageResolved=true,mountAppendPage(page, definition));
            }
            else _1=void 0;
          }
          else _1=void 0;
        }
        else _1=void 0;
      }
      else _1=mountOnce(data_1.pages);
      renderAppendRegistryHealth();
    };
    const setAppendRegistryWsState=(value) => {
      appendRegistryWsState=asText_1(value);
      renderAppendRegistryHealth();
    };
    appendRegistrySocket=null;
    queuedAppendRegistryFrames=[];
    appendRegistrySubscribed=false;
    appendRegistryTailRequested=false;
    const handleAppendRegistryEvents=(events) => {
      if(length(arrayOrEmpty(events))>0)readJson(cacheKey_1, (cached) => {
        const merged=mergeAppendPageRegistryEvents(cached==null?emptyAppendPagesReply():cached.$0, events);
        writeAppendPagesDefinitions(merged);
        applyDefinitionsFromReply(merged);
      });
    };
    function flushAppendRegistryFrames(socket){
      if(Equals(socket.readyState, 1)){
        const frames=queuedAppendRegistryFrames;
        queuedAppendRegistryFrames=[];
        iter((frame) => {
          socket.send(frame);
        }, frames);
      }
    }
    function ensureAppendRegistrySocket(){
      let _1, _2;
      if(appendRegistrySocket!=null&&appendRegistrySocket.$==1){
        const socket=appendRegistrySocket.$0;
        _1=(Equals(socket.readyState, 1)||Equals(socket.readyState, 0))&&(_2=appendRegistrySocket.$0,true);
      }
      else _1=false;
      if(_1)return _2;
      else {
        setAppendRegistryWsState("connecting");
        const socket_1=new WebSocket(syncWebSocketUrl());
        appendRegistrySocket=Some(socket_1);
        socket_1.onopen=() => {
          setAppendRegistryWsState("open");
          return flushAppendRegistryFrames(socket_1);
        };
        socket_1.onmessage=(event) => {
          try {
            const response=json(String(event.data));
            const responseType=asText_1(response.type).toLowerCase();
            const responseStatus=asText_1(response.status).toLowerCase();
            switch(responseStatus=="ok"?responseType=="subscribe"?0:responseType=="stream-event"?1:responseType=="read-tail"?2:responseType=="read"?2:responseType=="tail"?2:4:responseStatus=="error"?3:4){
              case 0:
                return setAppendRegistryWsState("subscribed");
              case 1:
                return handleAppendRegistryEvents([response.event]);
              case 2:
                return handleAppendRegistryEvents(response.events);
              case 3:
                return setAppendRegistryWsState("error");
              case 4:
                return null;
            }
          }
          catch(m){
            return setAppendRegistryWsState("parse-error");
          }
        };
        socket_1.onerror=() => setAppendRegistryWsState("error");
        socket_1.onclose=() => {
          appendRegistrySocket=null;
          appendRegistrySubscribed=false;
          appendRegistryTailRequested=false;
          return setAppendRegistryWsState("closed");
        };
        return socket_1;
      }
    }
    function sendAppendRegistryFrame(frame){
      while(true)
        {
          const socket=ensureAppendRegistrySocket();
          return Equals(socket.readyState, 1)?socket.send(frame):void(queuedAppendRegistryFrames=queuedAppendRegistryFrames.concat([frame]));
        }
    }
    function subscribeAppendPageRegistry(){
      const streamKey=appendPageRegistryStreamKey();
      if(!appendRegistrySubscribed){
        appendRegistrySubscribed=true;
        sendAppendRegistryFrame(JSON.stringify(New_1("subscribe", newRequestId("append-pages-subscribe"), streamKey)));
      }
      if(!appendRegistryTailRequested){
        appendRegistryTailRequested=true;
        sendAppendRegistryFrame(JSON.stringify(New_2("read-tail", newRequestId("append-pages-read-tail"), streamKey, defaultCacheLimit())));
      }
    }
    const startAfterAclSnapshot=() => {
      readJson(cacheKey_1, (a) => {
        if(a==null){ }
        else applyDefinitionsFromReply(a.$0);
      });
      getJson("/pages/api/definitions", (data) => {
        writeAppendPagesDefinitions(data);
        applyDefinitionsFromReply(data);
      }, () => {
        mountOnce([]);
        renderAppendRegistryHealth();
      });
      subscribeAppendPageRegistry();
      renderAppendRegistryHealth();
    };
    getJson("/acl/api/snapshot", (snapshot) => {
      set_currentAclSnapshotJson(JSON.stringify(snapshot));
      set_currentAclSnapshot(Some(snapshot));
      notifyAclSnapshotObservers(currentAclSnapshotJson());
      startAfterAclSnapshot();
    }, () => {
      startAfterAclSnapshot();
    });
  }
}
function doc_1(){
  return _c.doc;
}
function mountLogin(root){
  if(!tryMountLoginWithRegisteredRenderers(root, JSON.stringify(loginConfig())))mountLoginFallback(root);
}
function currentServerRealityId(){
  const node=doc_1().getElementById("ptc-comm-reality");
  if(node==null||isBlank_1(node.textContent))return"legacy";
  else try {
    return textOr("legacy", json(node.textContent).serverRealityId);
  }
  catch(m){
    return"legacy";
  }
}
function isBlank_1(value){
  return value==null||Trim(value)=="";
}
function asText_1(value){
  return value==null||Equals(typeof value, "undefined")?"":value;
}
function appendPagesDefinitionsCacheKey(){
  return cacheKey("append-pages-definitions", FSharpList.Empty);
}
function setData(name, value, node){
  !isBlank_1(name)?node.setAttribute("data-"+name, asText_1(value)):void 0;
  return node;
}
function emptyAppendPagesReply(){
  return New("ok", 0, 0n, []);
}
function arrayOrEmpty(values){
  return values==null?[]:values;
}
function mergeAppendPageRegistryEvents(baseline, events){
  let pages, maxSequence;
  const baseline_1=baseline==null?emptyAppendPagesReply():baseline;
  pages=arrayOrEmpty(baseline_1.pages);
  maxSequence=baseline_1.maxSequence;
  iter((event) => {
    if(!(event==null)&&event.sequence>0n){
      const m=asText_1(event.sourceKind).toLowerCase();
      if(m=="append-page.definition"){
        const b=event.sequence;
        maxSequence=Compare(maxSequence, b)===1?maxSequence:b;
        try {
          const o=pageDefinitionFromWire(json(event.payload));
          if(o==null)null;
          else {
            const page=o.$0;
            pages=sortAppendPages(filter((existing) =>!sameTextInvariant_1(existing.pageId, page.pageId), pages).concat([page]));
          }
        }
        catch(m_1){
          null;
        }
      }
      else if(m=="append-page.hidden"){
        const b_1=event.sequence;
        maxSequence=Compare(maxSequence, b_1)===1?maxSequence:b_1;
        try {
          const o_1=hiddenPageFromWire(json(event.payload));
          if(o_1==null)null;
          else {
            const _1=o_1.$0[0];
            const _2=o_1.$0[1];
            pages=sortAppendPages(filter((page_1) =>!(sameTextInvariant_1(page_1.pageId, _1)||sameTextInvariant_1(page_1.tabId, _2)||sameTextInvariant_1(page_1.pageId, _2)||sameTextInvariant_1(page_1.tabId, _1)), pages));
          }
        }
        catch(m_2){
          null;
        }
      }
      else void 0;
    }
  }, arrayOrEmpty(events));
  return New("ok", length(pages), maxSequence, pages);
}
function writeAppendPagesDefinitions(data){
  writeSnapshotWithWatermark(appendPagesDefinitionsCacheKey(), data, data.maxSequence, length(arrayOrEmpty(data.pages)), "append-pages-definitions");
}
function syncWebSocketUrl(){
  const location=globalThis.location;
  return(location.protocol=="https:"?"wss:":"ws:")+"//"+location.host+"/sync/ws";
}
function appendPageRegistryStreamKey(){
  return New_4("__append-page-registry", "append-page-registry", "__append-pages", ["__append-pages"]);
}
function newRequestId(prefix){
  set_requestSeq(requestSeq()+1);
  return prefix+"-"+String(requestSeq())+"-"+String(Math.floor(Math.random()*1000000000));
}
function defaultCacheLimit(){
  return _c.defaultCacheLimit;
}
function getJson(url, onOk, onError){
  const options=requestOptions();
  options.cache="no-store";
  (globalThis.fetch(url, options).then((response) => response.text().then((body) => response.ok?onOk(json(isBlank_1(body)?"{}":body)):onError(isBlank_1(body)?"GET "+String(url)+" "+String(response.status):body))))["catch"]((error) => onError(errorMessage(error)));
}
function set_currentAclSnapshotJson(_1){
  _c.currentAclSnapshotJson=_1;
}
function set_currentAclSnapshot(_1){
  _c.currentAclSnapshot=_1;
}
function notifyAclSnapshotObservers(snapshotJson){
  let r;
  const _1=snapshotJson;
  if(!(globalThis.PulseTrade&&globalThis.PulseTrade.AclSnapshotObservers))void 0;
  let observers=globalThis.PulseTrade.AclSnapshotObservers;
  for(let i=0;i<observers.length;i++){
    let r_1=observers[i];
    try {
      (r_1.render||r_1[1])(_1);
    }
    catch(e){
      console.error("ACL snapshot observer exception:", e);
    }
  }
}
function currentAclSnapshotJson(){
  return _c.currentAclSnapshotJson;
}
function findAppendPage(path, pages){
  return tryFind((page) => isCurrentPage(path, pagePath(page))||isCurrentPage(path, "/page/"+asText_1(page.pageId))||isCurrentPage(path, "/"+asText_1(page.pageId)), arrayOrEmpty(pages));
}
function clear(node){
  node.textContent="";
}
function mountAppendPage(page, definition){
  let currentLineageHealth, selected, selectedKeyJson, buckets, locallyHiddenKeyIds, pendingSelectKeyId, loadGeneration, visibleValueLimit, scrollValuesToBottomAfterNextRender, addKeyEditorOpen, addKeyMode, ensureSelectedSubscription, replayPendingCommands, deleteAcceptedPendingAppends, rerenderAppendForm, rerenderAddKeyBuilder, currentKeyMaxSequence, keyRegistryWsState, syncSocket, queuedSyncFrames, subscribedValueStream, keyRegistrySubscribed, keyRegistryTailRequested, pendingWsAppendIds, syncRepairScheduled, repairSyncAfterClose, replayingPending;
  page.className="page append-page";
  setData("tab-id", definition.tabId, setData("page-id", definition.pageId, setTestId_1("append-page-"+asText_1(definition.pageId), page)));
  const sameText=(left, right) => asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
  const readsLegacy=sameText(definition.tabId, definition.pageId);
  let currentLineage=New_5(definition.tabId, readsLegacy?"default":"fresh", readsLegacy?definition.pageId:"", readsLegacy, readsLegacy?"read-current-tab-and-legacy-page-streams":"read-current-tab-stream-only");
  const applyLineage=(lineage) => {
    const lineage_1=lineage==null?currentLineage:lineage;
    currentLineage=lineage_1;
    setData("lineage-read-repair-policy", lineage_1.readRepairPolicy, setData("lineage-reads-legacy", lineage_1.readsLegacyPageStreams?"true":"false", setData("lineage-legacy-page-id-alias", lineage_1.legacyPageIdAlias, setData("lineage-kind", lineage_1.lineageKind, setData("lineage-stream-page-id", lineage_1.streamPageId, page)))));
  };
  applyLineage(currentLineage);
  const defaultLineageHealth=() => New_6(currentLineage.streamPageId, currentLineage.lineageKind, currentLineage.legacyPageIdAlias, currentLineage.readsLegacyPageStreams, currentLineage.readRepairPolicy, [], 0, [], 0);
  currentLineageHealth=defaultLineageHealth();
  selected="";
  selectedKeyJson="";
  buckets=[];
  locallyHiddenKeyIds=[];
  pendingSelectKeyId="";
  loadGeneration=0;
  visibleValueLimit=defaultRenderLimit();
  scrollValuesToBottomAfterNextRender=false;
  addKeyEditorOpen=false;
  addKeyMode="target";
  const isLocallyHiddenKeyId=(keyId) =>!isBlank_1(keyId)&&exists((hidden) => sameText(hidden, keyId), locallyHiddenKeyIds);
  const rememberLocallyHiddenKeyId=(keyId) => {
    if(!isBlank_1(keyId)&&!isLocallyHiddenKeyId(keyId))locallyHiddenKeyIds=locallyHiddenKeyIds.concat([keyId]);
  };
  const side=element_1("aside", "sidebar append-sidebar", null);
  const sideHead=element_1("div", "panel-head", null);
  const sideActions=element_1("div", "head-actions", null);
  const addActorKeyButton=setTestId_1("append-add-actor-key", button_1("", "Add actor key"));
  const addKeyButton=setTestId_1("append-add-key", button_1("", "Add target key"));
  const addProxyKeyButton=setTestId_1("append-add-proxy-key", button_1("", "Add proxy key"));
  const removeKeyButton=setTestId_1("append-remove-key", button_1("", "Remove"));
  const removePageButton=setTestId_1("append-remove-page", button_1("", "Remove page"));
  const reload=setTestId_1("append-reload", button_1("", "Reload"));
  const actionPool=setTestId_1("append-page-actions", element_1("details", "append-page-actions", null));
  const actionSummary=setTestId_1("append-page-actions-summary", element_1("summary", "append-page-actions-summary", "Actions"));
  const actionMenu=setTestId_1("append-page-actions-menu", element_1("div", "append-page-actions-menu", null));
  const filters=element_1("div", "filters", null);
  const keyFilter=setTestId_1("append-key-filter", input_1("key contains"));
  const newKeyInput=setTestId_1("append-key-input", input_1(textOr("\"Aster\"", definition.keyPlaceholder)));
  const newKeyAliasInput=setTestId_1("append-key-alias-input", input_1("target alias (optional)"));
  const addKeyPanel=setTestId_1("append-add-key-panel", element_1("div", "append-add-key-panel", null));
  const fallbackAddKeyPanel=setTestId_1("append-add-key-fallback", element_1("div", "append-add-key-fallback", null));
  const fallbackAddKeyActions=setTestId_1("append-add-key-actions", element_1("div", "append-add-key-actions", null));
  const cleanKeyButton=setTestId_1("append-key-clean", button_1("", "Clean"));
  const cancelKeyButton=setTestId_1("append-key-cancel", button_1("", "Cancel"));
  const okKeyButton=setTestId_1("append-key-ok", button_1("primary", "OK"));
  const addKeyRendererHost=setData("renderer-state", "not-rendered", setTestId_1("append-add-key-renderer-host", element_1("div", "append-add-key-renderer-host", null)));
  const status=setTestId_1("append-key-status", element_1("div", "state", "Loading"));
  const list=setTestId_1("append-key-list", element_1("div", "list", null));
  const work=setTestId_1("append-work", element_1("section", "append-work", null));
  const values=setTestId_1("append-values", element_1("div", "append-values", null));
  const form=setTestId_1("append-form", element_1("div", "append-form", null));
  const valueInput=setTestId_1("append-value-input", textarea_1("append-value-input", textOr("JSON value", definition.valuePlaceholder)));
  const directionInput=setTestId_1("append-direction", input_1("outbound-message"));
  const appendButton=setTestId_1("append-submit", button_1("primary", "Append"));
  const canAddKey=pageAclAllows(definition.pageId, "ptcs.target-key.add");
  const canRemoveKey=pageAclAllows(definition.pageId, "ptcs.target-key.remove");
  const canRemovePage=pageAclAllows(definition.pageId, "ptcs.page.remove");
  const canAppendValue=isActorArguPage(definition)?pageAclAllows(definition.pageId, "ptcs.actor-argu.send"):pageAclAllows(definition.pageId, "ptcs.append.write");
  setHidden(!canAddKey, addActorKeyButton);
  setHidden(!canAddKey, addKeyButton);
  setHidden(!canAddKey, addProxyKeyButton);
  setHidden(!canRemoveKey, removeKeyButton);
  setHidden(!canRemovePage, removePageButton);
  setHidden(!canAppendValue, appendButton);
  const head_1=element_1("div", "work-head", null);
  const titleBox=element_1("div", "", null);
  const workState=setTestId_1("append-work-status", element_1("div", "state", "Loading"));
  const pendingState=setTestId_1("append-pending-state", element_1("div", "state pending-state", ""));
  const lineageHealthBox=setTestId_1("append-lineage-health", element_1("div", "meta wrap lineage-health", null));
  const lineageDetailBox=setTestId_1("append-lineage-detail", element_1("div", "lineage-detail", null));
  const lineageDetailPolicy=setTestId_1("append-lineage-detail-policy", element_1("span", "lineage-detail-value", ""));
  const lineageDetailStream=setTestId_1("append-lineage-detail-stream", element_1("span", "lineage-detail-value", ""));
  const lineageDetailLegacy=setTestId_1("append-lineage-detail-legacy", element_1("span", "lineage-detail-value", ""));
  const lineageDetailValueCount=setTestId_1("append-lineage-detail-value-count", element_1("span", "lineage-detail-value", ""));
  const lineageDetailValueStreams=setTestId_1("append-lineage-detail-value-streams", element_1("pre", "lineage-detail-value lineage-streams", ""));
  const lineageDetailKeyCount=setTestId_1("append-lineage-detail-key-count", element_1("span", "lineage-detail-value", ""));
  const lineageDetailKeyStreams=setTestId_1("append-lineage-detail-key-streams", element_1("pre", "lineage-detail-value lineage-streams", ""));
  const keyRegistryHealthBox=setTestId_1("append-key-registry-health", element_1("div", "meta wrap key-registry-health", null));
  const browserCacheHealthBox=setTestId_1("append-browser-cache-health", element_1("div", "meta wrap browser-cache-health", null));
  const lineageInfo=setTestId_1("append-lineage-info", element_1("details", "lineage-info", null));
  const lineageSummary=setTestId_1("append-lineage-toggle", element_1("summary", "lineage-summary", "Tab info"));
  const lineageInfoContent=setTestId_1("append-lineage-info-content", element_1("div", "lineage-info-content", null));
  const identityBox=setTestId_1("append-page-identity", element_1("div", "page-identity", null));
  const pageIdChip=setTestId_1("append-page-id", element_1("span", "identity-chip", "page "+asText_1(definition.pageId)));
  const tabIdChip=setTestId_1("append-tab-id", element_1("span", "identity-chip", "tab "+asText_1(definition.tabId)));
  const sideTitle=element_1("div", "panel-title", null);
  const lineageDetailRow=(label, valueNode) => {
    const row=element_1("div", "lineage-detail-row", null);
    append_1(row, [element_1("span", "lineage-detail-label", label), valueNode]);
    return row;
  };
  append_1(lineageDetailBox, [lineageDetailRow("policy", lineageDetailPolicy), lineageDetailRow("stream", lineageDetailStream), lineageDetailRow("legacy", lineageDetailLegacy), lineageDetailRow("value count", lineageDetailValueCount), lineageDetailRow("value streams", lineageDetailValueStreams), lineageDetailRow("key count", lineageDetailKeyCount), lineageDetailRow("key streams", lineageDetailKeyStreams)]);
  append_1(lineageInfoContent, [lineageHealthBox, lineageDetailBox, keyRegistryHealthBox, browserCacheHealthBox]);
  append_1(lineageInfo, [lineageSummary, lineageInfoContent]);
  newKeyInput.value=asText_1(definition.defaultKey);
  directionInput.value="outbound-message";
  directionInput.className="append-direction";
  appendButton.textContent=actorArguButtonLabel(definition);
  append_1(identityBox, [pageIdChip, tabIdChip]);
  append_1(sideTitle, [element_1("h1", "", pageTitle(definition)), identityBox]);
  append_1(actionMenu, isActorDynamicPage(definition)?[addActorKeyButton, addKeyButton, addProxyKeyButton, removeKeyButton, reload, removePageButton]:isActorArguPage(definition)?[addKeyButton, removeKeyButton, reload, removePageButton]:(addKeyButton.textContent="Add key",[addKeyButton, removeKeyButton, reload, removePageButton]));
  append_1(actionPool, [actionSummary, actionMenu]);
  append_1(sideActions, [actionPool]);
  append_1(sideHead, [sideTitle]);
  append_1(fallbackAddKeyActions, [cleanKeyButton, cancelKeyButton, okKeyButton]);
  append_1(fallbackAddKeyPanel, [newKeyInput, newKeyAliasInput, fallbackAddKeyActions]);
  append_1(addKeyPanel, [fallbackAddKeyPanel, addKeyRendererHost]);
  append_1(filters, [addKeyPanel, keyFilter, status]);
  append_1(side, [sideHead, sideActions, filters, list]);
  append_1(titleBox, [setTestId_1("append-page-type-label", element_1("label", "", pageTypeLabel(definition)+" / "+asText_1(definition.setName))), element_1("h2", "", pageTitle(definition)), element_1("div", "meta wrap", asText_1(definition.description)), lineageInfo]);
  append_1(head_1, [titleBox, workState]);
  const applyLineageHealth=(health) => {
    const health_1=health==null?defaultLineageHealth():health;
    currentLineageHealth=health_1;
    const valueStreamKeys=health_1.candidateValueStreamKeys==null?"":concat_1("\n", map(asText_1, health_1.candidateValueStreamKeys));
    const keyRegistryStreamKeys=health_1.candidateKeyRegistryStreamKeys==null?"":concat_1("\n", map(asText_1, health_1.candidateKeyRegistryStreamKeys));
    const visibleStreamKeys=(streamKeys) => isBlank_1(streamKeys)?"none":streamKeys;
    setData("lineage-health-policy", health_1.readRepairPolicy, setData("lineage-candidate-key-registry-stream-keys", keyRegistryStreamKeys, setData("lineage-candidate-value-stream-keys", valueStreamKeys, setData("lineage-candidate-key-registry-stream-count", String(health_1.candidateKeyRegistryStreamCount), setData("lineage-candidate-value-stream-count", String(health_1.candidateValueStreamCount), page)))));
    setData("read-repair-policy", health_1.readRepairPolicy, setData("candidate-key-registry-stream-keys", keyRegistryStreamKeys, setData("candidate-value-stream-keys", valueStreamKeys, setData("candidate-key-registry-stream-count", String(health_1.candidateKeyRegistryStreamCount), setData("candidate-value-stream-count", String(health_1.candidateValueStreamCount), setData("lineage-kind", health_1.lineageKind, setData("stream-page-id", health_1.streamPageId, lineageHealthBox)))))));
    lineageHealthBox.setAttribute("title", "value streams:\n"+valueStreamKeys+"\nkey registry streams:\n"+keyRegistryStreamKeys);
    lineageHealthBox.textContent="lineage "+String(asText_1(health_1.lineageKind))+" | stream "+String(asText_1(health_1.streamPageId))+" | value streams "+String(health_1.candidateValueStreamCount)+" | key streams "+String(health_1.candidateKeyRegistryStreamCount)+" | "+String(asText_1(health_1.readRepairPolicy));
    setData("read-repair-policy", health_1.readRepairPolicy, setData("candidate-key-registry-stream-keys", keyRegistryStreamKeys, setData("candidate-value-stream-keys", valueStreamKeys, setData("candidate-key-registry-stream-count", String(health_1.candidateKeyRegistryStreamCount), setData("candidate-value-stream-count", String(health_1.candidateValueStreamCount), setData("reads-legacy", health_1.readsLegacyPageStreams?"true":"false", setData("legacy-page-id-alias", health_1.legacyPageIdAlias, setData("lineage-kind", health_1.lineageKind, setData("stream-page-id", health_1.streamPageId, lineageDetailBox)))))))));
    lineageDetailPolicy.textContent=asText_1(health_1.readRepairPolicy);
    lineageDetailStream.textContent=asText_1(health_1.streamPageId);
    lineageDetailLegacy.textContent=isBlank_1(health_1.legacyPageIdAlias)?"none":asText_1(health_1.legacyPageIdAlias);
    lineageDetailValueCount.textContent=String(health_1.candidateValueStreamCount);
    lineageDetailValueStreams.textContent=visibleStreamKeys(valueStreamKeys);
    lineageDetailKeyCount.textContent=String(health_1.candidateKeyRegistryStreamCount);
    lineageDetailKeyStreams.textContent=visibleStreamKeys(keyRegistryStreamKeys);
  };
  applyLineageHealth(currentLineageHealth);
  if(isActorArguPage(definition)){
    form.className="append-form actor-argu-form";
    append_1(form, [valueInput, appendButton]);
  }
  else asText_1(definition.shape).toLowerCase()=="fcell-chat"?(form.className="append-form chat-form",append_1(form, [directionInput, valueInput, appendButton])):append_1(form, [valueInput, appendButton]);
  append_1(work, [head_1, pendingState, values, form]);
  append_1(page, [side, work]);
  const browserId=currentUserId();
  ensureSelectedSubscription=() => { };
  replayPendingCommands=() => { };
  deleteAcceptedPendingAppends=() =>() => null;
  rerenderAppendForm=() => { };
  rerenderAddKeyBuilder=() => { };
  const refreshPendingState=() => {
    readPendingRealitySplit((_3, _4) => renderPendingInspection(pendingState, filter((command) =>!(command==null)&&(sameText(command.target, definition.pageId)||!isBlank_1(command.payloadJson)&&command.payloadJson.indexOf("\"pageId\":\""+asText_1(definition.pageId)+"\"")!=-1), _3), filter((command) =>!(command==null)&&(sameText(command.target, definition.pageId)||!isBlank_1(command.payloadJson)&&command.payloadJson.indexOf("\"pageId\":\""+asText_1(definition.pageId)+"\"")!=-1), _4)));
  };
  const isPendingForThisPage=(command) =>!(command==null)&&(sameText(command.target, definition.pageId)||!isBlank_1(command.payloadJson)&&command.payloadJson.indexOf("\"pageId\":\""+asText_1(definition.pageId)+"\"")!=-1);
  const currentFilterText=() => isBlank_1(keyFilter.value)?"":Trim(keyFilter.value);
  const requestValuesScrollToBottom=() => {
    scrollValuesToBottomAfterNextRender=true;
  };
  const stateCacheKey=() => cacheKey("append-page-state", ofArray([definition.pageId, definition.tabId, currentFilterText()]));
  const keyRegistryCacheKey=() => cacheKey("append-page-keys", ofArray([definition.pageId, definition.tabId]));
  currentKeyMaxSequence=0n;
  keyRegistryWsState="idle";
  const updateBrowserCacheHealth=(renderedCount, cachedCount, minSequence, maxSequence, snapshotSeqId, backendGap) => {
    const gapText=backendGap?"true":"false";
    const cacheKey_1=stateCacheKey();
    const selectedText=isBlank_1(selected)?"(none)":selected;
    const n=setData("cache-key", cacheKey_1, browserCacheHealthBox);
    let _3=setData("selected-key-id", selected, n);
    let _4=setData("rendered-count", String(renderedCount), _3);
    let _5=setData("cached-count", String(cachedCount), _4);
    let _6=setData("min-sequence", String(minSequence), _5);
    let _7=setData("max-sequence", String(maxSequence), _6);
    let _8=setData("snapshot-seqid", String(snapshotSeqId), _7);
    setData("backend-gap", gapText, _8);
    browserCacheHealthBox.setAttribute("title", "cacheKey="+String(cacheKey_1)+"\nselectedKey="+String(selectedText)+"\nrendered="+String(renderedCount)+"\ncached="+String(cachedCount)+"\nrange="+String(minSequence)+".."+String(maxSequence)+"\nsnapshotSeqId="+String(snapshotSeqId)+"\nbackendGap="+String(gapText));
    browserCacheHealthBox.textContent="browser cache "+String(cacheKey_1)+" | rendered "+String(renderedCount)+" | cached "+String(cachedCount)+" | seq "+String(minSequence)+".."+String(maxSequence)+" | snapshot "+String(snapshotSeqId)+" | gap "+String(gapText);
  };
  const updateKeyRegistryHealth=() => {
    const cacheKey_1=keyRegistryCacheKey();
    const x=setData("ws-state", keyRegistryWsState, keyRegistryHealthBox);
    const x_1=setData("key-count", String(length(buckets)), x);
    let _3=setData("max-sequence", String(currentKeyMaxSequence), x_1);
    setData("cache-key", cacheKey_1, _3);
    keyRegistryHealthBox.setAttribute("title", "cacheKey="+String(cacheKey_1)+"\nwsState="+String(keyRegistryWsState)+"\nvisibleKeyCount="+String(length(buckets))+"\nmaxSequence="+String(currentKeyMaxSequence));
    keyRegistryHealthBox.textContent="key registry ws "+String(keyRegistryWsState)+" | visible keys "+String(length(buckets))+" | seq "+String(currentKeyMaxSequence);
  };
  const writeAppendPageKeyWatermark=(snapshot) => {
    const b=snapshot.keyMaxSequence;
    currentKeyMaxSequence=Compare(currentKeyMaxSequence, b)===1?currentKeyMaxSequence:b;
    writeWatermark(keyRegistryCacheKey(), currentKeyMaxSequence, snapshot.bucketCount, "append-page-keys");
    updateKeyRegistryHealth();
  };
  const writeCurrentSnapshot=() => {
    const snapshot=New_8("ok", definition, length(buckets), fold((_3, _4) => Compare(_3, _4)===1?_3:_4, 0n, map((bucket) => bucket.maxSequence, buckets)), currentKeyMaxSequence, currentLineage, currentLineageHealth, buckets);
    writeSnapshotWithWatermark(stateCacheKey(), snapshot, snapshot.maxSequence, appendPageValueCount(snapshot), "append-page-state");
    writeAppendPageKeyWatermark(snapshot);
  };
  const appendPageKeyId=(keys) => asText_1(definition.setName)+"::"+concat_1(" + ", sortBy((key) => key.toLowerCase(), distinctBy((key) => key.toLowerCase(), choose((key) => {
    const text=Trim(asText_1(key));
    return isBlank_1(text)?null:Some(text);
  }, arrayOrEmpty(keys)))));
  const selectBucketKeys=(keys) => {
    const keys_1=arrayOrEmpty(keys);
    return length(keys_1)>0&&(selected=appendPageKeyId(keys_1),selectedKeyJson=keysAsJson(keys_1),newKeyInput.value=selectedKeyJson,true);
  };
  const sortAppendPageBuckets=(items) => sortBy((bucket) =>[asText_1(bucket.setName), asText_1(bucket.keyId)], arrayOrEmpty(items));
  const sequenceBounds=(items) => {
    let oldest, newest;
    oldest=0n;
    newest=0n;
    iter((value) => {
      !(value==null)&&value.sequence>0n&&(oldest===0n||value.sequence<oldest)?oldest=value.sequence:void 0;
      !(value==null)&&value.sequence>newest?newest=value.sequence:void 0;
    }, arrayOrEmpty(items));
    return[oldest, newest];
  };
  const mergeAppendValues=(existing, incoming) => {
    let merged;
    merged=[];
    const add=(value) => {
      if(!(value==null)&&!isBlank_1(value.valueId)&&!exists((row) => row.valueId==value.valueId, merged))merged=merged.concat([value]);
    };
    iter(add, arrayOrEmpty(incoming));
    iter(add, arrayOrEmpty(existing));
    return sortBy((value) => asText_1(value.createdAtUtc), merged);
  };
  function renderList(){
    clear(list);
    iter((bucket) => {
      const item=button_1(bucket.keyId==selected?"list-card active":"list-card", null);
      const x=setData("key-id", bucket.keyId, setTestId_1("append-key-card", item));
      const x_1=setData("key-display-name", asText_1(bucket.displayName), x);
      let _3=setData("key-json", keysAsJson(bucket.keys), x_1);
      let _4=setData("min-sequence", String(bucket.minSequence), _3);
      setData("max-sequence", String(bucket.maxSequence), _4);
      item.setAttribute("title", joinValues(bucket.keys));
      let _5=item;
      const displayName=Trim(asText_1(bucket.displayName));
      let _6=isBlank_1(displayName)?joinValues(bucket.keys):displayName;
      let _7=element_1("div", "strong wrap", _6);
      let _8=[_7, element_1("div", "muted wrap", asText_1(bucket.setName)), element_1("div", "meta", "values="+String(bucket.valueCount)+" seq="+String(bucket.maxSequence)+" updated="+String(asText_1(bucket.updatedAtUtc)))];
      append_1(_5, _8);
      item.addEventListener("click", () => {
        selected=bucket.keyId;
        selectedKeyJson=keysAsJson(bucket.keys);
        newKeyInput.value=selectedKeyJson;
        visibleValueLimit=defaultRenderLimit();
        renderList();
        requestValuesScrollToBottom();
        renderValues();
        rerenderAppendForm();
        return ensureSelectedSubscription();
      });
      list.appendChild(item);
    }, buckets);
  }
  function renderValues(){
    while(true)
      {
        let _3, _4, _5;
        clear(values);
        const x=(((n) =>(n_1) => setData(n, selected, n_1))("selected-key-id"))(work);
        ((((n) =>(n_1) => setData(n, selectedKeyJson, n_1))("selected-key-json"))(x));
        const bucket=(((p) =>(a_3) => tryFind(p, a_3))((bucket_2) => bucket_2.keyId==selected))(buckets);
        if(bucket!=null&&bucket.$==1){
          const bucket_1=bucket.$0;
          const allValues=arrayOrEmpty(bucket_1.values);
          const visible=latestArray(visibleValueLimit, allValues);
          const a=0;
          const b=length(allValues)-length(visible);
          const hiddenCached=Compare(a, b)===1?a:b;
          const a_1=bucket_1.valueCount;
          const b_1=length(allValues);
          const reportedCount=Compare(a_1, b_1)===1?a_1:b_1;
          const fromValues=(sequenceBounds(allValues))[0];
          const oldestSequence=bucket_1.minSequence>0n?bucket_1.minSequence:fromValues;
          const a_2=bucket_1.maxSequence;
          const b_2=(sequenceBounds(allValues))[1];
          const newestSequence=Compare(a_2, b_2)===1?a_2:b_2;
          const backendGapAvailable=oldestSequence>1n&&hiddenCached===0;
          const x_1=[asText_1(definition.tabId), asText_1(definition.shape), asText_1(definition.setName), concat_1("\u001f", arrayOrEmpty(bucket_1.keys))];
          const selectedValueStreamKey=(((s) =>(s_1) => concat_1(s, s_1))("\n"))(x_1);
          const x_2=(((n, v) =>(n_1) => setData(n, v, n_1))("lineage-candidate-value-stream-count", "1"))(page);
          ((((n, selectedValueStreamKey_1) =>(n_1) => setData(n, selectedValueStreamKey_1, n_1))("lineage-candidate-value-stream-keys", selectedValueStreamKey))(x_2));
          const x_3=(((n, v) =>(n_1) => setData(n, v, n_1))("candidate-value-stream-count", "1"))(lineageHealthBox);
          ((((n, selectedValueStreamKey_1) =>(n_1) => setData(n, selectedValueStreamKey_1, n_1))("candidate-value-stream-keys", selectedValueStreamKey))(x_3));
          const x_4=(((n, v) =>(n_1) => setData(n, v, n_1))("candidate-value-stream-count", "1"))(lineageDetailBox);
          ((((n, selectedValueStreamKey_1) =>(n_1) => setData(n, selectedValueStreamKey_1, n_1))("candidate-value-stream-keys", selectedValueStreamKey))(x_4));
          lineageDetailValueCount.textContent="1";
          lineageDetailValueStreams.textContent=selectedValueStreamKey;
          const x_5=(((n, v) =>(n_1) => setData(n, v, n_1))("rendered-count", String(length(visible))))(values);
          const x_6=(((n, v) =>(n_1) => setData(n, v, n_1))("cached-count", String(length(allValues))))(x_5);
          const x_7=(((n, v) =>(n_1) => setData(n, v, n_1))("oldest-sequence", String(oldestSequence)))(x_6);
          const x_8=(((n, v) =>(n_1) => setData(n, v, n_1))("min-sequence", String(oldestSequence)))(x_7);
          const x_9=(((n, v) =>(n_1) => setData(n, v, n_1))("max-sequence", String(newestSequence)))(x_8);
          const x_10=(((n, v) =>(n_1) => setData(n, v, n_1))("snapshot-seqid", String(newestSequence)))(x_9);
          ((((n, v) =>(n_1) => setData(n, v, n_1))("backend-gap", backendGapAvailable?"true":"false"))(x_10));
          updateBrowserCacheHealth(length(visible), length(allValues), oldestSequence, newestSequence, newestSequence, backendGapAvailable);
          if(length(visible)===0)_3=void values.appendChild(element_1("div", "empty", "No values appended yet."));
          else {
            if(hiddenCached>0){
              const x_11=button_1("", "Load older ("+String(hiddenCached)+")");
              const loadOlder=(((i) =>(n) => setTestId_1(i, n))("append-load-older"))(x_11);
              _4=(loadOlder.addEventListener("click", ((allValues_1) =>() => {
                const a_3=length(allValues_1);
                const b_3=visibleValueLimit+defaultRenderLimit();
                visibleValueLimit=Compare(a_3, b_3)===-1?a_3:b_3;
                return renderValues();
              })(allValues)),void values.appendChild(loadOlder));
            }
            else if(backendGapAvailable){
              const x_12=button_1("", "Load older (backend)");
              const loadOlder_1=(((i) =>(n) => setTestId_1(i, n))("append-load-older"))(x_12);
              _4=(loadOlder_1.addEventListener("click", ((bucket_2, oldestSequence_1) =>() => readOlderFromBackend(bucket_2, oldestSequence_1))(bucket_1, oldestSequence)),void values.appendChild(loadOlder_1));
            }
            else _4=null;
            _3=(((a_3) =>(a_4) => {
              iter(a_3, a_4);
            })((value) => {
              values.appendChild(renderAppendValue(value));
            }))(visible);
          }
          _5=length(visible)<reportedCount?setStatus(workState, "Showing "+String(length(visible))+"/"+String(reportedCount)+" value(s)"):setStatus(workState, String(reportedCount)+" value(s)");
        }
        else {
          const x_13=(((n, v) =>(n_1) => setData(n, v, n_1))("rendered-count", "0"))(values);
          const x_14=(((n, v) =>(n_1) => setData(n, v, n_1))("cached-count", "0"))(x_13);
          const x_15=(((n, v) =>(n_1) => setData(n, v, n_1))("oldest-sequence", "0"))(x_14);
          const x_16=(((n, v) =>(n_1) => setData(n, v, n_1))("min-sequence", "0"))(x_15);
          const x_17=(((n, v) =>(n_1) => setData(n, v, n_1))("max-sequence", "0"))(x_16);
          const x_18=(((n, v) =>(n_1) => setData(n, v, n_1))("snapshot-seqid", "0"))(x_17);
          ((((n, v) =>(n_1) => setData(n, v, n_1))("backend-gap", "false"))(x_18));
          updateBrowserCacheHealth(0, 0, 0n, 0n, 0n, false);
          const x_19=(((n, v) =>(n_1) => setData(n, v, n_1))("lineage-candidate-value-stream-count", "0"))(page);
          ((((n, v) =>(n_1) => setData(n, v, n_1))("lineage-candidate-value-stream-keys", ""))(x_19));
          const x_20=(((n, v) =>(n_1) => setData(n, v, n_1))("candidate-value-stream-count", "0"))(lineageHealthBox);
          ((((n, v) =>(n_1) => setData(n, v, n_1))("candidate-value-stream-keys", ""))(x_20));
          const x_21=(((n, v) =>(n_1) => setData(n, v, n_1))("candidate-value-stream-count", "0"))(lineageDetailBox);
          ((((n, v) =>(n_1) => setData(n, v, n_1))("candidate-value-stream-keys", ""))(x_21));
          lineageDetailValueCount.textContent="0";
          lineageDetailValueStreams.textContent="none";
          values.appendChild(element_1("div", "empty", "No key selected."));
          _5=setStatus(workState, "No key selected");
        }
        if(scrollValuesToBottomAfterNextRender){
          scrollValuesToBottomAfterNextRender=false;
          scrollToBottomAfterRender(values);
        }
        return rerenderAppendForm();
      }
  }
  function readOlderFromBackend(bucket, beforeSequence){
    const keyJson=isBlank_1(selectedKeyJson)?keysAsJson(bucket.keys):selectedKeyJson;
    const url="/pages/api/read-before?pageId="+encodeURIComponent(asText_1(definition.pageId))+"&keyJson="+encodeURIComponent(keyJson)+"&beforeSequence="+String(beforeSequence)+"&count="+String(defaultRenderLimit());
    setStatus(workState, "Loading older values before "+String(beforeSequence));
    return getJson(url, (reply) => {
      applyLineage(reply.lineage);
      applyLineageHealth(reply.lineageHealth);
      const incoming=arrayOrEmpty(reply.values);
      if(length(incoming)===0)setStatus(workState, "No older backend values");
      else {
        updateSelectedBucketWithOlder(incoming);
        setStatus(workState, "Loaded "+String(length(incoming))+" older backend value(s)");
      }
    }, (t) => {
      setStatus(workState, t);
    });
  }
  function updateSelectedBucketWithOlder(incoming){
    let mergedLength;
    mergedLength=0;
    buckets=map((bucket) => {
      if(bucket.keyId==selected){
        const merged=mergeAppendValues(bucket.values, incoming);
        const p=sequenceBounds(merged);
        mergedLength=length(merged);
        const a=bucket.valueCount;
        const b_2=length(merged);
        let _3=Compare(a, b_2)===1?a:b_2;
        const a_1=bucket.maxSequence;
        const b_3=p[1];
        let _4=Compare(a_1, b_3)===1?a_1:b_3;
        return New_9(bucket.keyId, bucket.keys, bucket.displayName, bucket.setName, _3, p[0], _4, bucket.updatedAtUtc, merged);
      }
      else return bucket;
    }, buckets);
    writeCurrentSnapshot();
    const b=visibleValueLimit+length(arrayOrEmpty(incoming));
    const b_1=Compare(mergedLength, b)===-1?mergedLength:b;
    visibleValueLimit=Compare(visibleValueLimit, b_1)===1?visibleValueLimit:b_1;
    renderList();
    renderValues();
  }
  const readNewerFromBackend=(generation, bucket) => {
    const keyJson=keysAsJson(bucket.keys);
    return getJson("/pages/api/read-after?pageId="+encodeURIComponent(asText_1(definition.pageId))+"&keyJson="+encodeURIComponent(keyJson)+"&afterSequence="+String(bucket.maxSequence)+"&count="+String(defaultCacheLimit()), (reply) => {
      if(generation===loadGeneration){
        applyLineage(reply.lineage);
        applyLineageHealth(reply.lineageHealth);
        const incoming=arrayOrEmpty(reply.values);
        if(length(incoming)>0){
          (deleteAcceptedPendingAppends(bucket))(incoming);
          const keyId=reply.keyId;
          buckets=map((bucket_1) => {
            if(bucket_1.keyId==keyId){
              const merged=mergeAppendValues(bucket_1.values, incoming);
              const p=sequenceBounds(merged);
              const minSequence=p[0];
              const a=bucket_1.valueCount;
              const b=length(merged);
              let _3=Compare(a, b)===1?a:b;
              const a_1=bucket_1.maxSequence;
              const b_1=p[1];
              let _4=Compare(a_1, b_1)===1?a_1:b_1;
              return New_9(bucket_1.keyId, bucket_1.keys, bucket_1.displayName, bucket_1.setName, _3, minSequence>0n?minSequence:bucket_1.minSequence, _4, bucket_1.updatedAtUtc, merged);
            }
            else return bucket_1;
          }, buckets);
          writeCurrentSnapshot();
          renderList();
          requestValuesScrollToBottom();
          renderValues();
          setStatus(status, "Synced "+String(length(incoming))+" newer value(s) from backend");
        }
        else setStatus(status, "Cached data is current");
      }
    }, (error) => {
      if(generation===loadGeneration)setStatus(status, "Cached data loaded; tail sync failed: "+error);
    });
  };
  const applySnapshot=(source, data) => {
    let _3;
    applyLineage(data.lineage);
    applyLineageHealth(data.lineageHealth);
    const b=data.keyMaxSequence;
    currentKeyMaxSequence=Compare(currentKeyMaxSequence, b)===1?currentKeyMaxSequence:b;
    buckets=filter((bucket_1) =>!isLocallyHiddenKeyId(bucket_1.keyId), arrayOrEmpty(data.buckets));
    visibleValueLimit=defaultRenderLimit();
    if(isBlank_1(pendingSelectKeyId))_3=false;
    else {
      const m=tryFind((bucket_1) => sameText(bucket_1.keyId, pendingSelectKeyId), buckets);
      if(m==null)_3=false;
      else {
        const bucket=m.$0;
        const selectedPending=bucket==null?false:selectBucketKeys(bucket.keys);
        _3=(selectedPending?pendingSelectKeyId="":void 0,selectedPending);
      }
    }
    if(_3)null;
    else(isBlank_1(selected)||!exists((bucket_1) => bucket_1.keyId==selected, buckets))&&length(buckets)>0?(selected=get(buckets, 0).keyId,selectedKeyJson=keysAsJson(get(buckets, 0).keys),void(newKeyInput.value=selectedKeyJson)):length(buckets)===0?(selected="",void(selectedKeyJson="")):null;
    setStatus(status, "Loaded "+String(length(buckets))+" "+String(source)+" bucket(s)");
    renderList();
    requestValuesScrollToBottom();
    renderValues();
    ensureSelectedSubscription();
    iter((bucket_1) => {
      (deleteAcceptedPendingAppends(bucket_1))(bucket_1.values);
    }, buckets);
    refreshPendingState();
    return sameText(source, "backend")?void setTimeout(() => {
      replayPendingCommands();
    }, 100):null;
  };
  const load=() => {
    let url;
    loadGeneration=loadGeneration+1;
    const generation=loadGeneration;
    const filterText=currentFilterText();
    url="/pages/api/state?pageId="+encodeURIComponent(asText_1(definition.pageId))+"&limit="+String(defaultCacheLimit());
    if(!isBlank_1(filterText))url=url+"&key="+encodeURIComponent(filterText);
    const cacheKey_1=stateCacheKey();
    const fetchFullState=() => {
      getJson(url, (data) => {
        if(generation===loadGeneration){
          writeSnapshotWithWatermark(cacheKey_1, data, data.maxSequence, appendPageValueCount(data), "append-page-state");
          writeAppendPageKeyWatermark(data);
          applySnapshot("backend", data);
        }
      }, (error) => {
        if(generation===loadGeneration){
          setStatus(status, error);
          setStatus(workState, error);
        }
      });
    };
    readJson(cacheKey_1, (a) => {
      if(a==null){
        if(generation===loadGeneration)fetchFullState();
      }
      else if(a.$0,generation===loadGeneration){
        const cached=a.$0;
        applySnapshot("cached", cached);
        const sequenceBuckets=filter((bucket) => bucket.maxSequence>0n, arrayOrEmpty(cached.buckets));
        if(length(sequenceBuckets)===0)fetchFullState();
        else {
          iter((_3) => readNewerFromBackend(generation, _3), sequenceBuckets);
          fetchFullState();
        }
      }
    });
  };
  syncSocket=null;
  queuedSyncFrames=[];
  subscribedValueStream="";
  keyRegistrySubscribed=false;
  keyRegistryTailRequested=false;
  pendingWsAppendIds=[];
  syncRepairScheduled=false;
  repairSyncAfterClose=() => { };
  const setWsState=(value) => {
    setData("ws-state", value, work);
  };
  const setKeyRegistryWsState=(value) => {
    keyRegistryWsState=asText_1(value);
    setData("key-registry-ws-state", value, work);
    updateKeyRegistryHealth();
  };
  const effectiveSelectedKeys=() => {
    const selectedJsonKeys=keysFromJson(selectedKeyJson);
    const m=tryFind((bucket_1) => bucket_1.keyId==selected, buckets);
    if(m==null)return keysFromJson(selectedKeyJson);
    else {
      const bucket=m.$0;
      return length(selectedJsonKeys)>0&&sameText(appendPageKeyId(selectedJsonKeys), bucket.keyId)?(m.$0,selectedJsonKeys):arrayOrEmpty(m.$0.keys);
    }
  };
  const effectiveSelectedKeyJson=() => {
    const selectedJsonKeys=keysFromJson(selectedKeyJson);
    const m=tryFind((bucket_1) => bucket_1.keyId==selected, buckets);
    if(m==null)return selectedKeyJson;
    else {
      const bucket=m.$0;
      return length(selectedJsonKeys)>0&&sameText(appendPageKeyId(selectedJsonKeys), bucket.keyId)?(m.$0,selectedKeyJson):keysAsJson(m.$0.keys);
    }
  };
  const selectedBucket=() => {
    const m=tryFind((bucket_1) => bucket_1.keyId==selected, buckets);
    if(m==null){
      const keys=effectiveSelectedKeys();
      return length(keys)===0?null:Some(New_9(appendPageKeyId(keys), keys, "", definition.setName, 0, 0n, 0n, "", []));
    }
    else {
      const bucket=m.$0;
      const keys_1=effectiveSelectedKeys();
      return Some(New_9(bucket.keyId, length(keys_1)>0?keys_1:bucket.keys, bucket.displayName, bucket.setName, bucket.valueCount, bucket.minSequence, bucket.maxSequence, bucket.updatedAtUtc, bucket.values));
    }
  };
  deleteAcceptedPendingAppends=(bucket) =>(acceptedValues) => {
    const acceptedValues_1=arrayOrEmpty(acceptedValues);
    if(length(acceptedValues_1)>0){
      const keyJson=keysAsJson(bucket.keys);
      const commandMatches=(command) => {
        if(sameText(command.kind, "append-page-append-value")&&sameText(command.url, "/pages/api/append")&&isPendingForThisPage(command)&&!isBlank_1(command.payloadJson))try {
          const x=json(command.payloadJson);
          const _3=command.commandId;
          return sameText(x.pageId, definition.pageId)&&sameText(x.keyJson, keyJson)&&exists((value) => sameText(value.valueId, _3), acceptedValues_1);
        }
        catch(m){
          return false;
        }
        else return false;
      };
      return readAllPending((commands) => {
        let remaining;
        const accepted=filter(commandMatches, commands);
        if(length(accepted)>0){
          remaining=length(accepted);
          const finishOne=() => {
            remaining=remaining-1;
            remaining===0?refreshPendingState():void 0;
          };
          iter((command) => {
            deletePendingThen(command.commandId, finishOne);
          }, accepted);
        }
      });
    }
    else return null;
  };
  const streamKeyFor=(bucket) => New_4(definition.tabId, definition.shape, definition.setName, arrayOrEmpty(bucket.keys));
  const handleSyncEvent=(source, event) => {
    let o, updated, _3, o_1;
    if(!(event==null)){
      const m=asText_1(event.sourceKind).toLowerCase();
      if(m=="append-page.key"||m=="append-page.key-hidden"){
        if(!(event==null)&&event.sequence>0n){
          const m_1=asText_1(event.sourceKind).toLowerCase();
          if(m_1=="append-page.key"){
            if(event==null||isBlank_1(event.payload))o=null;
            else try {
              const wire=json(event.payload);
              if(wire==null||asText_1(wire.schema)!="ptc.comm.spa.append-page.key.v1"||!sameText(wire.pageId, definition.pageId))o=null;
              else {
                const keys=filter((key) =>!isBlank_1(key), map(asText_1, arrayOrEmpty(wire.keys)));
                o=length(keys)===0?null:Some([keys, Trim(asText_1(wire.displayName))]);
              }
            }
            catch(m_3){
              o=null;
            }
            if(o==null)return null;
            else {
              const _4=o.$0[0];
              const _5=o.$0[1];
              const b=event.sequence;
              currentKeyMaxSequence=Compare(currentKeyMaxSequence, b)===1?currentKeyMaxSequence:b;
              const keyId=appendPageKeyId(_4);
              const filterText=currentFilterText();
              if((isBlank_1(filterText)||exists((key) => asText_1(key).toLowerCase().indexOf(filterText.toLowerCase())!=-1, arrayOrEmpty(_4)))&&!isLocallyHiddenKeyId(keyId)){
                const m_2=tryFind((bucket_1) => sameText(bucket_1.keyId, keyId), buckets);
                if(m_2==null)updated=New_9(keyId, _4, _5, definition.setName, 0, 0n, 0n, asText_1(event.createdAtUtc), []);
                else {
                  const existing=m_2.$0;
                  updated=New_9(existing.keyId, _4, textOr(existing.displayName, _5), definition.setName, existing.valueCount, existing.minSequence, existing.maxSequence, textOr(existing.updatedAtUtc, event.createdAtUtc), existing.values);
                }
                _3=(buckets=sortAppendPageBuckets(filter((bucket_1) =>!sameText(bucket_1.keyId, keyId), buckets).concat([updated])),sameText(pendingSelectKeyId, keyId)?selectBucketKeys(_4)?void(pendingSelectKeyId=""):null:isBlank_1(selected)||!exists((bucket_1) => sameText(bucket_1.keyId, selected), buckets)?(selected=keyId,selectedKeyJson=keysAsJson(_4),void(newKeyInput.value=selectedKeyJson)):null);
              }
              else _3=null;
              writeCurrentSnapshot();
              renderList();
              renderValues();
              ensureSelectedSubscription();
              return setStatus(status, "Synced "+String(source)+" key registry");
            }
          }
          else if(m_1=="append-page.key-hidden"){
            if(event==null||isBlank_1(event.payload))o_1=null;
            else try {
              const wire_1=json(event.payload);
              o_1=wire_1==null||asText_1(wire_1.schema)!="ptc.comm.spa.append-page.key-hidden.v1"||!sameText(wire_1.pageId, definition.pageId)||isBlank_1(wire_1.keyId)?null:Some(Trim(wire_1.keyId));
            }
            catch(m_4){
              o_1=null;
            }
            if(o_1==null)return null;
            else {
              const keyId_1=o_1.$0;
              const b_1=event.sequence;
              currentKeyMaxSequence=Compare(currentKeyMaxSequence, b_1)===1?currentKeyMaxSequence:b_1;
              rememberLocallyHiddenKeyId(keyId_1);
              buckets=sortAppendPageBuckets(filter((bucket_1) =>!sameText(bucket_1.keyId, keyId_1), buckets));
              if(sameText(selected, keyId_1))if(length(buckets)>0){
                selected=get(buckets, 0).keyId;
                selectedKeyJson=keysAsJson(get(buckets, 0).keys);
                newKeyInput.value=selectedKeyJson;
              }
              else {
                selected="";
                selectedKeyJson="";
              }
              writeCurrentSnapshot();
              renderList();
              renderValues();
              ensureSelectedSubscription();
              return setStatus(status, "Synced "+String(source)+" key removal");
            }
          }
          else return null;
        }
        else return null;
      }
      else if(!(event==null)&&event.sequence>0n&&!(event.streamKey==null)){
        const eventKeys=arrayOrEmpty(event.streamKey.keys);
        const o_2=tryFind((bucket_1) => {
          const left=arrayOrEmpty(bucket_1.keys);
          const right=arrayOrEmpty(eventKeys);
          return length(left)===length(right)&&forall2(sameText, left, right);
        }, buckets);
        if(o_2==null)return null;
        else {
          const bucket=o_2.$0;
          return bucket.maxSequence>0n?readNewerFromBackend(loadGeneration, bucket):load();
        }
      }
      else return null;
    }
    else return null;
  };
  function flushSyncFrames(socket){
    if(Equals(socket.readyState, 1)){
      const frames=queuedSyncFrames;
      queuedSyncFrames=[];
      iter((frame) => {
        socket.send(frame);
      }, frames);
    }
  }
  function ensureSyncSocket(){
    let _3, _4;
    if(syncSocket!=null&&syncSocket.$==1){
      const socket=syncSocket.$0;
      _3=(Equals(socket.readyState, 1)||Equals(socket.readyState, 0))&&(_4=syncSocket.$0,true);
    }
    else _3=false;
    if(_3)return _4;
    else {
      setWsState("connecting");
      const socket_1=new WebSocket(syncWebSocketUrl());
      syncSocket=Some(socket_1);
      socket_1.onopen=() => {
        setWsState("open");
        return flushSyncFrames(socket_1);
      };
      socket_1.onmessage=(event) => {
        const text=String(event.data);
        try {
          const response=json(text);
          const responseType=asText_1(response.type).toLowerCase();
          const responseStatus=asText_1(response.status).toLowerCase();
          const requestId=asText_1(response.requestId);
          switch(responseStatus=="ok"?responseType=="subscribe"?0:responseType=="append"?1:responseType=="append-page"?1:responseType=="actor-argu"?1:responseType=="stream-event"?2:responseType=="read-tail"?3:responseType=="read"?3:responseType=="tail"?3:5:responseStatus=="error"?4:5){
            case 0:
              return asText_1(response.streamKey).indexOf("append-page-key-registry")!=-1?setKeyRegistryWsState("subscribed"):setWsState("subscribed");
            case 1:
              if(exists((id) => id==requestId, pendingWsAppendIds)){
                pendingWsAppendIds=filter((id) => id!=requestId, pendingWsAppendIds);
                deletePendingThen(requestId, () => {
                  valueInput.value="";
                  refreshPendingState();
                  setStatus(workState, "Appended through WebSocket");
                });
              }
              if(sameText(responseType, "actor-argu"))(((event_1, value) => {
                let keys, matched, _5;
                if(!(value==null)&&!isBlank_1(value.valueId)){
                  const eventKeys=event_1==null||event_1.streamKey==null?[]:arrayOrEmpty(event_1.streamKey.keys);
                  if(length(eventKeys)>0)keys=eventKeys;
                  else {
                    const m=tryFind((bucket_1) => bucket_1.keyId==selected, buckets);
                    keys=m==null?keysFromJson(selectedKeyJson):arrayOrEmpty(m.$0.keys);
                  }
                  if(length(keys)>0){
                    const keyId=appendPageKeyId(keys);
                    const incoming=[value];
                    matched=false;
                    buckets=map((bucket_1) => {
                      if(sameText(bucket_1.keyId, keyId)){
                        matched=true;
                        const merged=mergeAppendValues(bucket_1.values, incoming);
                        const p_1=sequenceBounds(merged);
                        const minSequence=p_1[0];
                        const a=bucket_1.valueCount;
                        const b=length(merged);
                        let _6=Compare(a, b)===1?a:b;
                        const a_1=bucket_1.maxSequence;
                        const b_1=p_1[1];
                        let _7=Compare(a_1, b_1)===1?a_1:b_1;
                        return New_9(bucket_1.keyId, keys, bucket_1.displayName, bucket_1.setName, _6, minSequence>0n?minSequence:bucket_1.minSequence, _7, textOr(bucket_1.updatedAtUtc, value.createdAtUtc), merged);
                      }
                      else return bucket_1;
                    }, buckets);
                    if(!matched){
                      const p=sequenceBounds(incoming);
                      const bucket=New_9(keyId, keys, "", definition.setName, length(incoming), p[0], p[1], asText_1(value.createdAtUtc), incoming);
                      _5=void(buckets=sortAppendPageBuckets(buckets.concat([bucket])));
                    }
                    else _5=null;
                    selected=keyId;
                    selectedKeyJson=keysAsJson(keys);
                    newKeyInput.value=selectedKeyJson;
                    writeCurrentSnapshot();
                    renderList();
                    requestValuesScrollToBottom();
                    return renderValues();
                  }
                  else return null;
                }
                else return null;
              })(response.event, response.value));
              return handleSyncEvent("live", response.event);
            case 2:
              return handleSyncEvent("live", response.event);
            case 3:
              return iter((_5) => handleSyncEvent("tail", _5), arrayOrEmpty(response.events));
            case 4:
              return exists((id) => id==requestId, pendingWsAppendIds)?setStatus(workState, pendingFailure("WebSocket append", asText_1(response.error))):setStatus(status, "WebSocket sync error: "+asText_1(response.error));
            case 5:
              return null;
          }
        }
        catch(error){
          return setStatus(status, "WebSocket sync parse failed: "+errorMessage(error));
        }
      };
      socket_1.onerror=() => {
        setWsState("error");
        return setStatus(status, "WebSocket sync error; pending command remains replayable");
      };
      socket_1.onclose=() => {
        syncSocket=null;
        subscribedValueStream="";
        keyRegistrySubscribed=false;
        keyRegistryTailRequested=false;
        setWsState("closed");
        setKeyRegistryWsState("closed");
        return!syncRepairScheduled?(syncRepairScheduled=true,void setTimeout(() => {
          syncRepairScheduled=false;
          repairSyncAfterClose();
        }, 500)):null;
      };
      return socket_1;
    }
  }
  function sendSyncFrame(frame){
    const socket=ensureSyncSocket();
    if(Equals(socket.readyState, 1))socket.send(frame);
    else queuedSyncFrames=queuedSyncFrames.concat([frame]);
  }
  const subscribeKeyRegistry=() => {
    const streamPageId=textOr(definition.pageId, definition.tabId);
    const streamKey=New_4(streamPageId, "append-page-key-registry", definition.setName, ["__append-page-keys", streamPageId]);
    if(!keyRegistrySubscribed){
      keyRegistrySubscribed=true;
      setKeyRegistryWsState("subscribing");
      sendSyncFrame(JSON.stringify(New_1("subscribe", newRequestId("append-page-keys-subscribe"), streamKey)));
    }
    if(!keyRegistryTailRequested){
      keyRegistryTailRequested=true;
      sendSyncFrame(JSON.stringify(New_2("read-tail", newRequestId("append-page-keys-read-tail"), streamKey, defaultCacheLimit())));
    }
  };
  ensureSelectedSubscription=() => {
    const o=selectedBucket();
    if(o==null)void 0;
    else {
      const streamKey=streamKeyFor(o.$0);
      const identity=concat_1("\n", [asText_1(streamKey.pageId), asText_1(streamKey.mode), asText_1(streamKey.setName), concat_1("\u001f", arrayOrEmpty(streamKey.keys))]);
      if(!isBlank_1(identity)&&identity!=subscribedValueStream){
        subscribedValueStream=identity;
        setWsState("subscribing");
        sendSyncFrame(JSON.stringify(New_1("subscribe", newRequestId("subscribe"), streamKey)));
      }
    }
  };
  repairSyncAfterClose=() => {
    setWsState("repairing");
    refreshPendingState();
    subscribeKeyRegistry();
    const m=selectedBucket();
    if(m==null)load();
    else {
      const bucket=m.$0;
      ensureSelectedSubscription();
      if(bucket.maxSequence>0n)readNewerFromBackend(loadGeneration, bucket);
      else load();
    }
  };
  const closeAddKeyEditor=() => {
    addKeyEditorOpen=false;
  };
  const cancelAddKeyEditor=() => {
    closeAddKeyEditor();
    rerenderAddKeyBuilder();
  };
  const addKeyWithKeyJson=(keyJson, displayName) => {
    if(isBlank_1(keyJson))return setStatus(status, "Key JSON is required");
    else {
      const submittedKeys=keysFromJson(keyJson);
      if(length(submittedKeys)>0)pendingSelectKeyId=appendPageKeyId(submittedKeys);
      else null;
      const request=New_11(definition.pageId, keyJson, Trim(asText_1(displayName)));
      const pendingId=rememberPending("append-page-add-key", definition.pageId, "/pages/api/add-key", request);
      refreshPendingState();
      setStatus(status, "Adding key; pending command saved in browser DB");
      return postAppendPageKey("/pages/api/add-key", request, (reply) => {
        deletePendingThen(pendingId, () => {
          let _3;
          if(!(reply.key==null)){
            const keyId=reply.key.keyId;
            if(!isBlank_1(keyId))locallyHiddenKeyIds=filter((hidden) =>!sameText(hidden, keyId), locallyHiddenKeyIds);
            pendingSelectKeyId=reply.key.keyId;
            _3=selectBucketKeys(reply.key.keys);
          }
          else _3=length(submittedKeys)>0?selectBucketKeys(submittedKeys):void 0;
          newKeyAliasInput.value="";
          closeAddKeyEditor();
          setStatus(status, "Key added");
          rerenderAddKeyBuilder();
          rerenderAppendForm();
          refreshPendingState();
          load();
        });
      }, (error) => {
        setStatus(status, pendingFailure("Add key", error));
        refreshPendingState();
      });
    }
  };
  const appendValue=() => {
    const request=New_10(definition.pageId, selectedKeyJson, Trim(valueInput.value), Trim(directionInput.value), ["web-append"]);
    if(isBlank_1(request.keyJson))setStatus(workState, "Select or add a key first");
    else if(isBlank_1(request.valueText))setStatus(workState, "Value text is required");
    else if(isActorArguPage(definition)){
      const request_1=New_12(definition.pageId, request.keyJson, request.valueText, ["web-append", "actor-argu"]);
      const m=selectedBucket();
      if(m!=null&&m.$==1){
        const bucket=m.$0;
        const o=tryHead(arrayOrEmpty(bucket.keys));
        const actorAddress=o==null?"":o.$0;
        if(isBlank_1(actorAddress))setStatus(workState, "Actor address key is required");
        else {
          const pendingId=rememberPending("actor-argu-send", definition.pageId, "/pages/api/actor-argu/send", request_1);
          const wsRequest=New_15("actor-argu", pendingId, definition.pageId, definition.title, definition.setName, streamKeyFor(bucket), actorAddress, request_1.rawArgu, definition.shape, ofSeq(delay(() => append_2(arrayOrEmpty(definition.tags), delay(() => append_2(arrayOrEmpty(request_1.tags), delay(() => append_2(["page:"+asText_1(definition.pageId)], delay(() => append_2(["tab:"+asText_1(definition.tabId)], delay(() =>["shape:"+asText_1(definition.shape)])))))))))), browserId, definition.tabId);
          pendingWsAppendIds=pendingWsAppendIds.concat([pendingId]);
          refreshPendingState();
          setStatus(workState, "Sending through WebSocket; pending command saved in browser DB");
          ensureSelectedSubscription();
          sendSyncFrame(JSON.stringify(wsRequest));
          scrollToBottomAfterRender(values);
        }
      }
      else setStatus(workState, "Select or add a key first");
    }
    else if(sameText(definition.shape, "raw")){
      const m_1=selectedBucket();
      if(m_1!=null&&m_1.$==1){
        const bucket_1=m_1.$0;
        const pendingId_1=rememberPending("append-page-append-value", definition.pageId, "/pages/api/append", request);
        const wsRequest_1=New_17("append", pendingId_1, streamKeyFor(bucket_1), request.valueText, "append-page.value", definition.shape, pendingId_1, ofSeq(delay(() => append_2(arrayOrEmpty(definition.tags), delay(() => append_2(arrayOrEmpty(request.tags), delay(() => append_2(["page:"+asText_1(definition.pageId)], delay(() => append_2(["tab:"+asText_1(definition.tabId)], delay(() =>["shape:"+asText_1(definition.shape)])))))))))), browserId, definition.tabId);
        pendingWsAppendIds=pendingWsAppendIds.concat([pendingId_1]);
        refreshPendingState();
        setStatus(workState, "Appending through WebSocket; pending command saved in browser DB");
        ensureSelectedSubscription();
        sendSyncFrame(JSON.stringify(wsRequest_1));
        scrollToBottomAfterRender(values);
      }
      else setStatus(workState, "Select or add a key first");
    }
    else {
      const m_2=selectedBucket();
      if(m_2!=null&&m_2.$==1){
        const bucket_2=m_2.$0;
        const pendingId_2=rememberPending("append-page-append-value", definition.pageId, "/pages/api/append", request);
        const wsRequest_2=New_16("append-page", pendingId_2, definition.pageId, definition.title, definition.setName, streamKeyFor(bucket_2), request.keyJson, request.valueText, request.direction, definition.shape, pendingId_2, ofSeq(delay(() => append_2(arrayOrEmpty(definition.tags), delay(() => append_2(arrayOrEmpty(request.tags), delay(() => append_2(["page:"+asText_1(definition.pageId)], delay(() => append_2(["tab:"+asText_1(definition.tabId)], delay(() =>["shape:"+asText_1(definition.shape)])))))))))), browserId, definition.tabId);
        pendingWsAppendIds=pendingWsAppendIds.concat([pendingId_2]);
        refreshPendingState();
        setStatus(workState, "Appending through WebSocket; pending command saved in browser DB");
        ensureSelectedSubscription();
        sendSyncFrame(JSON.stringify(wsRequest_2));
        scrollToBottomAfterRender(values);
      }
      else setStatus(workState, "Select or add a key first");
    }
  };
  rerenderAddKeyBuilder=() => {
    const baseRendererShape=isActorDynamicPage(definition)?"actor-dynamic":isActorArguPage(definition)?"actor-argu":definition.shape;
    const _3=asText_1(addKeyMode).toLowerCase();
    const rendererShape=_3=="target"?baseRendererShape=="actor-dynamic"?"actor-dynamic-target":baseRendererShape=="actor-argu"?"actor-argu-target":baseRendererShape:_3=="proxy"?baseRendererShape=="actor-dynamic"?"actor-dynamic-proxy":baseRendererShape:baseRendererShape;
    const forceFallback=sameText(addKeyMode, "actor");
    clear(addKeyRendererHost);
    const n=setData("shape", rendererShape, setData("renderer-state", "fallback", addKeyRendererHost));
    setData("mode", addKeyMode, n);
    setHidden(!addKeyEditorOpen, addKeyPanel);
    setHidden(true, fallbackAddKeyPanel);
    setHidden(true, addKeyRendererHost);
    if(sameText(addKeyMode, "actor"))newKeyInput.setAttribute("placeholder", "\"akka.tcp://system@127.0.0.1:9779/user/actor\"");
    else newKeyInput.setAttribute("placeholder", textOr("\"Aster\"", definition.keyPlaceholder));
    if(addKeyEditorOpen&&!forceFallback){
      const m=tryRenderAddKeyWithRegisteredRenderers(definition.pageId, rendererShape, definition.title, definition.setName, definition.keyPlaceholder, definition.defaultKey, (payload) => {
        const keyJson=rendererSubmittedKeyJson(payload);
        const displayName=rendererSubmittedDisplayName(payload);
        if(isBlank_1(keyJson))setStatus(status, "Renderer key is required");
        else {
          newKeyInput.value=keyJson;
          setData("last-key-json", keyJson, addKeyRendererHost);
          addKeyWithKeyJson(keyJson, displayName);
        }
      }, cancelAddKeyEditor, (payload) => {
        const keyJson=rendererSubmittedKeyJson(payload);
        const displayName=rendererSubmittedDisplayName(payload);
        if(!isBlank_1(keyJson)){
          newKeyInput.value=keyJson;
          setData("last-key-json", keyJson, addKeyRendererHost);
        }
        if(!isBlank_1(displayName))newKeyAliasInput.value=displayName;
      });
      if(m==null){
        setHidden(false, fallbackAddKeyPanel);
        addKeyRendererHost.textContent="";
      }
      else {
        const node=m.$0;
        setData("renderer-state", "custom", addKeyRendererHost);
        setHidden(false, addKeyRendererHost);
        addKeyRendererHost.appendChild(node);
      }
    }
    else addKeyEditorOpen?(setHidden(false, fallbackAddKeyPanel),addKeyRendererHost.textContent=""):setData("renderer-state", "closed", addKeyRendererHost);
  };
  rerenderAppendForm=() => {
    let effectiveKeyId;
    const rendererShape=isActorArguPage(definition)?"actor-argu":definition.shape;
    clear(form);
    const effectiveKeyJson=effectiveSelectedKeyJson();
    const m=tryFind((bucket) => bucket.keyId==selected, buckets);
    if(m==null){
      const keys=keysFromJson(selectedKeyJson);
      effectiveKeyId=length(keys)===0?"":appendPageKeyId(keys);
    }
    else effectiveKeyId=m.$0.keyId;
    const selectedKeys=effectiveSelectedKeys();
    const x=setData("selected-key-json", effectiveKeyJson, setData("selected-key-id", effectiveKeyId, setData("shape", rendererShape, setData("renderer-state", "fallback", form))));
    setData("selected-key-source", isBlank_1(effectiveKeyJson)?"none":"selected", x);
    const customNode=isBlank_1(effectiveKeyJson)?null:tryRenderAppendInputWithRegisteredRenderers(definition.pageId, rendererShape, definition.title, definition.setName, effectiveKeyId, effectiveKeyJson, selectedKeys, valueInput.placeholder, valueInput.value, (payload) => {
      let _3;
      const submitted=rendererSubmittedText(payload);
      const submittedKeyJson=rendererSubmittedKeyJson(payload);
      if(isBlank_1(submitted))setStatus(workState, "Renderer value text is required");
      else {
        if(!isBlank_1(submittedKeyJson)){
          const submittedKeys=keysFromJson(submittedKeyJson);
          _3=length(submittedKeys)>0?(selectedKeyJson=submittedKeyJson,selected=appendPageKeyId(submittedKeys),newKeyInput.value=submittedKeyJson):void 0;
        }
        else _3=void 0;
        const keyJson=effectiveSelectedKeyJson();
        const keys_1=effectiveSelectedKeys();
        if(isBlank_1(selectedKeyJson)&&!isBlank_1(keyJson)){
          selectedKeyJson=keyJson;
          newKeyInput.value=keyJson;
        }
        if(isBlank_1(selected)&&length(keys_1)>0)selected=appendPageKeyId(keys_1);
        valueInput.value=submitted;
        setData("last-raw-argu", submitted, form);
        appendValue();
      }
    }, (payload) => {
      const submitted=rendererSubmittedText(payload);
      if(!isBlank_1(submitted)){
        valueInput.value=submitted;
        setData("last-raw-argu", submitted, form);
      }
    });
    if(customNode==null)isActorArguPage(definition)?(form.className="append-form actor-argu-form",append_1(form, [valueInput, appendButton])):asText_1(definition.shape).toLowerCase()=="fcell-chat"?(form.className="append-form chat-form",append_1(form, [directionInput, valueInput, appendButton])):(form.className="append-form",append_1(form, [valueInput, appendButton]));
    else {
      const node=customNode.$0;
      form.className="append-form custom-append-input-form";
      setData("renderer-state", "custom", form);
      form.appendChild(node);
    }
  };
  rerenderAddKeyBuilder();
  rerenderAppendForm();
  replayingPending=false;
  replayPendingCommands=() => {
    if(!replayingPending){
      replayingPending=true;
      readAllPending((commands) => {
        let remaining, accepted;
        const mine=filter((command) => sameText(command.method, "POST")&&!isBlank_1(command.url)&&!isBlank_1(command.payloadJson), filter(isPendingForThisPage, commands));
        if(length(mine)===0){
          replayingPending=false;
          refreshPendingState();
        }
        else {
          remaining=length(mine);
          accepted=0;
          setStatus(pendingState, "Replaying "+String(length(mine))+" pending command(s)");
          const finishOne=() => {
            remaining=remaining-1;
            remaining===0?(replayingPending=false,refreshPendingState(),accepted>0?(setStatus(workState, "Replayed "+String(accepted)+" pending command(s)"),load()):void 0):void 0;
          };
          iter((command) => {
            postJsonText(command.url, command.payloadJson, (body) => {
              deletePendingThen(command.commandId, () => {
                let _3, _4;
                accepted=accepted+1;
                if(sameText(command.kind, "append-page-remove-page")){
                  try {
                    const reply=json(isBlank_1(body)?"{}":body);
                    _3=!(reply==null)?writeAppendPagesDefinitions(reply):null;
                  }
                  catch(m){
                    _3=null;
                  }
                  _4=globalThis.location.assign("/chat");
                }
                else _4=void 0;
                finishOne();
              });
            }, () => {
              finishOne();
            });
          }, mine);
        }
      });
    }
  };
  const openAddKeyEditor=(mode) => {
    const normalizedMode=asText_1(mode).toLowerCase();
    if(addKeyEditorOpen&&sameText(addKeyMode, normalizedMode))addKeyEditorOpen=false;
    else {
      addKeyMode=normalizedMode;
      addKeyEditorOpen=true;
    }
    actionPool.removeAttribute("open");
    rerenderAddKeyBuilder();
  };
  let _1=(addActorKeyButton.addEventListener("click", () => openAddKeyEditor("actor")),addKeyButton.addEventListener("click", () => openAddKeyEditor("target")),addProxyKeyButton.addEventListener("click", () => openAddKeyEditor("proxy")),cleanKeyButton.addEventListener("click", () => {
    newKeyInput.value="";
    newKeyAliasInput.value="";
  }),cancelKeyButton.addEventListener("click", cancelAddKeyEditor),okKeyButton.addEventListener("click", () => addKeyWithKeyJson(isBlank_1(newKeyInput.value)?asText_1(definition.defaultKey):Trim(newKeyInput.value), newKeyAliasInput.value)),removeKeyButton.addEventListener("click", () => {
    if(isBlank_1(selected))setStatus(status, "Select a key first");
    else {
      const removedKeyId=selected;
      const request=New_14(definition.pageId, removedKeyId);
      const pendingId=rememberPending("append-page-remove-key", definition.pageId, "/pages/api/remove-key", request);
      refreshPendingState();
      setStatus(status, "Removing key; pending command saved in browser DB");
      postRemoveAppendPageKey("/pages/api/remove-key", request, () => {
        deletePendingThen(pendingId, () => {
          rememberLocallyHiddenKeyId(removedKeyId);
          buckets=filter((bucket) => bucket.keyId!=removedKeyId, buckets);
          selected="";
          selectedKeyJson="";
          writeCurrentSnapshot();
          renderList();
          renderValues();
          setStatus(status, "Key removed");
          refreshPendingState();
        });
      }, (error) => {
        setStatus(status, pendingFailure("Remove key", error));
        refreshPendingState();
      });
    }
  }),removePageButton.addEventListener("click", () => {
    const request=New_13(definition.pageId);
    const pendingId=rememberPending("append-page-remove-page", definition.pageId, "/pages/api/remove-page", request);
    refreshPendingState();
    setStatus(status, "Removing page; pending command saved in browser DB");
    return postJson("/pages/api/remove-page", request, (reply) => {
      deletePendingThen(pendingId, () => {
        writeAppendPagesDefinitions(reply);
        setStatus(status, "Page removed");
        globalThis.location.assign("/chat");
      });
    }, (error) => {
      setStatus(status, pendingFailure("Remove page", error));
      refreshPendingState();
    });
  }),reload.addEventListener("click", load),keyFilter.addEventListener("input", load),appendButton.addEventListener("click", appendValue),load(),subscribeKeyRegistry(),refreshPendingState());
  let _2=_1;
  _2;
}
function renderNav(nav, activePath, pages){
  clear(nav);
  iter((_1) => {
    const href=_1[0];
    const label=_1[1];
    const x=setHref(href, element_1("a", isCurrentPage(activePath, href)?"nav-link active":"nav-link", label));
    let _2=setTestId_1("nav-"+label.toLowerCase(), x);
    nav.appendChild(_2);
  }, [["/chat", "Chat"], ["/sets", "Sets"], ["/actors", "Actors"]]);
  iter((page) => {
    const href=pagePath(page);
    const x=setHref(href, element_1("a", isCurrentPage(activePath, href)?"nav-link active":"nav-link", null));
    let _1=setTestId_1("nav-append-page-"+asText_1(page.pageId), x);
    let _2=setData("page-id", page.pageId, _1);
    const link=setData("shape", page.shape, _2);
    const x_1=element_1("span", "nav-type-badge "+pageTypeClass(page), pageTypeBadge(page));
    const badge=setTestId_1("nav-type-badge-append-page-"+asText_1(page.pageId), x_1);
    badge.setAttribute("title", pageTypeLabel(page));
    badge.setAttribute("aria-label", pageTypeLabel(page));
    const x_2=button_1("nav-close", "x");
    const closeButton=setTestId_1("nav-close-append-page-"+asText_1(page.pageId), x_2);
    closeButton.setAttribute("aria-label", "Remove page "+pageTitle(page));
    closeButton.setAttribute("title", "Remove page");
    closeButton.addEventListener("click", (event) => {
      event.preventDefault();
      event.stopPropagation();
      closeButton.setAttribute("disabled", "disabled");
      return postJson("/pages/api/remove-page", New_13(page.pageId), (reply) => {
        writeAppendPagesDefinitions(reply);
        isCurrentPage(activePath, href)?globalThis.location.assign("/chat"):renderNav(nav, activePath, reply.pages);
      }, (error) => {
        closeButton.removeAttribute("disabled");
        closeButton.textContent="!";
        closeButton.setAttribute("title", "Remove page failed: "+error);
      });
    });
    append_1(link, [badge, element_1("span", "nav-title", pageTitle(page)), closeButton]);
    nav.appendChild(link);
  }, arrayOrEmpty(pages));
}
function shell(activePath, pages){
  const app=element_1("div", "app", null);
  const top=element_1("header", "topbar", null);
  const topRow=element_1("div", "topbar-main", null);
  const brandCluster=element_1("div", "brand-cluster", null);
  const navShell=element_1("div", "nav-shell", null);
  const navViewport=setTestId_1("nav-viewport", element_1("div", "nav-viewport", null));
  const nav=setId("ptc-nav", element_1("nav", "nav", null));
  const navBack=setTestId_1("nav-scroll-left", button_1("nav-scroll", "<"));
  const navForward=setTestId_1("nav-scroll-right", button_1("nav-scroll", ">"));
  const create=renderPageCreator(nav, activePath, pages);
  const registryHealth=setTestId_1("append-registry-health", element_1("div", "state registry-health", "append registry ws pending"));
  const scrollTabs=(delta) => {
    navViewport.scrollLeft=navViewport.scrollLeft+delta;
  };
  navBack.setAttribute("aria-label", "Scroll tabs left");
  navForward.setAttribute("aria-label", "Scroll tabs right");
  navBack.addEventListener("click", () => scrollTabs(-260));
  navForward.addEventListener("click", () => scrollTabs(260));
  append_1(brandCluster, [element_1("div", "brand", "PTC.Comm SPA"), registryHealth]);
  renderNav(nav, activePath, pages);
  const x=element_1("a", "logout", "Logout");
  const logout=setHref(currentLogoutPath(), x);
  const page=element_1("main", "page", null);
  append_1(navViewport, [nav]);
  append_1(navShell, [navBack, navViewport, navForward]);
  append_1(topRow, [brandCluster, navShell, logout]);
  append_1(top, [topRow, create]);
  append_1(app, [top, page]);
  return[app, page];
}
function setMain(node){
  const main=doc_1().getElementById("main");
  if(!(main==null)){
    clear(main);
    main.appendChild(node);
  }
}
function mountSets(page){
  let selected, buckets, syncSocket, queuedSyncFrames, subscribedStreams, tailRequestedStreams, registryTailRequested, ensureSetsSubscriptions, loadGeneration;
  page.className="page sets-grid";
  selected="";
  buckets=[];
  const side=element_1("aside", "sidebar", null);
  const sideHead=element_1("div", "panel-head", null);
  const reload=button_1("", "Reload");
  const filters=element_1("div", "filters", null);
  const keyFilter=input_1("key contains");
  const setFilter=input_1("set name");
  const status=element_1("div", "state", "Loading sets");
  const list=element_1("div", "list", null);
  const work=element_1("section", "work", null);
  append_1(sideHead, [element_1("h1", "", "Sets"), reload]);
  append_1(filters, [keyFilter, setFilter, status]);
  append_1(side, [sideHead, filters, list]);
  append_1(page, [side, work]);
  syncSocket=null;
  queuedSyncFrames=[];
  subscribedStreams=[];
  tailRequestedStreams=[];
  registryTailRequested=false;
  ensureSetsSubscriptions=() => { };
  loadGeneration=0;
  const sameText=(left, right) => asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
  const streamIdentity=(streamKey) => concat_1("\n", [asText_1(streamKey.pageId), asText_1(streamKey.mode), asText_1(streamKey.setName), concat_1("\u001f", arrayOrEmpty(streamKey.keys))]);
  const setValueStreamKey=(pageId, mode, setName, keys) => New_4(asText_1(pageId), textOr("set", mode), asText_1(setName), arrayOrEmpty(keys));
  const currentFilterTexts=() =>[isBlank_1(keyFilter.value)?"":Trim(keyFilter.value), isBlank_1(setFilter.value)?"":Trim(setFilter.value)];
  const currentCacheKey=() => {
    const p=currentFilterTexts();
    return cacheKey("sets-state", ofArray([p[0], p[1]]));
  };
  function renderList(){
    clear(list);
    iter((bucket) => {
      const item=button_1(bucket.keyId==selected?"list-card active":"list-card", null);
      setData("key-id", bucket.keyId, setTestId_1("sets-bucket", item));
      append_1(item, [element_1("div", "strong wrap", asText_1(bucket.setName)), element_1("div", "muted wrap", joinValues(bucket.keys)), element_1("div", "meta", "values="+String(bucket.valueCount)+" seq="+String(bucket.maxSequence)+" updated="+String(asText_1(bucket.updatedAtUtc)))]);
      item.addEventListener("click", () => {
        selected=bucket.keyId;
        renderList();
        return renderDetail();
      });
      list.appendChild(item);
    }, buckets);
  }
  function renderDetail(){
    clear(work);
    const bucket=tryFind((bucket_2) => bucket_2.keyId==selected, buckets);
    if(bucket!=null&&bucket.$==1){
      const bucket_1=bucket.$0;
      const detail=element_1("div", "detail", null);
      const head_1=element_1("div", "work-head", null);
      const title=element_1("div", "", null);
      append_1(title, [element_1("label", "", "Key set"), element_1("h2", "", bucket_1.keyId)]);
      append_1(head_1, [title, element_1("div", "state", String(bucket_1.valueCount)+" value(s)")]);
      detail.appendChild(head_1);
      const table=element_1("table", "data-table", null);
      const thead=element_1("thead", "", null);
      const headerRow=element_1("tr", "", null);
      iter((label) => {
        headerRow.appendChild(element_1("th", "", label));
      }, ["Value", "Keys", "Created", "Body", "Tags"]);
      thead.appendChild(headerRow);
      const tbody=element_1("tbody", "", null);
      iter((value) => {
        const row=element_1("tr", "", null);
        iter((_1) => {
          row.appendChild(element_1("td", _1[1], _1[0]));
        }, [[value.valueId, "wrap"], [joinValues(value.keys), "wrap"], [asText_1(value.createdAtUtc), "wrap"], [asText_1(value.value), "preview"], [joinValues(value.tags), "wrap"]]);
        tbody.appendChild(row);
      }, arrayOrEmpty(bucket_1.values));
      append_1(table, [thead, tbody]);
      append_1(detail, [table]);
      work.appendChild(detail);
    }
    else work.appendChild(element_1("div", "empty", "No set selected."));
  }
  const applySnapshot=(source, data) => {
    buckets=arrayOrEmpty(data.buckets);
    (isBlank_1(selected)||!exists((bucket) => bucket.keyId==selected, buckets))&&length(buckets)>0?selected=get(buckets, 0).keyId:length(buckets)===0?selected="":void 0;
    setStatus(status, "Loaded "+String(length(buckets))+" "+String(source)+" bucket(s)");
    renderList();
    renderDetail();
    return ensureSetsSubscriptions();
  };
  const load=() => {
    loadGeneration=loadGeneration+1;
    const generation=loadGeneration;
    tailRequestedStreams=[];
    const parts=MarkResizable([]);
    const p=currentFilterTexts();
    const setText=p[1];
    const keyText=p[0];
    if(!isBlank_1(keyText))parts.push("participantId="+encodeURIComponent(keyText));
    if(!isBlank_1(setText))parts.push("setName="+encodeURIComponent(setText));
    parts.push("limit="+String(defaultRenderLimit()));
    const cacheKey_1=currentCacheKey();
    readJson(cacheKey_1, (a) => {
      if(a==null){
        if(generation===loadGeneration){
          buckets=[];
          selected="";
          renderList();
          renderDetail();
          ensureSetsSubscriptions();
        }
      }
      else if(a.$0,generation===loadGeneration)applySnapshot("cached", a.$0);
    });
    getJson("/sets/api/state?"+concat_1("&", ofSeq(parts)), (data) => {
      if(generation===loadGeneration){
        writeSnapshotWithWatermark(cacheKey_1, data, data.maxSequence, setValueCount(data.buckets), "sets-state");
        applySnapshot("backend", data);
      }
    }, (error) => {
      if(generation===loadGeneration)setStatus(status, error);
    });
  };
  const setWsState=(value) => {
    setData("ws-state", value, page);
  };
  const setWsStreamCount=() => {
    setData("ws-stream-count", String(length(subscribedStreams)), page);
  };
  function recF(recI, _1){
    while(true)
      switch(recI){
        case 0:
          const socket=ensureSyncSocket();
          return Equals(socket.readyState, 1)?socket.send(_1):void(queuedSyncFrames=queuedSyncFrames.concat([_1]));
        case 1:
          const request=New_2("read-tail", newRequestId("sets-read-tail"), _1, defaultRenderLimit());
          _1=JSON.stringify(request);
          recI=0;
          break;
        case 2:
          const identity=streamIdentity(_1);
          if(!isBlank_1(identity)&&!(((p) =>(a) => exists(p, a))(((identity_1) =>(existing) => existing==identity_1)(identity)))(tailRequestedStreams)){
            tailRequestedStreams=tailRequestedStreams.concat([identity]);
            _1=_1;
            recI=1;
          }
          else return null;
          break;
      }
  }
  function flushSyncFrames(socket){
    if(Equals(socket.readyState, 1)){
      const frames=queuedSyncFrames;
      queuedSyncFrames=[];
      iter((frame) => {
        socket.send(frame);
      }, frames);
    }
  }
  function ensureSyncSocket(){
    let _1, _2;
    if(syncSocket!=null&&syncSocket.$==1){
      const socket=syncSocket.$0;
      _1=(Equals(socket.readyState, 1)||Equals(socket.readyState, 0))&&(_2=syncSocket.$0,true);
    }
    else _1=false;
    if(_1)return _2;
    else {
      setWsState("connecting");
      const socket_1=new WebSocket(syncWebSocketUrl());
      syncSocket=Some(socket_1);
      socket_1.onopen=() => {
        setWsState("open");
        return flushSyncFrames(socket_1);
      };
      socket_1.onmessage=(event) => handleSyncMessage(String(event.data));
      socket_1.onerror=() => {
        setWsState("error");
        return setStatus(status, "WebSocket sets sync error");
      };
      socket_1.onclose=() => {
        syncSocket=null;
        subscribedStreams=[];
        tailRequestedStreams=[];
        registryTailRequested=false;
        setWsStreamCount();
        return setWsState("closed");
      };
      return socket_1;
    }
  }
  function sendSyncFrame(frame){
    return recF(0, frame);
  }
  function subscribeStream(streamKey){
    const identity=streamIdentity(streamKey);
    if(!isBlank_1(identity)&&!exists((existing) => existing==identity, subscribedStreams)){
      subscribedStreams=subscribedStreams.concat([identity]);
      setWsStreamCount();
      setWsState("subscribing");
      sendSyncFrame(JSON.stringify(New_1("subscribe", newRequestId("sets-subscribe"), streamKey)));
    }
  }
  function requestReadTail(streamKey){
    return recF(1, streamKey);
  }
  function requestReadTailOnce(streamKey){
    return recF(2, streamKey);
  }
  function handleSyncEvent(event){
    if(!(event==null)&&!(event.streamKey==null)){
      let m, updated, _1;
      const m_1=asText_1(event.sourceKind).toLowerCase();
      if(m_1=="set.stream"){
        if(event==null||isBlank_1(event.payload))m=null;
        else try {
          const wire=json(event.payload);
          m=wire==null||asText_1(wire.schema)!="ptc.comm.spa.set.stream.v1"?null:Some(setValueStreamKey(wire.pageId, wire.mode, wire.setName, wire.keys));
        }
        catch(m_3){
          m=null;
        }
        if(m==null)void 0;
        else {
          const streamKey=m.$0;
          subscribeStream(streamKey);
          requestReadTailOnce(streamKey);
        }
      }
      else if(m_1=="set"){
        if(!(event==null)&&event.sequence>0n&&!(event.streamKey==null)){
          const setName=asText_1(event.streamKey.setName);
          const keys=arrayOrEmpty(event.streamKey.keys);
          const p=currentFilterTexts();
          const setText=p[1];
          const keyText=p[0];
          if((isBlank_1(setText)||sameText(setName, setText))&&(isBlank_1(keyText)||exists((key) => asText_1(key).toLowerCase().indexOf(keyText.toLowerCase())!=-1, arrayOrEmpty(keys)))){
            const value=New_19(textOr(event.eventId, event.sourceId), arrayOrEmpty(event.streamKey.keys), asText_1(event.createdAtUtc), asText_1(event.payload), arrayOrEmpty(event.tags));
            const keyId=asText_1(setName)+"::"+concat_1(" + ", arrayOrEmpty(keys));
            const m_2=tryFind((bucket) => sameText(bucket.keyId, keyId), buckets);
            if(m_2==null)updated=New_18(keyId, setName, keys, 1, event.sequence, asText_1(event.createdAtUtc), [value]);
            else {
              const existing=m_2.$0;
              const existingValues=arrayOrEmpty(existing.values);
              const alreadyVisible=exists((row) => sameText(row.valueId, value.valueId), existingValues);
              const v=filter((row) =>!sameText(row.valueId, value.valueId), existingValues).concat([value]);
              const mergedValues=latestArray(defaultRenderLimit(), v);
              if(alreadyVisible)_1=existing.valueCount;
              else {
                const a=existing.valueCount;
                const b=length(existingValues);
                let _2=Compare(a, b)===1?a:b;
                _1=_2+1;
              }
              const a_1=existing.maxSequence;
              const b_1=event.sequence;
              let _3=Compare(a_1, b_1)===1?a_1:b_1;
              updated=New_18(existing.keyId, existing.setName, existing.keys, _1, _3, textOr(existing.updatedAtUtc, event.createdAtUtc), mergedValues);
            }
            buckets=sortBy((bucket) =>[asText_1(bucket.setName), asText_1(bucket.keyId)], arrayOrEmpty(filter((bucket) =>!sameText(bucket.keyId, keyId), buckets).concat([updated])));
            selected=keyId;
            renderList();
            renderDetail();
            const snapshot=New_20(fold((_4, _5) => Compare(_4, _5)===1?_4:_5, 0n, map((bucket) => bucket==null?0n:bucket.maxSequence, buckets)), buckets);
            writeSnapshotWithWatermark(currentCacheKey(), snapshot, snapshot.maxSequence, setValueCount(snapshot.buckets), "sets-state");
            ensureSetsSubscriptions();
            setStatus(status, "Synced set event "+value.valueId);
          }
          else void 0;
        }
        else void 0;
      }
      else void 0;
    }
  }
  function handleSyncMessage(text){
    try {
      const response=json(text);
      const responseType=asText_1(response.type).toLowerCase();
      const responseStatus=asText_1(response.status).toLowerCase();
      switch(responseStatus=="ok"?responseType=="subscribe"?0:responseType=="stream-event"?1:responseType=="read-tail"?2:responseType=="read"?2:responseType=="tail"?2:4:responseStatus=="error"?3:4){
        case 0:
          setData("ws-last-stream", response.streamKey, page);
          setWsState("subscribed");
          break;
        case 1:
          handleSyncEvent(response.event);
          break;
        case 2:
          iter(handleSyncEvent, arrayOrEmpty(response.events));
          break;
        case 3:
          setStatus(status, "WebSocket sets sync error: "+asText_1(response.error));
          break;
        case 4:
          null;
          break;
      }
    }
    catch(error){
      setStatus(status, "WebSocket sets sync parse failed: "+errorMessage(error));
    }
  }
  ensureSetsSubscriptions=() => {
    const registryKey=New_4("__set-registry", "set-registry", "__sets", ["__sets"]);
    subscribeStream(registryKey);
    if(!registryTailRequested){
      registryTailRequested=true;
      requestReadTail(registryKey);
    }
    iter((bucket) => {
      const streamKey=setValueStreamKey("", "set", bucket.setName, bucket.keys);
      subscribeStream(streamKey);
      requestReadTailOnce(streamKey);
    }, buckets);
  };
  reload.addEventListener("click", load);
  keyFilter.addEventListener("input", load);
  setFilter.addEventListener("input", load);
  load();
}
function mountActors(page){
  let actorSnapshot, syncSocket, queuedSyncFrames, subscribedRegistry, registryTailRequested, dynamicActorsPageAccepted;
  page.className="page actors-page";
  const head_1=element_1("div", "work-head actors-head", null);
  const title=element_1("div", "", null);
  const actions=element_1("div", "head-actions", null);
  const status=element_1("div", "state", "Loading actors");
  const reload=button_1("", "Reload");
  const nodes=element_1("div", "nodes", null);
  const treePanel=setTestId_1("actor-tree-panel", element_1("section", "actor-tree-panel", null));
  append_1(title, [element_1("label", "", "Actor / Participant Management"), element_1("h1", "", "Actors")]);
  append_1(actions, [status, reload]);
  append_1(head_1, [title, actions]);
  append_1(page, [head_1, treePanel, nodes]);
  const emptySnapshot=New_21(0, 0, 0n, []);
  actorSnapshot=emptySnapshot;
  syncSocket=null;
  queuedSyncFrames=[];
  subscribedRegistry=false;
  registryTailRequested=false;
  dynamicActorsPageAccepted=false;
  const collapsedTreeNodes=new HashSet("New_3");
  const cacheKey_1=cacheKey("actors-snapshot", FSharpList.Empty);
  const sameText=(left, right) => asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
  const actorRegistryStreamKey=() => New_4("__actor-registry", "actor-registry", "__actors", ["__actors"]);
  const isAkkaAddress=(value) => {
    const text=asText_1(value).toLowerCase();
    return StartsWith(text, "akka://")||StartsWith(text, "akka.tcp://")||StartsWith(text, "akka.ssl.tcp://");
  };
  function renderActorTree(source, tree){
    clear(treePanel);
    const safeNodes=arrayOrEmpty(tree.nodes);
    const title_1=element_1("div", "actor-tree-title", null);
    const content=setTestId_1("actor-tree-content", element_1("div", "actor-tree-content", null));
    const treeViewport=setTestId_1("actor-tree-viewport", element_1("div", "actor-tree-viewport", null));
    const treeBody=setTestId_1("actor-tree-body", element_1("div", "actor-tree-body", null));
    const tableViewport=setTestId_1("actor-tree-table-viewport", element_1("div", "actor-tree-table-viewport", null));
    const table=setTestId_1("actor-tree-table", element_1("table", "actor-tree-table", null));
    const thead=element_1("thead", "", null);
    const tbody=element_1("tbody", "", null);
    append_1(title_1, [element_1("label", "", "ActorTree"), element_1("h2", "", String(asText_1(tree.projectionId))+" / v"+String(tree.projectionVersion)), element_1("div", "state", String(source)+"; "+String(length(safeNodes))+" node(s); "+String(arrayOrEmpty(tree.edges).length)+" edge(s)")]);
    const safeNodes_1=arrayOrEmpty(tree.nodes);
    const jsonString=(value) =>"\""+Replace(Replace(Replace(Replace(Replace(asText_1(value), "\\", "\\\\"), "\"", "\\\""), "\r", "\\r"), "\n", "\\n"), "\u0009", "\\t")+"\"";
    const jsonArray=(values) =>"["+concat_1(",", map(jsonString, arrayOrEmpty(values)))+"]";
    const nodesJson=concat_1(",", map((node) => {
      const tags=jsonArray(arrayOrEmpty(node.tags));
      return"{\"id\":"+jsonString(node.id)+","+"\"parentId\":"+jsonString(node.parentId)+","+"\"label\":"+jsonString(node.label)+","+"\"fullPath\":"+jsonString(node.fullPath)+","+"\"kind\":"+jsonString(node.kind)+","+"\"status\":"+jsonString(node.status)+","+"\"address\":"+jsonString(node.address)+","+"\"tags\":"+tags+"}";
    }, safeNodes_1));
    const rootIdsJson=jsonArray(map(asText_1, arrayOrEmpty(tree.rootNodeIds)));
    let _1="{\"schema\":\"fskynet-sdui\",\"version\":\"1\",\"documentId\":"+jsonString("ptcs.actors."+textOr("actor-tree", tree.projectionId))+","+"\"surface\":\"ActorsPage\","+"\"documentType\":\"ActorTopologyPage\","+"\"projectionId\":"+jsonString(tree.projectionId)+","+"\"projectionVersion\":"+String(tree.projectionVersion)+","+"\"ui\":[{\"type\":\"ActorsPage\",\"id\":\"ptcs-actors-page\",\"dataRef\":\"actorTreeNodes\",\"rootNodeIds\":"+rootIdsJson+",\"nodeIdField\":\"id\",\"parentIdField\":\"parentId\",\"labelField\":\"label\",\"statusField\":\"status\",\"columns\":[\"kind\",\"status\",\"address\",\"fullPath\"],\"groupBy\":\"actorSystemHostPort\",\"roleOrder\":[\"ptcs-host\",\"gw-host\",\"rn-host\",\"unknown\"]}],"+"\"actions\":[{\"kind\":\"reload\"},{\"kind\":\"generate-report\"},{\"kind\":\"schedule-report\"}],"+"\"data\":{\"actorTreeNodes\":["+nodesJson+"]}"+"}";
    const m=tryRenderWithRegisteredPageRenderers(_1);
    if(m==null){
      dynamicActorsPageAccepted=false;
      nodes.removeAttribute("style");
      setData("renderer", "fallback", treePanel);
      const childMap=OfArray(groupBy((node) => asText_1(node.parentId), safeNodes));
      const nodeMap=OfArray(map((node) =>[asText_1(node.id), node], safeNodes));
      function renderNode(depth, node){
        let toggle;
        const id=asText_1(node.id);
        const o=childMap.TryFind(id);
        let _6=o==null?[]:o.$0;
        const children=sortBy((node_1) => asText_1(node_1.label), _6);
        const hasChildren=length(children)>0;
        const row=setData("node-id", id, setTestId_1("actor-tree-row", element_1("div", "actor-tree-row", null)));
        setData("parent-id", asText_1(node.parentId), row);
        const a=12;
        const a_1=0;
        const b=Compare(a_1, depth)===1?a_1:depth;
        let _7=Compare(a, b)===-1?a:b;
        let _8=String(_7);
        setData("depth", _8, row);
        const toggleText=!hasChildren?"":collapsedTreeNodes.Contains(id)?"+":"-";
        if(hasChildren){
          const value=setTestId_1("actor-tree-toggle", button_1("actor-tree-toggle", toggleText));
          toggle=(value.setAttribute("aria-expanded", collapsedTreeNodes.Contains(id)?"false":"true"),value.setAttribute("title", collapsedTreeNodes.Contains(id)?"Expand":"Collapse"),value);
        }
        else toggle=element_1("span", "actor-tree-toggle actor-tree-toggle-placeholder", "");
        if(hasChildren)toggle.addEventListener("click", () => {
          collapsedTreeNodes.Contains(id)?collapsedTreeNodes.Remove(id):collapsedTreeNodes.SAdd(id);
          return renderActorTree("toggle", tree);
        });
        else null;
        const labelText=asText_1(node.label);
        const kindText=asText_1(node.kind);
        const statusText=asText_1(node.status);
        const fullPathText=asText_1(node.fullPath);
        const addressText=asText_1(node.address);
        const displayText=!isBlank_1(addressText)?addressText:!isBlank_1(fullPathText)?fullPathText:!isBlank_1(labelText)?labelText:id;
        const label=element_1("span", "actor-tree-label", displayText);
        const statusDot_1=setData("status", statusText, element_1("span", "actor-tree-status-dot", ""));
        const kindPill=element_1("span", "actor-tree-kind-pill", kindText);
        const statusPill=setData("status", statusText, element_1("span", "actor-tree-status-pill", statusText));
        label.setAttribute("title", displayText);
        kindPill.setAttribute("title", "kind: "+kindText);
        statusPill.setAttribute("title", "status: "+statusText);
        append_1(row, [toggle, statusDot_1, label, kindPill, statusPill]);
        treeBody.appendChild(row);
        if(!collapsedTreeNodes.Contains(id)){
          const _9=depth+1;
          return iter((_10) => renderNode(_9, _10), children);
        }
        else return null;
      }
      const roots=arrayOrEmpty(tree.rootNodeIds);
      let _2=length(roots)===0?map((a) => a.id, filter((node) => isBlank_1(node.parentId), safeNodes)):roots;
      let _3=choose((id) => nodeMap.TryFind(asText_1(id)), _2);
      let _4=sortBy((node) => asText_1(node.label), _3);
      iter((_6) => renderNode(0, _6), _4);
      const headerRow=element_1("tr", "", null);
      let _5=(iter((text) => {
        headerRow.appendChild(element_1("th", "", text));
      }, ["parentId", "id", "kind", "status", "address", "fullPath"]),thead.appendChild(headerRow),iter((node) => {
        const x=setTestId_1("actor-tree-table-row", element_1("tr", "", null));
        const row=setData("node-id", asText_1(node.id), x);
        iter((text) => {
          row.appendChild(element_1("td", "", text));
        }, [asText_1(node.parentId), asText_1(node.id), asText_1(node.kind), asText_1(node.status), asText_1(node.address), asText_1(node.fullPath)]);
        tbody.appendChild(row);
      }, sortBy((node) => asText_1(node.fullPath), safeNodes)),table.appendChild(thead),table.appendChild(tbody),treeViewport.appendChild(treeBody),tableViewport.appendChild(table),append_1(content, [treeViewport, tableViewport]),void append_1(treePanel, [title_1, content]));
      return _5;
    }
    else {
      const dynamicNode=m.$0;
      dynamicActorsPageAccepted=true;
      clear(nodes);
      nodes.setAttribute("style", "display:none;");
      const host=setTestId_1("actor-tree-dynamic-page", element_1("div", "actor-tree-dynamic-page", null));
      setData("renderer", "dynamic-actors-page", treePanel);
      host.appendChild(dynamicNode);
      treePanel.appendChild(host);
      return;
    }
  }
  const applySnapshot=(source, data) => {
    actorSnapshot=data==null?emptySnapshot:data;
    clear(nodes);
    dynamicActorsPageAccepted?nodes.setAttribute("style", "display:none;"):(nodes.removeAttribute("style"),iter((node) => {
      const block=setData("node-id", node.nodeId, setTestId_1("actor-node", element_1("section", "node-block", null)));
      const blockHead=element_1("div", "work-head", null);
      const title_1=element_1("div", "", null);
      const grid=element_1("div", "actor-grid", null);
      let _1=(append_1(title_1, [element_1("label", "", "Node"), element_1("h2", "", asText_1(node.nodeId))]),append_1(blockHead, [title_1, element_1("div", "state", asText_1(node.status)+" / "+joinValues(node.roles))]),iter((actor) => {
        const card=setData("actor-id", actor.actorId, setTestId_1("actor-card", element_1("div", "actor-card", null)));
        const line=asText_1(actor.kind)+" / "+joinValues(actor.keys);
        const routees=element_1("div", "routees", null);
        const address=TrimEnd(Trim(asText_1(node.nodeAddress)), ["/"]);
        const logicalNode=TrimEnd(Trim(asText_1(node.nodeId)), ["/"]);
        const node_1=isBlank_1(address)?logicalNode:address;
        const actor_1=Trim(asText_1(actor.actorId));
        const fullAddress=isBlank_1(actor_1)?node_1:isAkkaAddress(actor_1)?actor_1:isBlank_1(node_1)?actor_1:StartsWith(actor_1, "/")?node_1+actor_1:isAkkaAddress(node_1)?node_1+"/user/"+TrimStart(actor_1, ["/"]):node_1+"/"+TrimStart(actor_1, ["/"]);
        const addressRow=setData("actor-address", fullAddress, setTestId_1("actor-address", element_1("div", "meta wrap actor-address", "address "+fullAddress)));
        let _2=(card.appendChild(cardTitle(textOr(actor.actorId, actor.displayName), actor.actorId, actor.status, line)),card.appendChild(addressRow),iter((routee) => {
          const row=element_1("div", "routee", null);
          let _3=(append_1(row, [statusDot(routee.status), element_1("span", "strong", asText_1(routee.routeeId)), element_1("span", "muted wrap", joinValues(routee.tags))]),row);
          routees.appendChild(_3);
        }, arrayOrEmpty(actor.routees)),card.appendChild(routees),card);
        grid.appendChild(_2);
      }, arrayOrEmpty(node.actors)),append_1(block, [blockHead, grid]),block);
      nodes.appendChild(_1);
    }, arrayOrEmpty(actorSnapshot.nodes)));
    return setStatus(status, "Loaded "+String(actorSnapshot.nodeCount)+" "+String(source)+" node(s), "+String(actorSnapshot.actorCount)+" actor(s)");
  };
  const load=() => {
    getJson("/actors/api/snapshot", (data) => {
      writeSnapshotWithWatermark(cacheKey_1, data, data.maxSequence, actorValueCount(data), "actors-snapshot");
      applySnapshot("backend", data);
    }, (t) => {
      setStatus(status, t);
    });
    getJson("/actors/api/tree", (data) => {
      renderActorTree("backend", data);
    }, (error) => {
      clear(treePanel);
      treePanel.appendChild(element_1("div", "empty", "ActorTree unavailable: "+error));
    });
  };
  const setWsState=(value) => {
    setData("ws-state", value, page);
  };
  function flushSyncFrames(socket){
    if(Equals(socket.readyState, 1)){
      const frames=queuedSyncFrames;
      queuedSyncFrames=[];
      iter((frame) => {
        socket.send(frame);
      }, frames);
    }
  }
  function ensureSyncSocket(){
    let _1, _2;
    if(syncSocket!=null&&syncSocket.$==1){
      const socket=syncSocket.$0;
      _1=(Equals(socket.readyState, 1)||Equals(socket.readyState, 0))&&(_2=syncSocket.$0,true);
    }
    else _1=false;
    if(_1)return _2;
    else {
      setWsState("connecting");
      const socket_1=new WebSocket(syncWebSocketUrl());
      syncSocket=Some(socket_1);
      socket_1.onopen=() => {
        setWsState("open");
        return flushSyncFrames(socket_1);
      };
      socket_1.onmessage=(event) => handleSyncMessage(String(event.data));
      socket_1.onerror=() => {
        setWsState("error");
        return setStatus(status, "WebSocket actors sync error");
      };
      socket_1.onclose=() => {
        syncSocket=null;
        subscribedRegistry=false;
        registryTailRequested=false;
        return setWsState("closed");
      };
      return socket_1;
    }
  }
  function sendSyncFrame(frame){
    while(true)
      {
        const socket=ensureSyncSocket();
        return Equals(socket.readyState, 1)?socket.send(frame):void(queuedSyncFrames=queuedSyncFrames.concat([frame]));
      }
  }
  function subscribeRegistry(){
    if(!subscribedRegistry){
      subscribedRegistry=true;
      setWsState("subscribing");
      const streamKey=actorRegistryStreamKey();
      sendSyncFrame(JSON.stringify(New_1("subscribe", newRequestId("actors-subscribe"), streamKey)));
    }
  }
  function requestRegistryTail(){
    if(!registryTailRequested){
      registryTailRequested=true;
      sendSyncFrame(JSON.stringify(New_2("read-tail", newRequestId("actors-read-tail"), actorRegistryStreamKey(), defaultRenderLimit())));
    }
  }
  function handleSyncEvent(event){
    if(!(event==null)&&asText_1(event.sourceKind).toLowerCase()=="actor.registered"){
      let x, updatedNode;
      if(event==null||isBlank_1(event.payload))x=null;
      else try {
        const wire=json(event.payload);
        x=wire==null||asText_1(wire.schema)!="ptc.comm.spa.actor.registration.v1"?null:Some(wire);
      }
      catch(m_1){
        x=null;
      }
      if(x==null)void 0;
      else {
        const _1=x.$0;
        const nodeId=asText_1(_1.nodeId);
        const nodeAddress=asText_1(_1.nodeAddress);
        const actorId=asText_1(_1.actorId);
        if(!isBlank_1(nodeId)&&!isBlank_1(actorId)){
          const tags=arrayOrEmpty(_1.tags);
          const roles=arrayOrEmpty(_1.roles);
          const actor=New_22(actorId, textOr(actorId, _1.displayName), textOr("actor", _1.kind), [nodeId, actorId].concat(tags), textOr("running", _1.status), arrayOrEmpty(_1.routees));
          const m=tryFind((node) => sameText(node.nodeId, nodeId), arrayOrEmpty(actorSnapshot.nodes));
          if(m==null)updatedNode=New_23(nodeId, nodeAddress, "up", roles, [actor]);
          else {
            const existing=m.$0;
            const actors=sortBy((row) => asText_1(row.actorId), filter((row) =>!sameText(row.actorId, actorId), arrayOrEmpty(existing.actors)).concat([actor]));
            updatedNode=New_23(existing.nodeId, isBlank_1(nodeAddress)?asText_1(existing.nodeAddress):nodeAddress, textOr("up", existing.status), length(roles)===0?arrayOrEmpty(existing.roles):roles, actors);
          }
          const nodes_1=sortBy((node) => asText_1(node.nodeId), filter((node) =>!sameText(node.nodeId, nodeId), arrayOrEmpty(actorSnapshot.nodes)).concat([updatedNode]));
          let _2=length(nodes_1);
          let _3=fold((_5, _6) => _5+_6, 0, map((node) => arrayOrEmpty(node.actors).length, nodes_1));
          const a=actorSnapshot.maxSequence;
          const b=event.sequence;
          let _4=Compare(a, b)===1?a:b;
          actorSnapshot=New_21(_2, _3, _4, nodes_1);
          writeSnapshotWithWatermark(cacheKey_1, actorSnapshot, actorSnapshot.maxSequence, actorValueCount(actorSnapshot), "actors-snapshot");
          applySnapshot("synced", actorSnapshot);
          setStatus(status, "Synced actor "+actorId);
        }
        else void 0;
      }
    }
  }
  function handleSyncMessage(text){
    try {
      const response=json(text);
      const responseType=asText_1(response.type).toLowerCase();
      const responseStatus=asText_1(response.status).toLowerCase();
      switch(responseStatus=="ok"?responseType=="subscribe"?0:responseType=="stream-event"?1:responseType=="read-tail"?2:responseType=="read"?2:responseType=="tail"?2:4:responseStatus=="error"?3:4){
        case 0:
          setWsState("subscribed");
          break;
        case 1:
          handleSyncEvent(response.event);
          break;
        case 2:
          iter(handleSyncEvent, arrayOrEmpty(response.events));
          break;
        case 3:
          setStatus(status, "WebSocket actors sync error: "+asText_1(response.error));
          break;
        case 4:
          null;
          break;
      }
    }
    catch(error){
      setStatus(status, "WebSocket actors sync parse failed: "+errorMessage(error));
    }
  }
  reload.addEventListener("click", load);
  load();
  subscribeRegistry();
}
function mountChat(page){
  let selected, cursor, polling, participants, replayingPending, chatSocket, queuedChatSyncFrames, subscribedChatStream, pendingWsChatIds;
  selected="";
  cursor="";
  polling=false;
  participants=[];
  const participantId=currentUserId();
  page.className="page chat-grid";
  const side=element_1("aside", "sidebar", null);
  const sideHead=element_1("div", "panel-head", null);
  const reload=button_1("", "Reload");
  const list=element_1("div", "list", null);
  append_1(sideHead, [element_1("h1", "", "Chat"), reload]);
  append_1(side, [sideHead, element_1("div", "", null), list]);
  const work=setTestId_1("chat-work", element_1("section", "work", null));
  const workHead=element_1("div", "work-head", null);
  const titleBox=element_1("div", "", null);
  const toTitle=element_1("h2", "", "No participant selected");
  const state=element_1("div", "state", "Loading participants");
  const pendingState=setTestId_1("chat-pending-state", element_1("div", "state pending-state", ""));
  const thread=setTestId_1("thread-list", setId("thread-list", element_1("div", "thread-list", null)));
  const composer=setTestId_1("chat-composer", element_1("div", "chat-composer", null));
  const draft=setTestId_1("chat-draft", textarea_1("draft", "Type a message"));
  const actions=element_1("div", "actions", null);
  const send=setTestId_1("chat-send", button_1("primary", "Send"));
  const participantsCacheKey=cacheKey("chat-agents", ofArray([participantId]));
  const threadCacheKey=(peerId) => cacheKey("chat-thread", ofArray([participantId, peerId]));
  append_1(titleBox, [element_1("label", "", "To"), toTitle]);
  append_1(workHead, [titleBox, state]);
  append_1(actions, [send]);
  append_1(composer, [draft, actions]);
  append_1(work, [workHead, pendingState, thread, composer]);
  append_1(page, [side, work]);
  const sameText=(left, right) => asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
  const isPendingForThisChat=(command) =>!(command==null)&&sameText(command.kind, "chat-send")&&StartsWith(asText_1(command.target), participantId+"->");
  replayingPending=false;
  chatSocket=null;
  queuedChatSyncFrames=[];
  subscribedChatStream="";
  pendingWsChatIds=[];
  const setChatWsState=(value) => {
    setData("ws-state", value, work);
  };
  const chatStreamKey=(peerId) => New_4("", "set", "chat", sameText(peerId, "channel.public")?["channel:public"]:[participantId, peerId]);
  const streamIdentity=(streamKey) => concat_1("\n", [asText_1(streamKey.pageId), asText_1(streamKey.mode), asText_1(streamKey.setName), concat_1("\u001f", arrayOrEmpty(streamKey.keys))]);
  function renderParticipants(){
    let _1;
    clear(list);
    iter((p_1) => {
      const className=p_1.participantId==selected?"list-card active":"list-card";
      const name=textOr(p_1.participantId, p_1.displayName);
      const line=asText_1(p_1.kind)+" / "+joinValues(p_1.labels);
      const item=button_1(className, null);
      setData("participant-id", p_1.participantId, setTestId_1("chat-participant", item));
      item.appendChild(cardTitle(name, p_1.participantId, p_1.status, line));
      item.addEventListener("click", () => {
        selected=p_1.participantId;
        cursor="";
        clear(thread);
        renderParticipants();
        refreshChatPendingState();
        pollThread(true);
        return ensureSelectedChatSubscription();
      });
      list.appendChild(item);
    }, participants);
    const current=tryFind((p_1) => p_1.participantId==selected, participants);
    if(current==null)_1="No participant selected";
    else {
      const p=current.$0;
      _1=textOr(p.participantId, p.displayName)+" ("+p.participantId+")";
    }
    toTitle.textContent=_1;
  }
  function appendMessages(messages){
    iter((message) => {
      if(!(message==null)&&!isBlank_1(message.messageId)&&doc_1().getElementById("thread-"+message.messageId)==null){
        const outbound=message.fromId==participantId;
        const wrap=setId("thread-"+message.messageId, element_1("div", outbound?"message outbound":"message inbound", null));
        setData("message-id", message.messageId, setTestId_1("chat-message", wrap));
        const meta=element_1("div", "message-meta", null);
        const route=message.scope=="public"?outbound?"You -> Public":asText_1(message.fromId)+" -> Public":outbound?"You -> "+asText_1(message.toId):asText_1(message.fromId)+" -> You";
        const idNode=setData("full-message-id", message.messageId, element_1("span", "message-id", compactMessageId(message.messageId)+"  "+asText_1(message.createdAtUtc)));
        idNode.setAttribute("title", message.messageId+"  "+asText_1(message.createdAtUtc));
        append_1(meta, [element_1("span", "", route), idNode]);
        append_1(wrap, [meta, element_1("pre", "message-body", asText_1(message.body))]);
        thread.appendChild(wrap);
      }
    }, arrayOrEmpty(messages));
    scrollToBottomAfterRender(thread);
  }
  function loadParticipants(){
    setStatus(state, "Loading participants");
    readJson(participantsCacheKey, (a) => {
      if(a!=null&&a.$==1)if(a.$0,length(participants)===0){
        participants=arrayOrEmpty(a.$0.participants);
        isBlank_1(selected)&&length(participants)>0?selected=get(participants, 0).participantId:void 0;
        renderParticipants();
        setStatus(state, "Loaded "+String(length(participants))+" cached participant(s)");
        pollThread(true);
        ensureSelectedChatSubscription();
        replayPendingChatCommands();
      }
    });
    getJson("/chat/api/agents", (data) => {
      participants=arrayOrEmpty(data.participants);
      writeSnapshotWithWatermark(participantsCacheKey, data, 0n, length(participants), "chat-agents");
      isBlank_1(selected)&&length(participants)>0?selected=get(participants, 0).participantId:void 0;
      renderParticipants();
      setStatus(state, "Loaded "+String(length(participants))+" participant(s)");
      pollThread(true);
      ensureSelectedChatSubscription();
      replayPendingChatCommands();
    }, (t) => {
      setStatus(state, t);
    });
  }
  function pollThread(force){
    if(!isBlank_1(selected)&&!polling){
      polling=true;
      const cacheKey_1=threadCacheKey(selected);
      const fetchThread=(useCursor) => {
        let url;
        url="/chat/api/thread?participantId="+encodeURIComponent(participantId)+"&peerId="+encodeURIComponent(selected);
        if(useCursor&&!isBlank_1(cursor))url=url+"&afterMessageId="+encodeURIComponent(cursor);
        getJson(url, (data) => {
          const messages=force&&!useCursor?latestArray(defaultRenderLimit(), data.messages):arrayOrEmpty(data.messages);
          appendMessages(messages);
          if(!isBlank_1(data.nextAfterMessageId))cursor=data.nextAfterMessageId;
          readJson(cacheKey_1, (cached) => {
            let _1, _2;
            switch(cached!=null&&cached.$==1?(cached.$0,useCursor?(_1=cached.$0,0):(cached.$0,!force?(_1=cached.$0,1):2)):2){
              case 0:
                _2=_1.messages;
                break;
              case 1:
                _2=_1.messages;
                break;
              case 2:
                _2=[];
                break;
            }
            const merged=mergeThreadMessages(_2, messages);
            const nextAfterMessageId=textOr(cursor, data.nextAfterMessageId);
            readWatermark(cacheKey_1, (watermark) => {
              const a=watermark==null?0n:int64OrZero(watermark.$0.newestSequence);
              const b=maxMessageSequence(merged);
              let _3=Compare(a, b)===1?a:b;
              writeSnapshotWithWatermark(cacheKey_1, New_25(merged, nextAfterMessageId), _3, length(merged), "chat-thread");
            });
          });
          setStatus(state, String(useCursor?"Synced":"Loaded")+" "+String(length(messages))+" backend message(s)");
          polling=false;
        }, (error) => {
          setStatus(state, error);
          polling=false;
        });
      };
      if(force)readJson(cacheKey_1, (a) => {
        if(a==null)fetchThread(false);
        else {
          const cached=a.$0;
          const messages=latestArray(defaultRenderLimit(), cached.messages);
          appendMessages(messages);
          if(!isBlank_1(cached.nextAfterMessageId))cursor=cached.nextAfterMessageId;
          setStatus(state, "Loaded "+String(length(messages))+" cached message(s); syncing missing tail");
          fetchThread(!isBlank_1(cursor));
        }
      });
      else fetchThread(!isBlank_1(cursor));
    }
  }
  function refreshChatPendingState(){
    readPendingRealitySplit((_1, _2) => renderPendingInspection(pendingState, filter(isPendingForThisChat, _1), filter(isPendingForThisChat, _2)));
  }
  function replayPendingChatCommands(){
    if(!replayingPending){
      replayingPending=true;
      readAllPending((commands) => {
        let remaining, accepted;
        const mine=filter((command) => sameText(command.method, "POST")&&!isBlank_1(command.url)&&!isBlank_1(command.payloadJson), filter(isPendingForThisChat, commands));
        if(length(mine)===0){
          replayingPending=false;
          refreshChatPendingState();
        }
        else {
          remaining=length(mine);
          accepted=0;
          setStatus(pendingState, "Replaying "+String(length(mine))+" pending command(s)");
          const finishOne=() => {
            remaining=remaining-1;
            remaining===0?(replayingPending=false,refreshChatPendingState(),accepted>0?(setStatus(state, "Replayed "+String(accepted)+" pending chat command(s)"),cursor="",polling=false,pollThread(true)):void 0):void 0;
          };
          iter((command) => {
            postJsonText(command.url, command.payloadJson, (responseBody) => {
              try {
                const reply=json(isBlank_1(responseBody)?"{}":responseBody);
                if(!(reply.message==null)&&!isBlank_1(reply.message.messageId))deletePendingThen(command.commandId, () => {
                  accepted=accepted+1;
                  appendMessages([reply.message]);
                  cacheAcceptedChatMessage(int64OrZero(reply.streamSequence), reply.message);
                  finishOne();
                });
                else finishOne();
              }
              catch(m){
                finishOne();
              }
            }, () => {
              finishOne();
            });
          }, mine);
        }
      });
    }
  }
  function cacheAcceptedChatMessage(sequence, message){
    if(!(message==null)&&!isBlank_1(message.messageId)&&!isBlank_1(selected)){
      const cacheKey_1=threadCacheKey(selected);
      return readJson(cacheKey_1, (cached) => {
        const merged=mergeThreadMessages(cached==null?[]:cached.$0.messages, [message]);
        writeSnapshotWithWatermark(cacheKey_1, New_25(merged, message.messageId), sequence>0n?sequence:maxMessageSequence(merged), length(merged), "chat-thread");
      });
    }
    else return null;
  }
  function handleChatSyncMessage(text){
    try {
      let o;
      const response=json(text);
      const responseType=asText_1(response.type).toLowerCase();
      const responseStatus=asText_1(response.status).toLowerCase();
      const requestId=asText_1(response.requestId);
      if(responseStatus=="ok"){
        if(responseType=="subscribe")setChatWsState("subscribed");
        else if(responseType=="chat-send"){
          exists((id) => id==requestId, pendingWsChatIds)?(pendingWsChatIds=filter((id) => id!=requestId, pendingWsChatIds),deletePendingThen(requestId, () => {
            refreshChatPendingState();
            draft.value="";
          })):void 0;
          !(response.message==null)&&!isBlank_1(response.message.messageId)?(appendMessages([response.message]),cacheAcceptedChatMessage(response.event==null?0n:response.event.sequence, response.message),cursor=response.message.messageId):void 0;
          setStatus(state, "Sent "+textOr("message", response.message==null?"":response.message.messageId)+" "+asText_1(response.deliveryHint));
        }
        else if(responseType=="stream-event"){
          const event=response.event;
          if(!isBlank_1(selected)&&!(event==null)&&!(event.streamKey==null)&&streamIdentity(event.streamKey)==streamIdentity(chatStreamKey(selected))){
            const event_1=response.event;
            if(event_1==null||isBlank_1(event_1.payload))o=null;
            else try {
              const message=json(event_1.payload);
              o=message==null||isBlank_1(message.messageId)?null:Some(message);
            }
            catch(m){
              o=Some(New_24(textOr(event_1.eventId, event_1.sourceId), "", participantId, "direct", asText_1(event_1.payload), asText_1(event_1.createdAtUtc)));
            }
            if(o==null)null;
            else {
              const message_1=o.$0;
              appendMessages([message_1]);
              cacheAcceptedChatMessage(response.event.sequence, message_1);
              cursor=message_1.messageId;
              setStatus(state, "Synced chat event "+message_1.messageId);
            }
          }
          else null;
        }
        else null;
      }
      else responseStatus=="error"?exists((id) => id==requestId, pendingWsChatIds)?(setStatus(state, pendingFailure("WebSocket chat send", asText_1(response.error))),refreshChatPendingState()):setStatus(state, "WebSocket chat error: "+asText_1(response.error)):null;
    }
    catch(error){
      setStatus(state, "WebSocket chat parse failed: "+errorMessage(error));
    }
  }
  function flushChatSyncFrames(socket){
    if(Equals(socket.readyState, 1)){
      const frames=queuedChatSyncFrames;
      queuedChatSyncFrames=[];
      iter((frame) => {
        socket.send(frame);
      }, frames);
    }
  }
  function ensureChatSyncSocket(){
    let _1, _2;
    if(chatSocket!=null&&chatSocket.$==1){
      const socket=chatSocket.$0;
      _1=(Equals(socket.readyState, 1)||Equals(socket.readyState, 0))&&(_2=chatSocket.$0,true);
    }
    else _1=false;
    if(_1)return _2;
    else {
      setChatWsState("connecting");
      const socket_1=new WebSocket(syncWebSocketUrl());
      chatSocket=Some(socket_1);
      socket_1.onopen=() => {
        setChatWsState("open");
        return flushChatSyncFrames(socket_1);
      };
      socket_1.onmessage=(event) => handleChatSyncMessage(String(event.data));
      socket_1.onerror=() => {
        setChatWsState("error");
        return setStatus(state, "WebSocket chat error; pending command remains replayable");
      };
      socket_1.onclose=() => {
        chatSocket=null;
        subscribedChatStream="";
        return setChatWsState("closed");
      };
      return socket_1;
    }
  }
  function sendChatSyncFrame(frame){
    while(true)
      {
        const socket=ensureChatSyncSocket();
        return Equals(socket.readyState, 1)?socket.send(frame):void(queuedChatSyncFrames=queuedChatSyncFrames.concat([frame]));
      }
  }
  function ensureSelectedChatSubscription(){
    if(!isBlank_1(selected)){
      const streamKey=chatStreamKey(selected);
      const identity=streamIdentity(streamKey);
      if(!isBlank_1(identity)&&identity!=subscribedChatStream){
        subscribedChatStream=identity;
        setChatWsState("subscribing");
        sendChatSyncFrame(JSON.stringify(New_1("subscribe", newRequestId("chat-subscribe"), streamKey)));
      }
    }
  }
  function sendMessage(){
    const body=Trim(draft.value);
    if(isBlank_1(selected))setStatus(state, "Select a participant first");
    else if(isBlank_1(body))setStatus(state, "Message is empty");
    else {
      const request=New_28(participantId, selected, body, ["web-chat"]);
      const pendingId=rememberPending("chat-send", participantId+"->"+selected, "/chat/api/send", request);
      const wsRequest=New_27("chat-send", pendingId, participantId, selected, body, ["web-chat"], participantId, "chat");
      pendingWsChatIds=pendingWsChatIds.concat([pendingId]);
      refreshChatPendingState();
      setStatus(state, "Sending through WebSocket; pending command saved in browser DB");
      sendChatSyncFrame(JSON.stringify(wsRequest));
      scrollToBottomAfterRender(thread);
    }
  }
  reload.addEventListener("click", loadParticipants);
  send.addEventListener("click", sendMessage);
  draft.addEventListener("keydown", (event) => event.key=="Enter"&&!event.shiftKey?(event.preventDefault(),sendMessage()):null);
  globalThis.setInterval(() => pollThread(false), 2500);
  refreshChatPendingState();
  loadParticipants();
}
function mountUnknownPage(page, path){
  page.className="page actors-page";
  page.appendChild(element_1("div", "empty", "No append page is registered for "+String(path)+"."));
}
function refreshAppendNav(activePath){
  const applyDefinitions=(data) => {
    const nav=doc_1().getElementById("ptc-nav");
    if(!(nav==null))renderNav(nav, activePath, arrayOrEmpty(data.pages));
  };
  readJson(appendPagesDefinitionsCacheKey(), (a) => {
    if(a==null){ }
    else applyDefinitions(a.$0);
  });
  getJson("/pages/api/definitions", (data) => {
    writeAppendPagesDefinitions(data);
    applyDefinitions(data);
  }, () => { });
}
function tryMountLoginWithRegisteredRenderers(root, configJson){
  let r;
  const _1=root;
  const _2=configJson;
  if(!(globalThis.PulseTrade&&globalThis.PulseTrade.LoginRenderers))return false;
  let renderers=globalThis.PulseTrade.LoginRenderers;
  for(let i=0;i<renderers.length;i++){
    let r_1=renderers[i];
    try {
      let value=(r_1.render||r_1[1])(_1, _2);
      if(value===true)return true;
    }
    catch(e){
      console.error("Login renderer exception:", e);
    }
  }
  return false;
}
function mountLoginFallback(root){
  const config=loginConfig();
  const frame=element_1("section", "login-frame", null);
  frame.setAttribute("aria-label", "PTCS Login");
  const systemPanel=element_1("aside", "system-panel", null);
  const brand=element_1("div", "", null);
  append_1(brand, [element_1("div", "brand-mark", "PT"), element_1("p", "brand-title", "PulseTrade Comm Spa"), element_1("p", "brand-subtitle", "\u672c\u9801\u793a\u610f PTCS.Login provider \u7684\u81ea\u6709\u767b\u5165\u5165\u53e3\u3002\u767b\u5165\u6210\u529f\u5f8c\u7531 server \u8a2d\u5b9a HttpOnly session cookie\uff0c\u518d\u56de\u5230\u53d7\u4fdd\u8b77\u7684 PTCS \u9801\u9762\u3002")]);
  const routes=element_1("ul", "route-list", null);
  routes.setAttribute("aria-label", "Login context");
  append_1(routes, [routeItem("P", "Protected route", textOr("/actors", config.protectedRoute)), routeItem("S", "Session cookie", textOr("ptc_login_session", config.sessionCookieName)), routeItem("A", "ACL mode", textOr("enabled or authenticated-only", config.aclLabel))]);
  append_1(systemPanel, [brand, routes]);
  const formPanel=element_1("section", "form-panel", null);
  const card=element_1("div", "form-card", null);
  const statusRow=element_1("div", "status-row", null);
  const providerPill=element_1("span", "pill", null);
  const bypassPill=element_1("span", "pill", null);
  append_1(providerPill, [element_1("span", "dot", ""), doc_1().createTextNode(textOr("PTCS.Login", config.providerLabel))]);
  append_1(bypassPill, [element_1("span", "dot warn", ""), doc_1().createTextNode("OAuth bypass")]);
  append_1(statusRow, [providerPill, bypassPill]);
  const errorBox=setTestId_1("ptcs-login-error", element_1("p", "error-box", "\u767b\u5165\u5931\u6557\u3002\u8acb\u78ba\u8a8d\u5e33\u865f\u6216\u5bc6\u78bc\u3002"));
  errorBox.setAttribute("role", "alert");
  const userName=setTestId_1("ptcs-login-username", setId("username", input_1("admin")));
  userName.setAttribute("name", "username");
  userName.setAttribute("type", "text");
  userName.setAttribute("autocomplete", "username");
  const password=setTestId_1("ptcs-login-password", setId("password", input_1("\u8f38\u5165\u5bc6\u78bc")));
  password.setAttribute("name", "password");
  password.setAttribute("type", "password");
  password.setAttribute("autocomplete", "current-password");
  const keepSession=doc_1().createElement("input");
  keepSession.setAttribute("name", "keepSession");
  keepSession.setAttribute("type", "checkbox");
  keepSession.setAttribute("value", "true");
  const inlineRow=element_1("div", "inline-row", null);
  const checkboxLabel=element_1("label", "checkbox-row", null);
  append_1(checkboxLabel, [keepSession, doc_1().createTextNode("\u4fdd\u6301\u6b64\u700f\u89bd\u5668\u767b\u5165")]);
  append_1(inlineRow, [checkboxLabel, setHref("/login/help", element_1("a", "link", "\u9700\u8981\u5354\u52a9?"))]);
  const form=element_1("form", "", null);
  form.setAttribute("method", "post");
  form.setAttribute("action", config.submitPath);
  const submit=setTestId_1("ptcs-login-submit", button_1("", "\u767b\u5165\u4e26\u8fd4\u56de PTCS"));
  const setError=(text) => {
    errorBox.textContent=textOr("\u767b\u5165\u5931\u6557\u3002\u8acb\u78ba\u8a8d\u5e33\u865f\u6216\u5bc6\u78bc\u3002", text);
    errorBox.className="error-box visible";
  };
  const submitLogin=() => {
    const request=New_33(Trim(userName.value), password.value, config.returnUrl, keepSession.checked);
    if(isBlank_1(request.userName)||isBlank_1(request.password))setError("\u8acb\u8f38\u5165\u5e33\u865f\u8207\u5bc6\u78bc\u3002");
    else {
      errorBox.className="error-box";
      submit.setAttribute("disabled", "disabled");
      submit.textContent="\u767b\u5165\u4e2d";
      postJson(config.submitPath, request, (reply) => {
        const target=textOr(config.returnUrl, reply.returnUrl);
        globalThis.location.assign(target);
      }, (error) => {
        submit.removeAttribute("disabled");
        submit.textContent="\u767b\u5165\u4e26\u8fd4\u56de PTCS";
        setError(isBlank_1(error)?"\u767b\u5165\u5931\u6557\u3002\u8acb\u78ba\u8a8d\u5e33\u865f\u6216\u5bc6\u78bc\u3002":error);
      });
    }
  };
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    return submitLogin();
  });
  submit.addEventListener("click", submitLogin);
  append_1(form, [field_1("\u5e33\u865f", "username", userName), field_1("\u5bc6\u78bc", "password", password), inlineRow, submit]);
  append_1(card, [statusRow, element_1("h1", "", textOr("\u767b\u5165 PTCS", config.title)), element_1("p", "lead", textOr("\u4f7f\u7528 host \u63d0\u4f9b\u7684\u5e33\u865f\u767b\u5165\u3002\u6b0a\u9650\u7531\u767b\u5165\u5f8c\u53d6\u5f97\u7684 principal \u8207 ACL policy \u6c7a\u5b9a\u3002", config.lead)), errorBox, form, element_1("p", "footer-note", "Browser flow \u61c9\u53ea\u56de HttpOnly cookie\uff1bheadless/API/WS \u624d\u4f7f\u7528 bearer token\u3002\u63d0\u4ea4\u7aef\u9ede\u793a\u610f\u70ba /login/api/submit\u3002")]);
  append_1(formPanel, [card]);
  append_1(frame, [systemPanel, formPanel]);
  clear(root);
  root.appendChild(frame);
}
function loginConfig(){
  const node=doc_1().getElementById("ptcs-login-config");
  return node==null||isBlank_1(node.textContent)?New_32("/login/api/submit", "/login/api/session", "/login/logout", "/actors", "/actors", "ptc_login_session", "\u767b\u5165 PTCS", "\u4f7f\u7528 host \u63d0\u4f9b\u7684\u5e33\u865f\u767b\u5165\u3002\u6b0a\u9650\u7531\u767b\u5165\u5f8c\u53d6\u5f97\u7684 principal \u8207 ACL policy \u6c7a\u5b9a\u3002", "PTCS.Login", "ACL mode"):json(node.textContent);
}
function textOr(fallback, value){
  return isBlank_1(value)?fallback:value;
}
function pageDefinitionFromWire(wire){
  if(wire==null||asText_1(wire.schema)!="ptc.comm.spa.append-page.definition.v1"||isBlank_1(wire.pageId))return null;
  else {
    const pageId=asText_1(wire.pageId);
    return Some(New_3(pageId, textOr(pageId, wire.tabId), textOr("/page/"+pageId, wire.path), textOr(pageId, wire.title), textOr(pageId, wire.setName), textOr("raw", wire.shape), asText_1(wire.description), textOr("\"Aster\"", wire.keyPlaceholder), textOr("JSON value", wire.valuePlaceholder), asText_1(wire.defaultKey), arrayOrEmpty(wire.tags)));
  }
}
function hiddenPageFromWire(wire){
  if(wire==null||asText_1(wire.schema)!="ptc.comm.spa.append-page.hidden.v1"||isBlank_1(wire.pageId))return null;
  else {
    const pageId=asText_1(wire.pageId);
    return Some([pageId, textOr(pageId, wire.tabId)]);
  }
}
function sameTextInvariant_1(left, right){
  return asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
}
function sortAppendPages(pages){
  return sortBy((page) =>[asText_1(page.title).toLowerCase(), asText_1(page.pageId).toLowerCase()], arrayOrEmpty(pages));
}
function writeSnapshotWithWatermark(cacheKey_1, value, newestSequence, cachedCount, source){
  writeJson(cacheKey_1, value);
  writeWatermark(cacheKey_1, newestSequence, cachedCount, source);
}
function set_requestSeq(_1){
  _c.requestSeq=_1;
}
function requestSeq(){
  return _c.requestSeq;
}
function requestOptions(){
  return{credentials:"same-origin"};
}
function errorMessage(error){
  return error==null?"request failed":String(error);
}
function isCurrentPage(activePath, href){
  return TrimEnd(activePath, ["/"])==TrimEnd(href, ["/"]);
}
function pagePath(page){
  const pageId=asText_1(page.pageId);
  const path=asText_1(page.path);
  return exists((alias) => sameTextInvariant_1(path, alias), ["/fcell-chat", "/fcell-list", "/fcell-grid"])?path:"/page/"+pageId;
}
function setTestId_1(id, node){
  !isBlank_1(id)?node.setAttribute("data-testid", id):void 0;
  return node;
}
function defaultRenderLimit(){
  return _c.defaultRenderLimit;
}
function element_1(tag, className, textValue){
  const node=doc_1().createElement(tag);
  if(!isBlank_1(className))node.className=className;
  if(!(textValue==null))node.textContent=textValue;
  return node;
}
function button_1(className, text){
  const node=element_1("button", className, text);
  node.setAttribute("type", "button");
  return node;
}
function input_1(placeholder){
  const node=doc_1().createElement("input");
  node.placeholder=placeholder;
  return node;
}
function textarea_1(className, placeholder){
  const node=doc_1().createElement("textarea");
  node.className=className;
  node.placeholder=placeholder;
  return node;
}
function pageAclAllows(pageId, action){
  return aclAllows(action, "ptcs.page", pageId);
}
function setHidden(hidden, node){
  hidden?node.setAttribute("hidden", "hidden"):node.removeAttribute("hidden");
  return node;
}
function append_1(parent, children){
  for(let i=0, _1=children.length-1;i<=_1;i++)parent.appendChild(get(children, i));
  return parent;
}
function actorArguButtonLabel(page){
  return isActorArguPage(page)?"Tell":"Append";
}
function pageTitle(page){
  return textOr(asText_1(page.pageId), asText_1(page.title));
}
function pageTypeLabel(page){
  const shapeText=asText_1(page.shape).toLowerCase();
  if(isActorArguPage(page)){
    if(shapeText=="fcell-chat")return"Actor Argu";
    else if(shapeText=="actor-argu")return"Actor Argu";
    else if(shapeText=="raw")return"Raw Actor Argu";
    else {
      const m=findAppendPageShape(page.shape);
      return m==null?"Actor Argu":textOr("Actor Argu", m.$0.label);
    }
  }
  else {
    const m_1=findAppendPageShape(page.shape);
    if(m_1==null)return"Raw";
    else {
      const shape=m_1.$0;
      return textOr(normalizeShapeText(page.shape), shape.label);
    }
  }
}
function isActorArguPage(page){
  return hasTag("actor-argu", page.tags);
}
function currentUserId(){
  return currentBrowserUser().participantId;
}
function renderPendingInspection(node, commands, foreignCommands){
  let _1, shown, shown_1;
  const commands_1=arrayOrEmpty(commands);
  const foreignCommands_1=arrayOrEmpty(foreignCommands);
  node.setAttribute("data-pending-count", String(length(commands_1)));
  node.setAttribute("data-foreign-pending-count", String(length(foreignCommands_1)));
  node.setAttribute("data-foreign-pending-realities", concat_1(",", distinct(map((command) => asText_1(command.serverRealityId), foreignCommands_1))));
  node.setAttribute("data-pending-kinds", concat_1(",", map((a) => a.kind, commands_1)));
  node.setAttribute("data-pending-targets", concat_1("\n", map((a) => a.target, commands_1)));
  node.setAttribute("data-pending-urls", concat_1("\n", map((a) => a.url, commands_1)));
  node.setAttribute("data-pending-statuses", concat_1(",", map((a) => a.status, commands_1)));
  clear(node);
  if(length(commands_1)>0){
    node.appendChild(element_1("div", "strong", "Pending commands: "+String(length(commands_1))));
    const list=setTestId_1("pending-command-list", element_1("div", "pending-inspection-list", null));
    _1=(shown=0,iter((command) => {
      if(shown<4){
        shown=shown+1;
        const row=setData("pending-status", command.status, setData("pending-url", command.url, setData("pending-target", command.target, setData("pending-kind", command.kind, setTestId_1("pending-command-row", element_1("div", "pending-command-row wrap", null))))));
        append_1(row, [element_1("span", "strong pending-command-kind", asText_1(command.kind)), element_1("span", "muted wrap pending-command-target", asText_1(command.target)), element_1("span", "meta wrap pending-command-status", String(asText_1(command.method))+" "+String(asText_1(command.url))+" / "+String(asText_1(command.status)))]);
        list.appendChild(row);
      }
    }, commands_1),length(commands_1)>shown?list.appendChild(element_1("div", "meta", "+"+String(length(commands_1)-shown)+" more pending command(s)")):void 0,node.appendChild(list));
  }
  else _1=void 0;
  if(length(foreignCommands_1)>0){
    node.appendChild(setData("foreign-pending-count", String(length(foreignCommands_1)), setTestId_1("foreign-pending-summary", element_1("div", "pending-foreign-summary meta", "Foreign pending blocked/stale: "+String(length(foreignCommands_1))))));
    const list_1=setTestId_1("foreign-pending-list", element_1("div", "pending-foreign-list", null));
    shown_1=0;
    iter((command) => {
      if(shown_1<3){
        shown_1=shown_1+1;
        const x=setTestId_1("foreign-pending-row", element_1("div", "pending-command-row pending-command-foreign wrap", null));
        let _2=setData("pending-reality", asText_1(command.serverRealityId), x);
        let _3=setData("pending-kind", command.kind, _2);
        const row=setData("pending-target", command.target, _3);
        append_1(row, [element_1("span", "strong pending-command-kind", asText_1(command.kind)), element_1("span", "muted wrap pending-command-target", asText_1(command.target)), element_1("span", "meta wrap pending-command-status", "blocked/stale / "+asText_1(command.serverRealityId))]);
        list_1.appendChild(row);
      }
    }, foreignCommands_1);
    if(length(foreignCommands_1)>shown_1)list_1.appendChild(element_1("div", "meta", "+"+String(length(foreignCommands_1)-shown_1)+" more foreign pending command(s)"));
    node.appendChild(list_1);
  }
  else void 0;
}
function appendPageValueCount(snapshot){
  return snapshot==null?0:fold((_1, _2) => _1+_2, 0, map((bucket) => {
    if(bucket==null)return 0;
    else {
      const a=bucket.valueCount;
      const b=length(arrayOrEmpty(bucket.values));
      return Compare(a, b)===1?a:b;
    }
  }, arrayOrEmpty(snapshot.buckets)));
}
function keysAsJson(keys){
  const keys_1=arrayOrEmpty(keys);
  return length(keys_1)===1?JSON.stringify(get(keys_1, 0)):JSON.stringify(keys_1);
}
function joinValues(values){
  const values_1=arrayOrEmpty(values);
  return length(values_1)===0?"":concat_1(" / ", values_1);
}
function latestArray(limit, values){
  const values_1=arrayOrEmpty(values);
  return length(values_1)<=limit?values_1:skip(length(values_1)-limit, values_1);
}
function renderAppendValue(value){
  let _1;
  const mode=asText_1(value.mode);
  const m=mode.toLowerCase();
  const className=m=="inbound-message"?"fcell-card fcell-chat inbound":m=="outbound-message"?"fcell-card fcell-chat outbound":m=="list"?"fcell-card fcell-list":m=="grid"?"fcell-card fcell-grid":"fcell-card";
  const card=setData("mode", mode, setTestId_1("append-value-card", element_1("div", className, null)));
  const head_1=element_1("div", "fcell-head", null);
  append_1(head_1, [element_1("span", "fcell-pill", fcellValueModeLabel(mode, value.tags)), element_1("span", "muted wrap", asText_1(value.valueId)+" / "+asText_1(value.createdAtUtc))]);
  card.appendChild(head_1);
  const m_1=mode.toLowerCase();
  switch(m_1){
    case"outbound-message":
    case"inbound-message":
      _1=iter((row) => {
        card.appendChild(renderTextBlock("fcell-message-body", row));
      }, arrayOrEmpty(value.rows));
      break;
    case"list":
      const list=element_1("ul", "fcell-list-items", null);
      _1=(iter((row) => {
        list.appendChild(element_1("li", "", asText_1(row)));
      }, arrayOrEmpty(value.rows)),void card.appendChild(list));
      break;
    case"grid":
      let _2;
      const table=element_1("table", "fcell-grid-table", null);
      const columns=arrayOrEmpty(value.columns);
      if(length(columns)>0){
        const thead=element_1("thead", "", null);
        const header=element_1("tr", "", null);
        _2=(iter((column) => {
          header.appendChild(element_1("th", "wrap", asText_1(column)));
        }, columns),thead.appendChild(header),void table.appendChild(thead));
      }
      else _2=null;
      const tbody=element_1("tbody", "", null);
      _1=(iter((cells) => {
        const tr=element_1("tr", "", null);
        iter((cell) => {
          tr.appendChild(element_1("td", "wrap", asText_1(cell)));
        }, arrayOrEmpty(cells));
        tbody.appendChild(tr);
      }, arrayOrEmpty(value.tableRows)),table.appendChild(tbody),void card.appendChild(table));
      break;
    default:
      _1=void card.appendChild(renderTextBlock("fcell-source", value.rawValue));
      break;
  }
  if(!isBlank_1(value.source)&&mode.toLowerCase()!="inbound-message"&&mode.toLowerCase()!="outbound-message")card.appendChild(renderTextBlock("fcell-source", value.source));
  return card;
}
function setStatus(node, text){
  node.textContent=text;
}
function scrollToBottomAfterRender(node){
  scrollToBottomNow(node);
  setTimeout(() => {
    scrollToBottomNow(node);
  }, 0);
  setTimeout(() => {
    scrollToBottomNow(node);
  }, 50);
  setTimeout(() => {
    scrollToBottomNow(node);
  }, 150);
  setTimeout(() => {
    scrollToBottomNow(node);
  }, 300);
}
function keysFromJson(keyJson){
  let r;
  const _1=keyJson;
  if(typeof _1!=="string"||_1.trim().length===0)return[];
  try {
    let parsed=JSON.parse(_1);
    let keys=Array.isArray(parsed)?parsed:parsed==null?[]:[parsed];
    return keys.map((value) => value==null?"":String(value).trim()).filter((value) => value.length>0);
  }
  catch(_ignoreKeyJsonParse){
    return[];
  }
}
function postAppendPageKey(url, body, onOk, onError){
  const headers=new Headers();
  headers.set("Content-Type", "application/json");
  const options=requestOptions();
  options.method="POST";
  options.headers=headers;
  options.body=JSON.stringify(body);
  (globalThis.fetch(url, options).then((response) => response.text().then((responseBody) => response.ok?onOk(json(isBlank_1(responseBody)?"{}":responseBody)):onError(isBlank_1(responseBody)?"POST "+String(url)+" "+String(response.status):responseBody))))["catch"]((error) => onError(errorMessage(error)));
}
function pendingFailure(action, error){
  return String(action)+" failed; pending command kept in browser DB: "+String(asText_1(error));
}
function rememberPending(kind, target, url, body){
  const payloadJson=JSON.stringify(body);
  const commandId=newPendingCommandId(kind, target, url, payloadJson);
  writePending(New_7(commandId, currentServerRealityId(), kind, target, url, "POST", payloadJson, "pending"));
  return commandId;
}
function isActorDynamicPage(page){
  return sameTextInvariant_1(page.shape, "actor-dynamic");
}
function tryRenderAddKeyWithRegisteredRenderers(pageId, shape, title, setName, keyPlaceholder, defaultKey, submitKey, cancelKey, setKeyJson){
  let r;
  const _1=pageId;
  const _2=shape;
  const _3=title;
  const _4=setName;
  const _5=keyPlaceholder;
  const _6=defaultKey;
  const _7=submitKey;
  const _8=cancelKey;
  const _9=setKeyJson;
  if(!(globalThis.PulseTrade&&globalThis.PulseTrade.AddKeyRenderers))return null;
  let renderers=globalThis.PulseTrade.AddKeyRenderers;
  let context={
    pageId:String(_1||""), 
    shape:String(_2||""), 
    title:String(_3||""), 
    setName:String(_4||""), 
    keyPlaceholder:String(_5||""), 
    defaultKey:String(_6||""), 
    submitKey:(payload) => {
      _7(payload);
    }, 
    cancelKey:() => {
      _8();
    }, 
    setKeyJson:(payload) => {
      _9(payload);
    }
  };
  for(let i=0;i<renderers.length;i++){
    let r_1=renderers[i];
    try {
      let value=(r_1.render||r_1[1])(context);
      let nodeOpt=((value_1) => {
        if(value_1==null)return null;
        if(value_1.$===1)return value_1;
        if(value_1.nodeType)return{$:1, $0:value_1};
        if(value_1.element&&value_1.element.nodeType)return{$:1, $0:value_1.element};
        if(value_1.node&&value_1.node.nodeType)return{$:1, $0:value_1.node};
        return null;
      })(value);
      if(nodeOpt!=null)return nodeOpt;
    }
    catch(e){
      console.error("Add-key renderer exception:", e);
    }
  }
  return null;
}
function rendererSubmittedKeyJson(payload){
  let r;
  if(payload==null)return"";
  if(typeof payload==="string")return payload;
  if(typeof payload.keyJson==="string")return payload.keyJson;
  let keys=[];
  if(Array.isArray(payload))keys=payload;
  else if(payload&&Array.isArray(payload.keys))keys=payload.keys;
  else if(payload&&typeof payload.actorAddress==="string"){
    keys=[payload.actorAddress];
    if(typeof payload.duTypeName==="string"&&payload.duTypeName.trim().length>0)keys.push(payload.duTypeName);
    if(Array.isArray(payload.unionCaseNames))keys=keys.concat(payload.unionCaseNames);
  }
  keys=keys.map((value) => value==null?"":String(value).trim()).filter((value) => value.length>0);
  if(keys.length===0)return"";
  return JSON.stringify(keys.length===1?keys[0]:keys);
}
function rendererSubmittedDisplayName(payload){
  let r;
  if(payload==null||typeof payload==="string")return"";
  let value="";
  if(typeof payload.displayName==="string")value=payload.displayName;
  else if(typeof payload.keyAlias==="string")value=payload.keyAlias;
  else if(typeof payload.alias==="string")value=payload.alias;
  else if(typeof payload.targetAlias==="string")value=payload.targetAlias;
  value=String(value||"").trim();
  return value;
}
function tryRenderAppendInputWithRegisteredRenderers(pageId, shape, title, setName, selectedKeyId, selectedKeyJson, selectedKeys, valuePlaceholder, valueText, submit, setValue){
  let r;
  const _1=pageId;
  const _2=shape;
  const _3=title;
  const _4=setName;
  const _5=selectedKeyId;
  const _6=selectedKeyJson;
  const _7=selectedKeys;
  const _8=valuePlaceholder;
  const _9=valueText;
  const _10=submit;
  const _11=setValue;
  if(!(globalThis.PulseTrade&&globalThis.PulseTrade.AppendInputRenderers))return null;
  let renderers=globalThis.PulseTrade.AppendInputRenderers;
  let keyParts=Array.isArray(_7)?_7.slice().map(String):[];
  if(keyParts.length===0&&typeof _6==="string"&&_6.trim().length>0)try {
    let parsedKeyJson=JSON.parse(_6);
    if(Array.isArray(parsedKeyJson))keyParts=parsedKeyJson.slice().map(String);
    else if(parsedKeyJson!=null)keyParts=[String(parsedKeyJson)];
  }
  catch(_ignoreKeyJsonParse){
    keyParts=[];
  }
  let duTypeName=keyParts.length>1?String(keyParts[1]||""):"";
  if(duTypeName.indexOf("1:duType:")===0)duTypeName=duTypeName.substring("1:duType:".length);
  let unionCaseNames=keyParts.length>2?keyParts.slice(2).map(String):[];
  unionCaseNames=unionCaseNames.length===1&&unionCaseNames[0].indexOf("2:unionCases:")===0?unionCaseNames[0].substring("2:unionCases:".length).split("|").map((value_1) => String(value_1||"").trim()).filter((value_1) => value_1.length>0):unionCaseNames.map((value_1) => value_1.indexOf("2:unionCase:")===0?value_1.substring("2:unionCase:".length):value_1).map((value_1) => String(value_1||"").trim()).filter((value_1) => value_1.length>0);
  let context={
    pageId:String(_1||""), 
    shape:String(_2||""), 
    title:String(_3||""), 
    setName:String(_4||""), 
    selectedKeyId:String(_5||""), 
    selectedKeyJson:String(_6||""), 
    selectedKeys:keyParts.slice(), 
    keyParts:keyParts.slice(), 
    actorAddress:keyParts.length>0?String(keyParts[0]||""):"", 
    duTypeName:duTypeName, 
    unionCaseNames:unionCaseNames, 
    valuePlaceholder:String(_8||""), 
    valueText:String(_9||""), 
    submit:(payload) => {
      _10(payload);
    }, 
    setValue:(payload) => {
      _11(payload);
    }
  };
  for(let i=0;i<renderers.length;i++){
    let r_1=renderers[i];
    try {
      let value=(r_1.render||r_1[1])(context);
      let nodeOpt=((value_1) => {
        if(value_1==null)return null;
        if(value_1.$===1)return value_1;
        if(value_1.nodeType)return{$:1, $0:value_1};
        if(value_1.element&&value_1.element.nodeType)return{$:1, $0:value_1.element};
        if(value_1.node&&value_1.node.nodeType)return{$:1, $0:value_1.node};
        return null;
      })(value);
      if(nodeOpt!=null)return nodeOpt;
    }
    catch(e){
      console.error("Append input renderer exception:", e);
    }
  }
  return null;
}
function rendererSubmittedText(payload){
  let r;
  if(payload==null)return"";
  if(typeof payload==="string")return payload;
  if(typeof payload.rawArgu==="string")return payload.rawArgu;
  if(typeof payload.valueText==="string")return payload.valueText;
  if(typeof payload.argu==="string")return payload.argu;
  if(typeof payload.commandLine==="string")return payload.commandLine;
  return String(payload);
}
function postJsonText(url, payloadJson, onOk, onError){
  const headers=new Headers();
  headers.set("Content-Type", "application/json");
  const options=requestOptions();
  options.method="POST";
  options.headers=headers;
  options.body=textOr("{}", payloadJson);
  (globalThis.fetch(url, options).then((response) => response.text().then((responseBody) => response.ok?onOk(responseBody):onError(isBlank_1(responseBody)?"POST "+String(url)+" "+String(response.status):responseBody))))["catch"]((error) => onError(errorMessage(error)));
}
function postJson(url, body, onOk, onError){
  const headers=new Headers();
  headers.set("Content-Type", "application/json");
  const options=requestOptions();
  options.method="POST";
  options.headers=headers;
  options.body=JSON.stringify(body);
  (globalThis.fetch(url, options).then((response) => response.text().then((responseBody) => response.ok?onOk(json(isBlank_1(responseBody)?"{}":responseBody)):onError(isBlank_1(responseBody)?"POST "+String(url)+" "+String(response.status):responseBody))))["catch"]((error) => onError(errorMessage(error)));
}
function postRemoveAppendPageKey(url, body, onOk, onError){
  const headers=new Headers();
  headers.set("Content-Type", "application/json");
  const options=requestOptions();
  options.method="POST";
  options.headers=headers;
  options.body=JSON.stringify(body);
  (globalThis.fetch(url, options).then((response) => response.text().then((responseBody) => response.ok?onOk(json(isBlank_1(responseBody)?"{}":responseBody)):onError(isBlank_1(responseBody)?"POST "+String(url)+" "+String(response.status):responseBody))))["catch"]((error) => onError(errorMessage(error)));
}
function setHref(href, node){
  node.setAttribute("href", href);
  return node;
}
function pageTypeClass(page){
  const shapeText=asText_1(page.shape).toLowerCase();
  if(isActorArguPage(page)){
    if(shapeText=="fcell-chat")return"actor-argu";
    else if(shapeText=="actor-argu")return"actor-argu";
    else if(shapeText=="raw")return"raw actor-argu";
    else {
      const m=findAppendPageShape(page.shape);
      if(m==null)return"actor-argu";
      else {
        const shape=m.$0;
        return textOr(normalizeShapeText(page.shape), shape.className);
      }
    }
  }
  else {
    const m_1=findAppendPageShape(page.shape);
    if(m_1==null)return"raw";
    else {
      const shape_1=m_1.$0;
      return textOr(normalizeShapeText(page.shape), shape_1.className);
    }
  }
}
function pageTypeBadge(page){
  const shapeText=asText_1(page.shape).toLowerCase();
  if(isActorArguPage(page)){
    if(shapeText=="fcell-chat")return"aa";
    else if(shapeText=="actor-argu")return"aa";
    else if(shapeText=="raw")return"ra";
    else {
      const m=findAppendPageShape(page.shape);
      return m==null?"aa":textOr("aa", m.$0.badge);
    }
  }
  else {
    const m_1=findAppendPageShape(page.shape);
    return m_1==null?"R":textOr("?", m_1.$0.badge);
  }
}
function setId(id, node){
  node.setAttribute("id", id);
  return node;
}
function currentLogoutPath(){
  const path=currentBrowserUser().logoutPath;
  return isBlank_1(path)?"/chat/logout":path;
}
function renderPageCreator(nav, activePath, pages){
  let candidatePageId, candidatesLoaded, replayingPendingPageRegistration;
  const wrap=setTestId_1("page-create", element_1("div", "page-create", null));
  const shape=setTestId_1("page-create-shape", select_1(appendPageShapeOptions()));
  const pageId=setTestId_1("page-create-id", input_1("page id"));
  const title=setTestId_1("page-create-title", input_1("title"));
  const binding=setTestId_1("page-create-binding", select_1([]));
  const add=setTestId_1("page-create-submit", button_1("", "+ Page"));
  const status=setTestId_1("page-create-status", element_1("span", "state page-create-status", ""));
  candidatePageId="";
  candidatesLoaded=false;
  const sameText=(left, right) => asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
  const appendOption=(value, label, target) => {
    const option=doc_1().createElement("option");
    option.setAttribute("value", value);
    option.textContent=label;
    target.appendChild(option);
  };
  const resetBinding=() => {
    clear(binding);
    appendOption("", "Default", binding);
    binding.value="";
    binding.setAttribute("data-candidate-count", "0");
    candidatesLoaded=false;
    candidatePageId="";
  };
  const refresh=(pages_1) => {
    renderNav(nav, activePath, arrayOrEmpty(pages_1));
  };
  const loadCandidates=(pageIdText, onDone) => {
    if(isBlank_1(pageIdText)){
      resetBinding();
      return onDone();
    }
    else {
      const normalizedInput=Trim(pageIdText);
      return candidatesLoaded&&candidatePageId.toLowerCase()==normalizedInput.toLowerCase()?onDone():(setStatus(status, "Checking history"),getJson("/pages/api/tab-candidates?pageId="+encodeURIComponent(normalizedInput), (reply) => {
        const candidates=arrayOrEmpty(reply.candidates);
        clear(binding);
        if(length(candidates)===0){
          appendOption("", "Default", binding);
          binding.value="";
          setStatus(status, "Ready");
        }
        else {
          iter((candidate) => {
            appendOption("reuse:"+asText_1(candidate.tabId), "Reuse "+textOr(asText_1(candidate.pageId), asText_1(candidate.tabId))+" ("+(candidate.visible?"visible":"hidden")+")", binding);
          }, candidates);
          appendOption("new", "New history", binding);
          binding.value="reuse:"+asText_1(get(candidates, 0).tabId);
          setStatus(status, "Existing history found");
        }
        candidatePageId=normalizedInput;
        candidatesLoaded=true;
        binding.setAttribute("data-candidate-count", String(length(candidates)));
        onDone();
      }, (error) => {
        resetBinding();
        setStatus(status, error);
        onDone();
      }));
    }
  };
  const addPageAfterCandidates=() => {
    const pageIdText=Trim(pageId.value);
    const titleText=Trim(title.value);
    if(isBlank_1(pageIdText)&&isBlank_1(titleText))setStatus(status, "Page id or title is required");
    else {
      const bindingValue=asText_1(binding.value);
      const p=StartsWith(bindingValue, "reuse:")?[bindingValue.substring("reuse:".length), "reuse"]:bindingValue=="new"?["", "new"]:["", ""];
      const request=New_35(pageIdText, titleText, "", shape.value, p[0], p[1], "", "");
      const pendingId=rememberPending("append-page-register", textOr(titleText, pageIdText), "/pages/api/register-page", request);
      setStatus(status, "Saving");
      postJson("/pages/api/register-page", request, (reply) => {
        deletePendingThen(pendingId, () => {
          writeAppendPagesDefinitions(New(reply.status, length(arrayOrEmpty(reply.pages)), reply.maxSequence, reply.pages));
          refresh(reply.pages);
          reply.page==null?setStatus(status, "Saved"):(setStatus(status, "Saved "+pageTitle(reply.page)),globalThis.location.assign(navigationPathForCreatedPage(reply.page)));
        });
      }, (error) => {
        setStatus(status, pendingFailure("Create page", error));
      });
    }
  };
  replayingPendingPageRegistration=false;
  const addPage=() => {
    loadCandidates(Trim(pageId.value), addPageAfterCandidates);
  };
  pageId.addEventListener("keydown", (event) => event.key=="Enter"?addPage():null);
  pageId.addEventListener("input", () => {
    const pageIdText=Trim(pageId.value);
    return isBlank_1(pageIdText)?resetBinding():loadCandidates(pageIdText, () => { });
  });
  title.addEventListener("keydown", (event) => event.key=="Enter"?addPage():null);
  add.addEventListener("click", addPage);
  resetBinding();
  refresh(pages);
  append_1(wrap, [shape, pageId, title, binding, add, status]);
  setHidden(!systemAclAllows("*", "ptcs.page.create"), wrap);
  if(!replayingPendingPageRegistration){
    replayingPendingPageRegistration=true;
    readAllPending((commands) => {
      let remaining, accepted;
      const mine=filter((command) =>!(command==null)&&sameText(command.kind, "append-page-register")&&sameText(command.method, "POST")&&!isBlank_1(command.url)&&!isBlank_1(command.payloadJson), commands);
      if(length(mine)===0)replayingPendingPageRegistration=false;
      else {
        remaining=length(mine);
        accepted=0;
        setStatus(status, "Replaying "+String(length(mine))+" pending page command(s)");
        const finishOne=() => {
          remaining=remaining-1;
          remaining===0?(replayingPendingPageRegistration=false,accepted>0?setStatus(status, "Replayed "+String(accepted)+" pending page command(s)"):void 0):void 0;
        };
        iter((command) => {
          postJsonText(command.url, command.payloadJson, (body) => {
            try {
              const reply=json(isBlank_1(body)?"{}":body);
              deletePendingThen(command.commandId, () => {
                accepted=accepted+1;
                !(reply==null)?(writeAppendPagesDefinitions(New(reply.status, length(arrayOrEmpty(reply.pages)), reply.maxSequence, reply.pages)),refresh(reply.pages)):void 0;
                finishOne();
              });
            }
            catch(error){
              setStatus(status, "Replay create page parse failed: "+errorMessage(error));
              finishOne();
            }
          }, (error) => {
            setStatus(status, pendingFailure("Replay create page", error));
            finishOne();
          });
        }, mine);
      }
    });
  }
  return wrap;
}
function setValueCount(buckets){
  return fold((_1, _2) => _1+_2, 0, map((bucket) => bucket==null?0:bucket.valueCount, arrayOrEmpty(buckets)));
}
function tryRenderWithRegisteredPageRenderers(text){
  let r;
  const content=asText_1(text);
  if(isBlank_1(content))return null;
  else {
    const _1=content;
    if(globalThis.PulseTrade){
      let rendererGroups=[];
      if(globalThis.PulseTrade.PageRenderers)rendererGroups.push(globalThis.PulseTrade.PageRenderers);
      if(globalThis.PulseTrade.MessageRenderers)rendererGroups.push(globalThis.PulseTrade.MessageRenderers);
      for(let g=0;g<rendererGroups.length;g++){
        let renderers=rendererGroups[g];
        for(let i=0;i<renderers.length;i++){
          let r_1=renderers[i];
          try {
            let value=(r_1.render||r_1[1])(_1);
            let nodeOpt=((value_1) => {
              if(value_1==null)return null;
              if(value_1.$===1)return value_1;
              if(value_1.nodeType)return{$:1, $0:value_1};
              if(value_1.element&&value_1.element.nodeType)return{$:1, $0:value_1.element};
              if(value_1.node&&value_1.node.nodeType)return{$:1, $0:value_1.node};
              return null;
            })(value);
            if(nodeOpt!=null)return nodeOpt;
          }
          catch(e){
            console.error("Page renderer exception:", e);
          }
        }
      }
    }
    return null;
  }
}
function actorValueCount(data){
  if(data==null)return 0;
  else {
    const a=data.actorCount;
    const b=data.nodeCount;
    return Compare(a, b)===1?a:b;
  }
}
function cardTitle(title, id, status, line){
  const wrap=doc_1().createDocumentFragment();
  const row=element_1("div", "name-row", null);
  append_1(row, [statusDot(status), element_1("span", "strong wrap", title)]);
  wrap.appendChild(row);
  if(!isBlank_1(id))wrap.appendChild(element_1("div", "muted wrap", id));
  if(!isBlank_1(line))wrap.appendChild(element_1("div", "meta wrap", line));
  return wrap;
}
function statusDot(status){
  const node=element_1("span", isLive(status)?"status-dot online":"status-dot offline", null);
  node.setAttribute("title", asText_1(status));
  return node;
}
function compactMessageId(value){
  const text=asText_1(value);
  return text.length<=32?text:StartsWith(text.toLowerCase(), "pending-command")?"pending-command:"+String(text.length):Substring(text, 0, 24)+"..."+text.substring(text.length-6);
}
function mergeThreadMessages(existing, incoming){
  const v=distinctMessages(arrayOrEmpty(existing).concat(arrayOrEmpty(incoming)));
  return latestArray(defaultRenderLimit(), v);
}
function maxMessageSequence(messages){
  return fold((_1, _2) => Compare(_1, _2)===1?_1:_2, 0n, map((message) => message==null?0n:tryParseSequence("msg-", message.messageId), arrayOrEmpty(messages)));
}
function int64OrZero(value){
  const parsed=parseInt(asText_1(value), globalThis.$radix);
  return isNaN(parsed)||parsed<0?0n:BigInt(parsed);
}
function initializeClientExtensionGlobals(){
  if(!globalThis.PulseTrade)globalThis.PulseTrade={};
  if(!globalThis.PulseTrade.MessageRenderers)globalThis.PulseTrade.MessageRenderers=[];
  if(!globalThis.PulseTrade.PageRenderers)globalThis.PulseTrade.PageRenderers=[];
  if(!globalThis.PulseTrade.AppendInputRenderers)globalThis.PulseTrade.AppendInputRenderers=[];
  if(!globalThis.PulseTrade.AddKeyRenderers)globalThis.PulseTrade.AddKeyRenderers=[];
  if(!globalThis.PulseTrade.LoginRenderers)globalThis.PulseTrade.LoginRenderers=[];
  if(!globalThis.PulseTrade.AclSnapshotObservers)globalThis.PulseTrade.AclSnapshotObservers=[];
  if(!globalThis.PulseTrade.AclCapabilityProviders)globalThis.PulseTrade.AclCapabilityProviders=[];
  if(!globalThis.PulseTrade.Renderers)globalThis.PulseTrade.Renderers=globalThis.PulseTrade.MessageRenderers;
  let register_1=(collection, name, priority, func) => {
    if(typeof priority==="function"){
      func=priority;
      priority=0;
    }
    if(typeof func!=="function")return;
    collection.push({
      name:String(name||"unnamed"), 
      priority:Number(priority||0), 
      render:func
    });
    collection.sort((left, right) =>(right.priority||0)-(left.priority||0));
  };
  globalThis.PulseTradeRegisterRenderer=(name, priority, func) => {
    register_1(globalThis.PulseTrade.MessageRenderers, name, priority, func);
  };
  globalThis.PulseTradeRegisterPageRenderer=(name, priority, func) => {
    register_1(globalThis.PulseTrade.PageRenderers, name, priority, func);
  };
  globalThis.PulseTradeRegisterAppendInputRenderer=(name, priority, func) => {
    register_1(globalThis.PulseTrade.AppendInputRenderers, name, priority, func);
  };
  globalThis.PulseTradeRegisterAddKeyRenderer=(name, priority, func) => {
    register_1(globalThis.PulseTrade.AddKeyRenderers, name, priority, func);
  };
  globalThis.PulseTradeRegisterLoginRenderer=(name, priority, func) => {
    register_1(globalThis.PulseTrade.LoginRenderers, name, priority, func);
  };
  globalThis.PulseTradeRegisterAclSnapshotObserver=(name, priority, func) => {
    register_1(globalThis.PulseTrade.AclSnapshotObservers, name, priority, func);
  };
  globalThis.PulseTradeRegisterAclCapabilityProvider=(name, priority, func) => {
    register_1(globalThis.PulseTrade.AclCapabilityProviders, name, priority, func);
  };
}
function routeItem(icon, name, value){
  const item=element_1("li", "route-item", null);
  const content=element_1("div", "", null);
  append_1(content, [element_1("p", "route-name", name), element_1("p", "route-value", value)]);
  append_1(item, [element_1("span", "route-icon", icon), content]);
  return item;
}
function field_1(labelText, inputId, control){
  const wrap=element_1("div", "field", null);
  const label=element_1("label", "", labelText);
  label.setAttribute("for", inputId);
  append_1(wrap, [label, control]);
  return wrap;
}
function aclAllows(action, resourceKind, resourceId){
  const m=tryAclCapabilityProvider(action, resourceKind, resourceId);
  return m==null?aclAllowsFallback(action, resourceKind, resourceId):m.$0;
}
function findAppendPageShape(shape){
  const normalized=normalizeShapeText(shape);
  return tryFind((candidate) => normalizeShapeText(candidate.shape)==normalized, appendPageShapeRegistry());
}
function normalizeShapeText(value){
  const text=Trim(asText_1(value)).toLowerCase();
  return text.length>0&&text.length<=64&&forall((ch) => ch>="a"&&ch<="z"||ch>="0"&&ch<="9"||ch==="-"||ch==="_"||ch===".", text)?text:"raw";
}
function hasTag(tag, tags){
  return exists((value) => asText_1(value).toLowerCase()==tag, arrayOrEmpty(tags));
}
function currentBrowserUser(){
  const userNode=doc_1().getElementById("ptc-comm-user");
  if(userNode==null||isBlank_1(userNode.textContent))return New_34("user.web", "Web User", "", false, "anonymous", "/chat/logout");
  else {
    const user=json(userNode.textContent);
    return user==null||isBlank_1(user.participantId)?New_34("user.web", "Web User", "", false, "anonymous", "/chat/logout"):user;
  }
}
function fcellValueModeLabel(mode, tags){
  return hasTag("actor-argu-command", tags)?"Actor Argu Outbound":hasTag("actor-argu-reply", tags)?"Actor Argu Reply":hasTag("actor-argu-error", tags)?"Actor Argu Error":fcellModeLabel(mode);
}
function renderTextBlock(className, text){
  const m=tryRenderWithRegisteredRenderers(text);
  return m==null?element_1("pre", className, asText_1(text)):m.$0;
}
function scrollToBottomNow(node){
  if(!(node==null)){
    try {
      node.scrollTop=node.scrollHeight;
    }
    catch(m){
      null;
    }
  }
}
function newPendingCommandId(kind, target, url, payloadJson){
  set_pendingCommandSeq(pendingCommandSeq()+1);
  return cacheKey("pending-command", ofArray([kind, target, url, payloadJson, "attempt-"+String(pendingCommandSeq()), "rand-"+String(Math.floor(Math.random()*1000000000))]));
}
function select_1(options){
  const node=doc_1().createElement("select");
  iter((_1) => {
    const option=doc_1().createElement("option");
    option.setAttribute("value", _1[0]);
    option.textContent=_1[1];
    node.appendChild(option);
  }, options);
  return node;
}
function appendPageShapeOptions(){
  return map((shape) =>[normalizeShapeText(shape.shape), textOr(normalizeShapeText(shape.shape), shape.label)], appendPageShapeRegistry());
}
function systemAclAllows(resourceId, action){
  return aclAllows(action, "ptcs.system", resourceId);
}
function navigationPathForCreatedPage(page){
  const pageId=asText_1(page.pageId);
  const path=asText_1(page.path);
  return exists((alias) => sameTextInvariant_1(path, alias), ["/fcell-chat", "/fcell-list", "/fcell-grid"])?path:"/page/"+pageId;
}
function isLive(status){
  const m=asText_1(status).toLowerCase();
  return m=="online"||(m=="running"||(m=="up"||m=="available"));
}
function distinctMessages(messages){
  let kept;
  kept=[];
  iter((message) => {
    if(!(message==null)&&!isBlank_1(message.messageId)&&!exists((row) => row.messageId==message.messageId, kept))kept=kept.concat([message]);
  }, arrayOrEmpty(messages));
  return kept;
}
function tryParseSequence(prefix, value){
  const text=asText_1(value);
  if(isBlank_1(text)||!StartsWith(text, prefix))return 0n;
  else try {
    return BigInt(text.substring(prefix.length));
  }
  catch(m){
    return 0n;
  }
}
function tryAclCapabilityProvider(action, resourceKind, resourceId){
  const normalized=Trim(asText_1(((action_1, resourceKind_1, resourceId_1, snapshotJson) => {
    if(!(globalThis.PulseTrade&&globalThis.PulseTrade.AclCapabilityProviders))return"unknown";
    const providers=globalThis.PulseTrade.AclCapabilityProviders;
    for(let i=0;i<providers.length;i++){
      const provider=providers[i];
      try {
        const value=(provider.render||provider[1])(action_1, resourceKind_1, resourceId_1, snapshotJson||"");
        if(value===true)return"allow";
        if(value===false)return"deny";
        const text=String(value||"").toLowerCase();
        if(text==="allow"||text==="allowed"||text==="true")return"allow";
        if(text==="deny"||text==="denied"||text==="false")return"deny";
      }
      catch(e){
        console.error("ACL capability provider exception:", e);
      }
    }
    return"unknown";
  })(action, resourceKind, resourceId, currentAclSnapshotJson()))).toLowerCase();
  return normalized=="allow"?Some(true):normalized=="deny"?Some(false):null;
}
function aclAllowsFallback(action, resourceKind, resourceId){
  const _1=currentAclSnapshot();
  if(_1!=null&&_1.$==1){
    if(!currentAclSnapshot().$0.enabled){
      currentAclSnapshot().$0;
      return true;
    }
    else {
      const snapshot=currentAclSnapshot().$0;
      const o=tryFind((resource) => aclSameText(resource.resourceKind, resourceKind)&&aclSameText(resource.resourceId, resourceId), arrayOrEmpty(snapshot.resources));
      const o_1=o==null?null:aclCapabilityAllowed(action, o.$0.capabilities);
      const o_2=o_1==null?aclCapabilityAllowed(action, snapshot.globalCapabilities):(o_1.$0,o_1);
      return o_2==null?false:o_2.$0;
    }
  }
  else return true;
}
function appendPageShapeRegistry(){
  return distinctBy((shape) => normalizeShapeText(shape.shape), concat([builtInAppendPageShapes(), manifestAppendPageShapes(), runtimeAppendPageShapes()]));
}
function fcellModeLabel(mode){
  const m=asText_1(mode).toLowerCase();
  return m=="inbound-message"?"FCell Chat":m=="outbound-message"?"FCell Chat":m=="list"?"FCell List":m=="table"?"FCell Grid":m=="grid"?"FCell Grid":"FCell Value";
}
function tryRenderWithRegisteredRenderers(text){
  let r;
  const content=asText_1(text);
  if(isBlank_1(content))return null;
  else {
    const local=tryPick((_2) => {
      try {
        return _2[1](content);
      }
      catch(m){
        return null;
      }
    }, registeredRenderers());
    if(local==null){
      const _1=content;
      if(globalThis.PulseTrade&&globalThis.PulseTrade.MessageRenderers){
        let renderers=globalThis.PulseTrade.MessageRenderers;
        for(let i=0;i<renderers.length;i++){
          let r_1=renderers[i];
          try {
            let value=(r_1.render||r_1[1])(_1);
            let nodeOpt=((value_1) => {
              if(value_1==null)return null;
              if(value_1.$===1)return value_1;
              if(value_1.nodeType)return{$:1, $0:value_1};
              if(value_1.element&&value_1.element.nodeType)return{$:1, $0:value_1.element};
              if(value_1.node&&value_1.node.nodeType)return{$:1, $0:value_1.node};
              return null;
            })(value);
            if(nodeOpt!=null)return nodeOpt;
          }
          catch(e){
            console.error("Renderer exception:", e);
          }
        }
      }
      return null;
    }
    else return Some(local.$0);
  }
}
function set_pendingCommandSeq(_1){
  _c.pendingCommandSeq=_1;
}
function pendingCommandSeq(){
  return _c.pendingCommandSeq;
}
function currentAclSnapshot(){
  return _c.currentAclSnapshot;
}
function aclCapabilityAllowed(action, capabilities){
  const o=tryFind((item) => aclSameText(item.action, action), arrayOrEmpty(capabilities));
  return o==null?null:Some(o.$0.allowed);
}
function aclSameText(left, right){
  return asText_1(left).toLowerCase()==asText_1(right).toLowerCase();
}
function builtInAppendPageShapes(){
  return[shapeRegistration("fcell-chat", "FCell Chat", "C", "fcell-chat"), shapeRegistration("fcell-list", "FCell List", "L", "fcell-list"), shapeRegistration("fcell-grid", "FCell Grid", "G", "fcell-grid"), shapeRegistration("actor-argu", "Actor Argu", "aa", "actor-argu"), shapeRegistration("raw", "Raw", "R", "raw")];
}
function manifestAppendPageShapes(){
  return filter((shape) => shape.shape!="raw", map((shape) => shape==null?shapeRegistration("raw", "Raw", "R", "raw"):shapeRegistration(shape.shape, shape.label, shape.badge, shape.className), collect((extension) => extension==null?[]:arrayOrEmpty(extension.appendPageShapes), serverClientExtensions())));
}
function runtimeAppendPageShapes(){
  return _c.runtimeAppendPageShapes;
}
function registeredRenderers(){
  return _c.registeredRenderers;
}
function shapeRegistration(shape, label, badge, className){
  return New_31(normalizeShapeText(shape), textOr(normalizeShapeText(shape), label), textOr("?", badge), textOr(normalizeShapeText(shape), className));
}
function serverClientExtensions(){
  const node=doc_1().getElementById("ptc-comm-client-extensions");
  if(node==null||isBlank_1(node.textContent))return[];
  else {
    const o=tryJson(node.textContent);
    return o==null?[]:o.$0;
  }
}
function FailWith(msg){
  throw new Error(msg);
}
function KeyValue(kvp){
  return[kvp.K, kvp.V];
}
function GetFieldValues(o){
  let r=[];
  let k;
  for(var k_1 in o)r.push(o[k_1]);
  return r;
}
function New(status, count, maxSequence, pages){
  return{
    status:status, 
    count:count, 
    maxSequence:maxSequence, 
    pages:pages
  };
}
function iter(f, arr){
  for(let i=0, _1=arr.length-1;i<=_1;i++)f(arr[i]);
}
function filter(f, arr){
  const r=[];
  for(let i=0, _1=arr.length-1;i<=_1;i++)if(f(arr[i]))r.push(arr[i]);
  return r;
}
function tryFind(f, arr){
  let res, i;
  res=null;
  i=0;
  while(i<arr.length&&res==null)
    {
      f(arr[i])?res=Some(arr[i]):void 0;
      i=i+1;
    }
  return res;
}
function exists(f, x){
  let e, i;
  e=false;
  i=0;
  const l=length(x);
  while(!e&&i<l)
    if(f(x[i]))e=true;
    else i=i+1;
  return e;
}
function map(f, arr){
  const r=new Array(arr.length);
  for(let i=0, _1=arr.length-1;i<=_1;i++)r[i]=f(arr[i]);
  return r;
}
function sortBy(f, arr){
  return map((t) => t[0], mapi((_1, _2) =>[_2, [f(_2), _1]], arr).sort((_1, _2) => Compare(_1[1], _2[1])));
}
function tryHead(arr){
  return arr.length===0?null:Some(arr[0]);
}
function forall2(f, x1, x2){
  let a, i;
  checkLength(x1, x2);
  a=true;
  i=0;
  const l=length(x1);
  while(a&&i<l)
    if(f(x1[i], x2[i]))i=i+1;
    else a=false;
  return a;
}
function choose(f, arr){
  const q=[];
  for(let i=0, _1=arr.length-1;i<=_1;i++){
    const m=f(arr[i]);
    if(m==null){ }
    else q.push(m.$0);
  }
  return q;
}
function distinctBy(f, a){
  return ofSeq(distinctBy_1(f, a));
}
function fold(f, zero, arr){
  let acc;
  acc=zero;
  for(let i=0, _1=arr.length-1;i<=_1;i++)acc=f(acc, arr[i]);
  return acc;
}
function distinct(l){
  return ofSeq(distinct_1(l));
}
function mapi(f, arr){
  const y=new Array(arr.length);
  for(let i=0, _1=arr.length-1;i<=_1;i++)y[i]=f(i, arr[i]);
  return y;
}
function skip(i, ar){
  return i<0?nonNegative():i>ar.length?insufficient():ar.slice(i);
}
function ofSeq(xs){
  if(xs instanceof Array)return xs.slice();
  else if(xs instanceof FSharpList)return ofList(xs);
  else {
    const q=[];
    const o=Get(xs);
    try {
      while(o.MoveNext())
        q.push(o.Current);
      return q;
    }
    finally {
      const _1=o;
      if(typeof _1=="object"&&isIDisposable(_1))o.Dispose();
    }
  }
}
function checkLength(arr1, arr2){
  if(arr1.length!==arr2.length)FailWith("The arrays have different lengths.");
}
function ofList(xs){
  let l;
  const q=[];
  l=xs;
  while(!(l.$==0))
    {
      q.push(head(l));
      l=tail(l);
    }
  return q;
}
function sortInPlace(arr){
  mapInPlace((t) => t[0], mapiInPlace((_1, _2) =>[_2, _1], arr).sort(Compare));
}
function concat(xs){
  return Array.prototype.concat.apply([], ofSeq(xs));
}
function tryPick(f, arr){
  let res, i;
  res=null;
  i=0;
  while(i<arr.length&&res==null)
    {
      const m=f(arr[i]);
      if(m!=null&&m.$==1)res=m;
      i=i+1;
    }
  return res;
}
function collect(f, x){
  return Array.prototype.concat.apply([], map(f, x));
}
function pick(f, arr){
  const m=tryPick(f, arr);
  return m==null?FailWith("KeyNotFoundException"):m.$0;
}
function tryFindIndex(f, arr){
  let res, i;
  res=null;
  i=0;
  while(i<arr.length&&res==null)
    {
      f(arr[i])?res=Some(i):void 0;
      i=i+1;
    }
  return res;
}
function readJson(key, onRead){
  if(isBlank_1(key))onRead(null);
  else withStore(snapshotStore(), "readonly", (store) => {
    try {
      const request=store.get(key);
      request.onsuccess=(event) => {
        const value=eventResult(event);
        if(isMissing(value))return onRead(null);
        else try {
          const text=String(value);
          return isBlank_1(text)?onRead(null):onRead(tryJson(text));
        }
        catch(m){
          return onRead(null);
        }
      };
      request.onerror=() => onRead(null);
    }
    catch(m){
      onRead(null);
    }
  }, () => {
    onRead(null);
  });
}
function cacheKey(scope, parts){
  return currentServerRealityId()+":"+scope+":"+concat_1(":", map_1((part) => encodeURIComponent(asText_1(part)), parts));
}
function withStore(storeName, mode, onStore, onUnavailable){
  openDb((db) => {
    try {
      onStore(db.transaction([storeName], mode).objectStore(storeName));
    }
    catch(m){
      onUnavailable();
    }
  }, onUnavailable);
}
function snapshotStore(){
  return _c.snapshotStore;
}
function eventResult(event){
  const target=event.target;
  return isMissing(target)?null:target.result;
}
function isMissing(value){
  return value==null||Equals(typeof value, "undefined");
}
function readPendingRealitySplit(onRead){
  readAllPendingRaw((commands) => {
    const reality=currentServerRealityId();
    onRead(filter((command) =>!(command==null)&&textOr("legacy", command.serverRealityId)==reality, commands), filter((command) =>!(command==null)&&textOr("legacy", command.serverRealityId)!=reality, commands));
  });
}
function writeWatermark(streamId, newestSequence, cachedCount, source){
  if(!isBlank_1(streamId)){
    let _1=watermarkStore();
    const a=0n;
    let _2=Compare(a, newestSequence)===1?a:newestSequence;
    let _3=String(_2);
    const a_1=0;
    let _4=Compare(a_1, cachedCount)===1?a_1:cachedCount;
    let _5=New_26(streamId, _3, _4, asText_1(source), nowTicks());
    writeJsonTo(_1, streamId, _5);
    compactSnapshots();
  }
}
function readAllPending(onRead){
  readAllPendingRaw((commands) => {
    const reality=currentServerRealityId();
    onRead(filter((command) =>!(command==null)&&textOr("legacy", command.serverRealityId)==reality, commands));
  });
}
function deletePendingThen(commandId, onDeleted){
  deleteFromThen(pendingStore(), commandId, onDeleted);
}
function readWatermark(key, onRead){
  if(isBlank_1(key))onRead(null);
  else withStore(watermarkStore(), "readonly", (store) => {
    try {
      const request=store.get(key);
      request.onsuccess=(event) => {
        const value=eventResult(event);
        if(isMissing(value))return onRead(null);
        else try {
          const text=String(value);
          return isBlank_1(text)?onRead(null):onRead(tryJson(text));
        }
        catch(m){
          return onRead(null);
        }
      };
      request.onerror=() => onRead(null);
    }
    catch(m){
      onRead(null);
    }
  }, () => {
    onRead(null);
  });
}
function openDb(onReady, onUnavailable){
  try {
    const indexedDb=globalThis.indexedDB;
    if(isMissing(indexedDb))onUnavailable();
    else {
      const a=[databaseName(), databaseVersion()];
      const request=indexedDb.open.apply(indexedDb, a);
      request.onupgradeneeded=(event) => {
        const db=eventResult(event);
        return!isMissing(db)?ensureStores(db):null;
      };
      request.onsuccess=(event) => {
        const db=eventResult(event);
        return!isMissing(db)?onReady(db):onUnavailable();
      };
      request.onerror=() => onUnavailable();
    }
  }
  catch(m){
    onUnavailable();
  }
}
function writeJson(key, value){
  writeJsonTo(snapshotStore(), key, value);
}
function readAllPendingRaw(onRead){
  withStore(pendingStore(), "readonly", (store) => {
    try {
      const request=store.getAll();
      request.onsuccess=(event) => {
        const value=eventResult(event);
        if(isMissing(value))return onRead([]);
        else try {
          return onRead(choose((text) => {
            try {
              return isBlank_1(text)?null:tryJson(text);
            }
            catch(m){
              return null;
            }
          }, value));
        }
        catch(m){
          return onRead([]);
        }
      };
      request.onerror=() => onRead([]);
    }
    catch(m){
      onRead([]);
    }
  }, () => {
    onRead([]);
  });
}
function writeJsonTo(storeName, key, value){
  if(!isBlank_1(key))withStore(storeName, "readwrite", (store) => {
    try {
      const a=[JSON.stringify(value), key];
      store.put.apply(store, a);
    }
    catch(m){
      null;
    }
  }, () => { });
}
function watermarkStore(){
  return _c.watermarkStore;
}
function nowTicks(){
  try {
    const this_1=Date.now();
    let _1=BigInt(Math.trunc(this_1))*BigInt(1E4)+BigInt((this_1-Math.trunc(this_1))*1E4);
    return String(_1);
  }
  catch(m){
    return"0";
  }
}
function compactSnapshots(){
  readAllWatermarks((watermarks) => {
    const watermarks_1=arrayOrEmpty(watermarks);
    const overflow=length(watermarks_1)-maxSnapshotRecords();
    if(overflow>0)iter((watermark) => {
      deleteSnapshotAndWatermark(watermark.streamId);
    }, sortBy(watermarkTouchedAt, filter((watermark) =>!(watermark==null)&&!isBlank_1(watermark.streamId)&&!protectedSnapshotKey(watermark.streamId), watermarks_1)).slice(0, overflow));
    readAllSnapshotKeys((snapshotKeys) => {
      iter((key) => {
        deleteFrom(snapshotStore(), key);
      }, filter((key) =>!isBlank_1(key)&&!protectedSnapshotKey(key)&&!exists((watermark) =>!(watermark==null)&&watermark.streamId==key, watermarks_1), snapshotKeys));
    });
  });
}
function deleteFromThen(storeName, key, onDeleted){
  if(isBlank_1(key))onDeleted();
  else withTransactionStore(storeName, "readwrite", (_1, _2) =>(((tx) =>(store) => {
    let finished;
    finished=false;
    const finish=() => {
      if(!finished){
        finished=true;
        onDeleted();
      }
    };
    tx.oncomplete=() => finish();
    tx.onabort=() => finish();
    tx.onerror=() => finish();
    try {
      store["delete"](key);
      return;
    }
    catch(m){
      return finish();
    }
  })(_1))(_2), onDeleted);
}
function pendingStore(){
  return _c.pendingStore;
}
function writePending(command){
  writeJsonTo(pendingStore(), command.commandId, command);
}
function databaseName(){
  return _c.databaseName;
}
function databaseVersion(){
  return _c.databaseVersion;
}
function ensureStores(db){
  ensureStore(snapshotStore(), db);
  ensureStore(pendingStore(), db);
  ensureStore(watermarkStore(), db);
}
function readAllWatermarks(onRead){
  withStore(watermarkStore(), "readonly", (store) => {
    try {
      const request=store.getAll();
      request.onsuccess=(event) => {
        const value=eventResult(event);
        if(isMissing(value))return onRead([]);
        else try {
          return onRead(choose((text) => {
            try {
              return isBlank_1(text)?null:tryJson(text);
            }
            catch(m){
              return null;
            }
          }, value));
        }
        catch(m){
          return onRead([]);
        }
      };
      request.onerror=() => onRead([]);
    }
    catch(m){
      onRead([]);
    }
  }, () => {
    onRead([]);
  });
}
function maxSnapshotRecords(){
  return _c.maxSnapshotRecords;
}
function protectedSnapshotKey(key){
  const key_1=asText_1(key);
  return key_1=="append-pages-definitions:"||key_1.indexOf(":append-pages-definitions:")!=-1||StartsWith(key_1, "chat-agents:")||key_1.indexOf(":chat-agents:")!=-1||StartsWith(key_1, "actors-snapshot:")||key_1.indexOf(":actors-snapshot:")!=-1;
}
function watermarkTouchedAt(watermark){
  let o;
  if(watermark==null)return 0n;
  else {
    const m=(o=0n,[TryParse(asText_1(watermark.touchedAt), {get:() => o, set:(v) => {
      o=v;
    }}), o]);
    return m[0]?m[1]:0n;
  }
}
function deleteSnapshotAndWatermark(key){
  if(!isBlank_1(key))withSnapshotWatermarkStores("readwrite", (_1, _2, _3) => {
    try {
      _2["delete"](key);
      _3["delete"](key);
      return;
    }
    catch(m){
      return null;
    }
  }, () => { });
}
function readAllSnapshotKeys(onRead){
  withStore(snapshotStore(), "readonly", (store) => {
    try {
      const request=store.getAllKeys();
      request.onsuccess=(event) => {
        const value=eventResult(event);
        if(isMissing(value))return onRead([]);
        else try {
          return onRead(value);
        }
        catch(m){
          return onRead([]);
        }
      };
      request.onerror=() => onRead([]);
    }
    catch(m){
      onRead([]);
    }
  }, () => {
    onRead([]);
  });
}
function deleteFrom(storeName, key){
  if(!isBlank_1(key))withStore(storeName, "readwrite", (store) => {
    try {
      store["delete"](key);
    }
    catch(m){
      null;
    }
  }, () => { });
}
function withTransactionStore(storeName, mode, onStore, onUnavailable){
  openDb((db) => {
    try {
      const tx=db.transaction([storeName], mode);
      onStore(tx, tx.objectStore(storeName));
    }
    catch(m){
      onUnavailable();
    }
  }, onUnavailable);
}
function ensureStore(storeName, db){
  let _1;
  const names=db.objectStoreNames;
  if(isMissing(names))_1=false;
  else try {
    _1=names.contains(storeName);
  }
  catch(m){
    _1=false;
  }
  if(!_1)db.createObjectStore(storeName);
}
function withSnapshotWatermarkStores(mode, onStores, onUnavailable){
  openDb((db) => {
    try {
      const a=[[snapshotStore(), watermarkStore()], mode];
      const tx=db.transaction.apply(db, a);
      const a_1=[snapshotStore()];
      let _1=tx.objectStore.apply(tx, a_1);
      const a_2=[watermarkStore()];
      let _2=tx.objectStore.apply(tx, a_2);
      onStores(tx, _1, _2);
    }
    catch(m){
      onUnavailable();
    }
  }, onUnavailable);
}
function Equals(a, b){
  let _1;
  if(a===b)return true;
  else {
    const m=typeof a;
    if(m=="object"){
      if(a===null||a===void 0||b===null||b===void 0||!Equals(typeof b, "object"))return false;
      else if("Equals"in a)return a.Equals(b);
      else if("Equals"in b)return false;
      else if(a instanceof Array&&b instanceof Array)return arrayEquals(a, b);
      else if(a instanceof Date&&b instanceof Date)return dateEquals(a, b);
      else {
        const a_1=a;
        const b_1=b;
        const eqR=[true];
        let k;
        for(var k_2 in a_1)if(((k_3) => {
          eqR[0]=!a_1.hasOwnProperty(k_3)||b_1.hasOwnProperty(k_3)&&Equals(a_1[k_3], b_1[k_3]);
          return!eqR[0];
        })(k_2))break;
        if(eqR[0]){
          let k_1;
          for(var k_3 in b_1)if(((k_4) => {
            eqR[0]=!b_1.hasOwnProperty(k_4)||a_1.hasOwnProperty(k_4);
            return!eqR[0];
          })(k_3))break;
          _1=void 0;
        }
        else _1=null;
        return eqR[0];
      }
    }
    else return m=="function"&&("$Func"in a?a.$Func===b.$Func&&a.$Target===b.$Target:"$Invokes"in a&&"$Invokes"in b&&arrayEquals(a.$Invokes, b.$Invokes));
  }
}
function Compare(a, b){
  if(a===b)return 0;
  else {
    const m=typeof a;
    switch(m=="boolean"?1:m=="number"?1:m=="bigint"?1:m=="string"?1:m=="object"?2:m=="function"?3:m=="symbol"?4:0){
      case 0:
        return typeof b=="undefined"?0:-1;
      case 1:
        return a<b?-1:1;
      case 2:
        let _1;
        if(a===null)return -1;
        else if(b===null)return 1;
        else if("CompareTo"in a)return a.CompareTo(b);
        else if("CompareTo0"in a)return a.CompareTo0(b);
        else if(a instanceof Array&&b instanceof Array)return compareArrays(a, b);
        else if(a instanceof Date&&b instanceof Date)return compareDates(a, b);
        else {
          const a_1=a;
          const b_1=b;
          const cmp=[0];
          let k;
          for(var k_2 in a_1)if(((k_3) =>!a_1.hasOwnProperty(k_3)?false:!b_1.hasOwnProperty(k_3)?(cmp[0]=1,true):(cmp[0]=Compare(a_1[k_3], b_1[k_3]),cmp[0]!==0))(k_2))break;
          if(cmp[0]===0){
            let k_1;
            for(var k_3 in b_1)if(((k_4) =>!b_1.hasOwnProperty(k_4)?false:!a_1.hasOwnProperty(k_4)&&(cmp[0]=-1,true))(k_3))break;
            _1=void 0;
          }
          else _1=null;
          return cmp[0];
        }
        break;
      case 3:
        return FailWith("Cannot compare function values.");
      case 4:
        return FailWith("Cannot compare symbol values.");
    }
  }
}
function arrayEquals(a, b){
  let eq, i;
  if(length(a)===length(b)){
    eq=true;
    i=0;
    while(eq&&i<length(a))
      {
        !Equals(get(a, i), get(b, i))?eq=false:void 0;
        i=i+1;
      }
    return eq;
  }
  else return false;
}
function dateEquals(a, b){
  return a.getTime()===b.getTime();
}
function compareArrays(a, b){
  let cmp, i;
  if(length(a)<length(b))return -1;
  else if(length(a)>length(b))return 1;
  else {
    cmp=0;
    i=0;
    while(cmp===0&&i<length(a))
      {
        cmp=Compare(get(a, i), get(b, i));
        i=i+1;
      }
    return cmp;
  }
}
function compareDates(a, b){
  return Compare(a.getTime(), b.getTime());
}
function Hash(o){
  const m=typeof o;
  return m=="function"?0:m=="boolean"?o?1:0:m=="number"?o:m=="string"?hashString(o):m=="object"?o==null?0:o instanceof Array?hashArray(o):hashObject(o):m=="bigint"?hashString(String(o)):m=="symbol"?hashString(o.description):0;
}
function hashString(s){
  let hash;
  if(s===null)return 0;
  else {
    hash=5381;
    for(let i=0, _1=s.length-1;i<=_1;i++)hash=hashMix(hash, s[i].charCodeAt());
    return hash;
  }
}
function hashArray(o){
  let h;
  h=-34948909;
  for(let i=0, _1=length(o)-1;i<=_1;i++)h=hashMix(h, Hash(get(o, i)));
  return h;
}
function hashObject(o){
  if("GetHashCode"in o)return o.GetHashCode();
  else {
    const ____=hashMix;
    const h=[0];
    let k;
    for(var k_1 in o)if(((key) => {
      h[0]=____(____(h[0], hashString(key)), Hash(o[key]));
      return false;
    })(k_1))break;
    return h[0];
  }
}
function hashMix(x, y){
  return(x<<5)+x+y;
}
function Some(Value){
  return{$:1, $0:Value};
}
function json(text){
  return JSON.parse(asText_1(text));
}
function tryJson(text){
  try {
    return isBlank_1(text)?null:Some(json(text));
  }
  catch(m){
    return null;
  }
}
function New_1(type, requestId, streamKey){
  return{
    type:type, 
    requestId:requestId, 
    streamKey:streamKey
  };
}
function New_2(type, requestId, streamKey, count){
  return{
    type:type, 
    requestId:requestId, 
    streamKey:streamKey, 
    count:count
  };
}
let _c=Lazy((_i) => class $StartupCode_Client {
  static {
    _c=_i(this);
  }
  static requestSeq;
  static pendingCommandSeq;
  static maxSnapshotRecords;
  static watermarkStore;
  static pendingStore;
  static snapshotStore;
  static databaseVersion;
  static databaseName;
  static initializeClientExtensionGlobalsOnce;
  static currentAclSnapshotJson;
  static currentAclSnapshot;
  static runtimeAppendPageShapes;
  static registeredRenderers;
  static defaultCacheLimit;
  static defaultRenderLimit;
  static doc;
  static {
    this.doc=globalThis.document;
    this.defaultRenderLimit=200;
    this.defaultCacheLimit=1000;
    this.registeredRenderers=[];
    this.runtimeAppendPageShapes=[];
    this.currentAclSnapshot=null;
    this.currentAclSnapshotJson="";
    this.initializeClientExtensionGlobalsOnce=(initializeClientExtensionGlobals(),0);
    this.databaseName="PulseTrade.Comm.Spa.BrowserDb";
    this.databaseVersion=3;
    this.snapshotStore="uiSnapshots";
    this.pendingStore="pendingCommands";
    this.watermarkStore="streamWatermarks";
    this.maxSnapshotRecords=256;
    this.pendingCommandSeq=0;
    this.requestSeq=0;
  }
});
function TrimEnd(s, t){
  let i, go;
  if(Equals(t, null)||t.length==0)return TrimEndWS(s);
  else {
    i=s.length-1;
    go=true;
    while(i>=0&&go)
      ((() => {
        const c=s[i];
        return exists((y) => c===y, t)?void(i=i-1):void(go=false);
      })());
    return Substring(s, 0, i+1);
  }
}
function concat_1(separator, strings){
  return ofSeq(strings).join(separator);
}
function TrimEndWS(s){
  return s.replace(new RegExp("\\s+$"), "");
}
function Trim(s){
  return s.replace(new RegExp("^\\s+"), "").replace(new RegExp("\\s+$"), "");
}
function StartsWith(t, s){
  return t.substring(0, s.length)==s;
}
function Replace(subject, search, replace){
  function replaceLoop(subj){
    const index=subj.indexOf(search);
    if(index!==-1){
      const replaced=ReplaceOnce(subj, search, replace);
      const nextStartIndex=index+replace.length;
      return Substring(replaced, 0, index+replace.length)+replaceLoop(replaced.substring(nextStartIndex));
    }
    else return subj;
  }
  return replaceLoop(subject);
}
function TrimStart(s, t){
  let i, go;
  if(Equals(t, null)||t.length==0)return TrimStartWS(s);
  else {
    i=0;
    go=true;
    while(i<s.length&&go)
      ((() => {
        const c=s[i];
        return exists((y) => c===y, t)?void(i=i+1):void(go=false);
      })());
    return s.substring(i);
  }
}
function Substring(s, ix, ct){
  return s.substr(ix, ct);
}
function ReplaceOnce(string, search, replace){
  return string.replace(search, replace);
}
function TrimStartWS(s){
  return s.replace(new RegExp("^\\s+"), "");
}
function IsNullOrWhiteSpace(x){
  return x==null||(new RegExp("^\\s*$")).test(x);
}
function SplitChars(s, sep, opts){
  return Split(s, new RegExp("["+RegexEscape(sep.join(""))+"]"), opts);
}
function Split(s, pat, opts){
  return opts===1?filter((x) => x!=="", SplitWith(s, pat)):SplitWith(s, pat);
}
function RegexEscape(s){
  return s.replace(new RegExp("[-\\/\\\\^$*+?.()|[\\]{}]", "g"), "\\$&");
}
function SplitWith(str, pat){
  return str.split(pat);
}
function NewFromSeq(fields){
  let _1;
  const r={};
  const e=Get(fields);
  try {
    while(e.MoveNext())
      {
        const f=e.Current;
        r[f[0]]=f[1];
      }
    _1=void 0;
  }
  finally {
    const _2=e;
    if(typeof _2=="object"&&isIDisposable(_2))e.Dispose();
  }
  return r;
}
class FSharpList {
  static Empty=Create(FSharpList, {$:0});
  static Cons(Head, Tail){
    return Create(FSharpList, {
      $:1, 
      $0:Head, 
      $1:Tail
    });
  }
  GetEnumerator(){
    return new T(this, null, (e) => {
      const m=e.s;
      if(m.$==0)return false;
      else {
        const xs=m.$1;
        e.c=m.$0;
        e.s=xs;
        return true;
      }
    }, void 0);
  }
  $;
  $0;
  $1;
}
class Object_1 {
  Equals(obj){
    return this===obj;
  }
  GetHashCode(){
    return -1;
  }
}
function TryParse(s, r){
  return TryParseBigInt(s, -9223372036854775808n, 9223372036854775807n, r);
}
function New_3(pageId, tabId, path, title, setName, shape, description, keyPlaceholder, valuePlaceholder, defaultKey, tags){
  return{
    pageId:pageId, 
    tabId:tabId, 
    path:path, 
    title:title, 
    setName:setName, 
    shape:shape, 
    description:description, 
    keyPlaceholder:keyPlaceholder, 
    valuePlaceholder:valuePlaceholder, 
    defaultKey:defaultKey, 
    tags:tags
  };
}
function length(arr){
  return arr.dims===2?arr.length*arr.length:arr.length;
}
function get(arr, n){
  checkBounds(arr, n);
  return arr[n];
}
function checkBounds(arr, n){
  if(n<0||n>=arr.length)FailWith("Index was outside the bounds of the array.");
}
function New_4(pageId, mode, setName, keys){
  return{
    pageId:pageId, 
    mode:mode, 
    setName:setName, 
    keys:keys
  };
}
function New_5(streamPageId, lineageKind, legacyPageIdAlias, readsLegacyPageStreams, readRepairPolicy){
  return{
    streamPageId:streamPageId, 
    lineageKind:lineageKind, 
    legacyPageIdAlias:legacyPageIdAlias, 
    readsLegacyPageStreams:readsLegacyPageStreams, 
    readRepairPolicy:readRepairPolicy
  };
}
function New_6(streamPageId, lineageKind, legacyPageIdAlias, readsLegacyPageStreams, readRepairPolicy, candidateValueStreamKeys, candidateValueStreamCount, candidateKeyRegistryStreamKeys, candidateKeyRegistryStreamCount){
  return{
    streamPageId:streamPageId, 
    lineageKind:lineageKind, 
    legacyPageIdAlias:legacyPageIdAlias, 
    readsLegacyPageStreams:readsLegacyPageStreams, 
    readRepairPolicy:readRepairPolicy, 
    candidateValueStreamKeys:candidateValueStreamKeys, 
    candidateValueStreamCount:candidateValueStreamCount, 
    candidateKeyRegistryStreamKeys:candidateKeyRegistryStreamKeys, 
    candidateKeyRegistryStreamCount:candidateKeyRegistryStreamCount
  };
}
function New_7(commandId, serverRealityId, kind, target, url, method, payloadJson, status){
  return{
    commandId:commandId, 
    serverRealityId:serverRealityId, 
    kind:kind, 
    target:target, 
    url:url, 
    method:method, 
    payloadJson:payloadJson, 
    status:status
  };
}
function ofArray(arr){
  let r;
  r=FSharpList.Empty;
  for(let i=length(arr)-1, _1=0;i>=_1;i--)r=FSharpList.Cons(get(arr, i), r);
  return r;
}
function map_1(f, x){
  let r, l, go;
  if(x.$==0)return x;
  else {
    const res=Create(FSharpList, {$:1});
    r=res;
    l=x;
    go=true;
    while(go)
      {
        r.$0=f(l.$0);
        l=l.$1;
        if(l.$==0)go=false;
        else {
          const t=Create(FSharpList, {$:1});
          r=(r.$1=t,t);
        }
      }
    r.$1=FSharpList.Empty;
    return res;
  }
}
function head(l){
  return l.$==1?l.$0:listEmpty();
}
function tail(l){
  return l.$==1?l.$1:listEmpty();
}
function listEmpty(){
  return FailWith("The input list was empty.");
}
function New_8(status, page, bucketCount, maxSequence, keyMaxSequence, lineage, lineageHealth, buckets){
  return{
    status:status, 
    page:page, 
    bucketCount:bucketCount, 
    maxSequence:maxSequence, 
    keyMaxSequence:keyMaxSequence, 
    lineage:lineage, 
    lineageHealth:lineageHealth, 
    buckets:buckets
  };
}
function New_9(keyId, keys, displayName, setName, valueCount, minSequence, maxSequence, updatedAtUtc, values){
  return{
    keyId:keyId, 
    keys:keys, 
    displayName:displayName, 
    setName:setName, 
    valueCount:valueCount, 
    minSequence:minSequence, 
    maxSequence:maxSequence, 
    updatedAtUtc:updatedAtUtc, 
    values:values
  };
}
function New_10(pageId, keyJson, valueText, direction, tags){
  return{
    pageId:pageId, 
    keyJson:keyJson, 
    valueText:valueText, 
    direction:direction, 
    tags:tags
  };
}
function New_11(pageId, keyJson, displayName){
  return{
    pageId:pageId, 
    keyJson:keyJson, 
    displayName:displayName
  };
}
function New_12(pageId, keyJson, rawArgu, tags){
  return{
    pageId:pageId, 
    keyJson:keyJson, 
    rawArgu:rawArgu, 
    tags:tags
  };
}
function New_13(pageId){
  return{pageId:pageId};
}
function New_14(pageId, keyId){
  return{pageId:pageId, keyId:keyId};
}
function New_15(type, requestId, pageId, title, setName, streamKey, actorAddress, rawArgu, renderMode, tags, browserId, tabId){
  return{
    type:type, 
    requestId:requestId, 
    pageId:pageId, 
    title:title, 
    setName:setName, 
    streamKey:streamKey, 
    actorAddress:actorAddress, 
    rawArgu:rawArgu, 
    renderMode:renderMode, 
    tags:tags, 
    browserId:browserId, 
    tabId:tabId
  };
}
function delay(f){
  return{GetEnumerator:() => Get(f())};
}
function append_2(s1, s2){
  return{GetEnumerator:() => {
    const e1=Get(s1);
    const first=[true];
    return new T(e1, null, (x) => {
      if(x.s.MoveNext()){
        x.c=x.s.Current;
        return true;
      }
      else {
        const x_1=x.s;
        if(!Equals(x_1, null))x_1.Dispose();
        else null;
        x.s=null;
        return first[0]&&(first[0]=false,x.s=Get(s2),x.s.MoveNext()?(x.c=x.s.Current,true):(x.s.Dispose(),x.s=null,false));
      }
    }, (x) => {
      const x_1=x.s;
      if(!Equals(x_1, null))x_1.Dispose();
    });
  }};
}
function distinctBy_1(f, s){
  return{GetEnumerator:() => {
    const o=Get(s);
    const seen=new HashSet("New_3");
    return new T(null, null, (e) => {
      let cur, has;
      if(o.MoveNext()){
        cur=o.Current;
        has=seen.SAdd(f(cur));
        while(!has&&o.MoveNext())
          {
            cur=o.Current;
            has=seen.SAdd(f(cur));
          }
        return has&&(e.c=cur,true);
      }
      else return false;
    }, () => {
      o.Dispose();
    });
  }};
}
function map_2(f, s){
  return{GetEnumerator:() => {
    const en=Get(s);
    return new T(null, null, (e) => en.MoveNext()&&(e.c=f(en.Current),true), () => {
      en.Dispose();
    });
  }};
}
function forall(p, s){
  return!exists_1((x) =>!p(x), s);
}
function distinct_1(s){
  return distinctBy_1((x) => x, s);
}
function exists_1(p, s){
  const e=Get(s);
  try {
    let r;
    r=false;
    while(!r&&e.MoveNext())
      r=p(e.Current);
    return r;
  }
  finally {
    const _1=e;
    if(typeof _1=="object"&&isIDisposable(_1))e.Dispose();
  }
}
function compareWith(f, s1, s2){
  const e1=Get(s1);
  try {
    const e2=Get(s2);
    try {
      let r, loop;
      r=0;
      loop=true;
      while(loop&&r===0)
        if(e1.MoveNext())r=e2.MoveNext()?f(e1.Current, e2.Current):1;
        else if(e2.MoveNext())r=-1;
        else loop=false;
      return r;
    }
    finally {
      const _1=e2;
      if(typeof _1=="object"&&isIDisposable(_1))e2.Dispose();
    }
  }
  finally {
    const _2=e1;
    if(typeof _2=="object"&&isIDisposable(_2))e1.Dispose();
  }
}
function forall2_1(p, s1, s2){
  return!exists2((_1, _2) =>!p(_1, _2), s1, s2);
}
function exists2(p, s1, s2){
  const e1=Get(s1);
  try {
    const e2=Get(s2);
    try {
      let r;
      r=false;
      while(!r&&e1.MoveNext()&&e2.MoveNext())
        r=p(e1.Current, e2.Current);
      return r;
    }
    finally {
      const _1=e2;
      if(typeof _1=="object"&&isIDisposable(_1))e2.Dispose();
    }
  }
  finally {
    const _2=e1;
    if(typeof _2=="object"&&isIDisposable(_2))e1.Dispose();
  }
}
function unfold(f, s){
  return{GetEnumerator:() => new T(s, null, (e) => {
    const m=f(e.s);
    if(m==null)return false;
    else {
      const t=m.$0[0];
      const s_1=m.$0[1];
      e.c=t;
      e.s=s_1;
      return true;
    }
  }, void 0)};
}
function New_16(type, requestId, pageId, title, setName, streamKey, keyJson, valueText, direction, renderMode, idempotencyKey, tags, browserId, tabId){
  return{
    type:type, 
    requestId:requestId, 
    pageId:pageId, 
    title:title, 
    setName:setName, 
    streamKey:streamKey, 
    keyJson:keyJson, 
    valueText:valueText, 
    direction:direction, 
    renderMode:renderMode, 
    idempotencyKey:idempotencyKey, 
    tags:tags, 
    browserId:browserId, 
    tabId:tabId
  };
}
function New_17(type, requestId, streamKey, payload, sourceKind, renderMode, idempotencyKey, tags, browserId, tabId){
  return{
    type:type, 
    requestId:requestId, 
    streamKey:streamKey, 
    payload:payload, 
    sourceKind:sourceKind, 
    renderMode:renderMode, 
    idempotencyKey:idempotencyKey, 
    tags:tags, 
    browserId:browserId, 
    tabId:tabId
  };
}
function New_18(keyId, setName, keys, valueCount, maxSequence, updatedAtUtc, values){
  return{
    keyId:keyId, 
    setName:setName, 
    keys:keys, 
    valueCount:valueCount, 
    maxSequence:maxSequence, 
    updatedAtUtc:updatedAtUtc, 
    values:values
  };
}
function New_19(valueId, keys, createdAtUtc, value, tags){
  return{
    valueId:valueId, 
    keys:keys, 
    createdAtUtc:createdAtUtc, 
    value:value, 
    tags:tags
  };
}
function New_20(maxSequence, buckets){
  return{maxSequence:maxSequence, buckets:buckets};
}
function New_21(nodeCount, actorCount, maxSequence, nodes){
  return{
    nodeCount:nodeCount, 
    actorCount:actorCount, 
    maxSequence:maxSequence, 
    nodes:nodes
  };
}
class HashSet extends Object_1 {
  equals;
  hash;
  data;
  count;
  Contains(item){
    const arr=this.data[this.hash(item)];
    return arr==null?false:this.arrContains(item, arr);
  }
  Remove(item){
    const arr=this.data[this.hash(item)];
    return arr==null?false:this.arrRemove(item, arr)&&(this.count=this.count-1,true);
  }
  SAdd(item){
    return this.add(item);
  }
  arrContains(item, arr){
    let c, i;
    c=true;
    i=0;
    const l=arr.length;
    while(c&&i<l)
      if(this.equals.apply(null, [arr[i], item]))c=false;
      else i=i+1;
    return!c;
  }
  arrRemove(item, arr){
    let c, i;
    c=true;
    i=0;
    const l=arr.length;
    while(c&&i<l)
      if(this.equals.apply(null, [arr[i], item])){
        arr.splice(i, 1);
        c=false;
      }
      else i=i+1;
    return!c;
  }
  add(item){
    const h=this.hash(item);
    const arr=this.data[h];
    return arr==null?(this.data[h]=[item],this.count=this.count+1,true):this.arrContains(item, arr)?false:(arr.push(item),this.count=this.count+1,true);
  }
  GetEnumerator(){
    return Get(concat_2(this.data));
  }
  constructor(i, _1, _2, _3){
    if(i=="New_3"){
      i="New_4";
      _1=[];
      _2=Equals;
      _3=Hash;
    }
    if(i=="New_4"){
      const init=_1;
      const equals=_2;
      const hash=_3;
      super();
      this.equals=equals;
      this.hash=hash;
      this.data=[];
      this.count=0;
      const e=Get(init);
      try {
        while(e.MoveNext())
          this.add(e.Current);
      }
      finally {
        const _4=e;
        if(typeof _4=="object"&&isIDisposable(_4))e.Dispose();
      }
    }
  }
}
function OfArray(a){
  return new FSharpMap("New_1", OfSeq(map_2((_1) => Pair.New(_1[0], _1[1]), a)));
}
function New_22(actorId, displayName, kind, keys, status, routees){
  return{
    actorId:actorId, 
    displayName:displayName, 
    kind:kind, 
    keys:keys, 
    status:status, 
    routees:routees
  };
}
function New_23(nodeId, nodeAddress, status, roles, actors){
  return{
    nodeId:nodeId, 
    nodeAddress:nodeAddress, 
    status:status, 
    roles:roles, 
    actors:actors
  };
}
function New_24(messageId, fromId, toId, scope, body, createdAtUtc){
  return{
    messageId:messageId, 
    fromId:fromId, 
    toId:toId, 
    scope:scope, 
    body:body, 
    createdAtUtc:createdAtUtc
  };
}
function New_25(messages, nextAfterMessageId){
  return{messages:messages, nextAfterMessageId:nextAfterMessageId};
}
function New_26(streamId, newestSequence, cachedCount, source, touchedAt){
  return{
    streamId:streamId, 
    newestSequence:newestSequence, 
    cachedCount:cachedCount, 
    source:source, 
    touchedAt:touchedAt
  };
}
function New_27(type, requestId, fromId, toId, body, tags, browserId, tabId){
  return{
    type:type, 
    requestId:requestId, 
    fromId:fromId, 
    toId:toId, 
    body:body, 
    tags:tags, 
    browserId:browserId, 
    tabId:tabId
  };
}
function New_28(fromId, toId, body, tags){
  return{
    fromId:fromId, 
    toId:toId, 
    body:body, 
    tags:tags
  };
}
function New_29(valueText, keyJson){
  return{valueText:valueText, keyJson:keyJson};
}
function New_30(runId, outcome, manifestPath, finalPath, notePath, summary){
  return{
    runId:runId,
    outcome:outcome,
    manifestPath:manifestPath,
    finalPath:finalPath,
    notePath:notePath,
    summary:summary
  };
}
function New_31(shape, label, badge, className){
  return{
    shape:shape, 
    label:label, 
    badge:badge, 
    className:className
  };
}
function New_32(submitPath, sessionPath, logoutPath, returnUrl, protectedRoute, sessionCookieName, title, lead, providerLabel, aclLabel){
  return{
    submitPath:submitPath, 
    sessionPath:sessionPath, 
    logoutPath:logoutPath, 
    returnUrl:returnUrl, 
    protectedRoute:protectedRoute, 
    sessionCookieName:sessionCookieName, 
    title:title, 
    lead:lead, 
    providerLabel:providerLabel, 
    aclLabel:aclLabel
  };
}
function New_33(userName, password, returnUrl, keepSession){
  return{
    userName:userName, 
    password:password, 
    returnUrl:returnUrl, 
    keepSession:keepSession
  };
}
function New_34(participantId, displayName, login, authenticated, provider, logoutPath){
  return{
    participantId:participantId, 
    displayName:displayName, 
    login:login, 
    authenticated:authenticated, 
    provider:provider, 
    logoutPath:logoutPath
  };
}
function Get(x){
  return x instanceof Array?ArrayEnumerator(x):Equals(typeof x, "string")?StringEnumerator(x):x.GetEnumerator();
}
function ArrayEnumerator(s){
  return new T(0, null, (e) => {
    const i=e.s;
    return i<length(s)&&(e.c=get(s, i),e.s=i+1,true);
  }, void 0);
}
function StringEnumerator(s){
  return new T(0, null, (e) => {
    const i=e.s;
    return i<s.length&&(e.c=s[i],e.s=i+1,true);
  }, void 0);
}
function Get0(x){
  return x instanceof Array?ArrayEnumerator(x):Equals(typeof x, "string")?StringEnumerator(x):"GetEnumerator0"in x?x.GetEnumerator0():x.GetEnumerator();
}
class T extends Object_1 {
  s;
  c;
  n;
  d;
  e;
  MoveNext(){
    const m=this.n(this);
    this.e=m?1:2;
    return m;
  }
  get Current(){
    return this.e===1?this.c:this.e===0?FailWith("Enumeration has not started. Call MoveNext."):FailWith("Enumeration already finished.");
  }
  Dispose(){
    if(this.d)this.d(this);
  }
  constructor(s, c, n, d){
    super();
    this.s=s;
    this.c=c;
    this.n=n;
    this.d=d;
    this.e=0;
  }
}
function New_35(pageId, title, setName, shape, tabId, tabMode, path, description){
  return{
    pageId:pageId, 
    title:title, 
    setName:setName, 
    shape:shape, 
    tabId:tabId, 
    tabMode:tabMode, 
    path:path, 
    description:description
  };
}
function notPresent(){
  throw new KeyNotFoundException("New");
}
function alreadyAdded(){
  throw new ArgumentException("New_2", "An item with the same key has already been added.");
}
class FSharpMap extends Object_1 {
  tree;
  TryFind(k){
    const o=TryFind(Pair.New(k, void 0), this.tree);
    return o==null?null:Some(o.$0.Value);
  }
  Equals(other){
    return this.Count===other.Count&&forall2_1(Equals, this, other);
  }
  get Count(){
    const tree=this.tree;
    return tree==null?0:tree.Count;
  }
  GetEnumerator(){
    return Get(map_2((kv) =>({K:kv.Key, V:kv.Value}), Enumerate(false, this.tree)));
  }
  GetHashCode(){
    return Hash(ofSeq(this));
  }
  CompareTo0(other){
    return compareWith((_1, _2) => Compare(_1, _2), this, other);
  }
  constructor(i, _1){
    if(i=="New_1"){
      const tree=_1;
      super();
      this.tree=tree;
    }
  }
}
class Pair {
  Key;
  Value;
  Equals(other){
    return Equals(this.Key, other.Key);
  }
  GetHashCode(){
    return Hash(this.Key);
  }
  CompareTo0(other){
    return Compare(this.Key, other.Key);
  }
  static New(Key, Value){
    return Create(Pair, {Key:Key, Value:Value});
  }
}
function OfSeq(data){
  const a=ofSeq(distinct_1(data));
  sortInPlace(a);
  return Build(a, 0, a.length-1);
}
function TryFind(v, t){
  const x=(Lookup(v, t))[0];
  return x==null?null:Some(x.Node);
}
function Lookup(k, t){
  let spine, t_1, loop;
  spine=[];
  t_1=t;
  loop=true;
  while(loop)
    if(t_1==null)loop=false;
    else {
      const m=Compare(k, t_1.Node);
      if(m===0)loop=false;
      else m===1?(spine.unshift([true, t_1.Node, t_1.Left]),t_1=t_1.Right):(spine.unshift([false, t_1.Node, t_1.Right]),t_1=t_1.Left);
    }
  return[t_1, spine];
}
function Build(data, min, max){
  if(max-min+1<=0)return null;
  else {
    const center=(min+max)/2>>0;
    return Branch(get(data, center), Build(data, min, center-1), Build(data, center+1, max));
  }
}
function Branch(node, left, right){
  const a=left==null?0:left.Height;
  const b=right==null?0:right.Height;
  let _1=Compare(a, b)===1?a:b;
  let _2=1+_1;
  return New_41(node, left, right, _2, 1+(left==null?0:left.Count)+(right==null?0:right.Count));
}
function Enumerate(flip, t){
  function gen(t_1, spine){
    let t_2;
    while(true)
      {
        if(t_1==null){
          if(spine.$==1){
            const t_3=spine.$0[0];
            const spine_1=spine.$1;
            return Some([t_3, [spine.$0[1], spine_1]]);
          }
          else return null;
        }
        else if(flip){
          t_2=t_1;
          t_1=t_2.Right;
          spine=FSharpList.Cons([t_2.Node, t_2.Left], spine);
        }
        else {
          t_2=t_1;
          t_1=t_2.Left;
          spine=FSharpList.Cons([t_2.Node, t_2.Right], spine);
        }
      }
  }
  return unfold((_1) => gen(_1[0], _1[1]), [t, FSharpList.Empty]);
}
function groupBy(f, a){
  const d=new Dictionary("New_5");
  const keys=[];
  for(let i=0, _1=length(a)-1;i<=_1;i++){
    const c=a[i];
    const k=f(c);
    if(d.ContainsKey(k))d.Item(k).push(c);
    else {
      keys.push(k);
      d.DAdd(k, [c]);
    }
  }
  mapInPlace((k_1) =>[k_1, d.Item(k_1)], keys);
  return keys;
}
function nonNegative(){
  return FailWith("The input must be non-negative.");
}
function insufficient(){
  return FailWith("The input sequence has an insufficient number of elements.");
}
function mapInPlace(f, arr){
  for(let i=0, _1=arr.length-1;i<=_1;i++)arr[i]=f(arr[i]);
}
function mapiInPlace(f, arr){
  for(let i=0, _1=arr.length-1;i<=_1;i++)arr[i]=f(i, arr[i]);
  return arr;
}
function New_36(schema, target, perspective, engine, invocation, body, tags){
  return{
    schema:schema, 
    target:target, 
    perspective:perspective, 
    engine:engine, 
    invocation:invocation, 
    body:body, 
    tags:tags
  };
}
function New_37(mode, scope, participantId, groupId){
  return{
    mode:mode, 
    scope:scope, 
    participantId:participantId, 
    groupId:groupId
  };
}
function New_38(mode, participantId, senderPolicy){
  return{
    mode:mode, 
    participantId:participantId, 
    senderPolicy:senderPolicy
  };
}
function New_39(engine, model, reasoning){
  return{
    engine:engine, 
    model:model, 
    reasoning:reasoning
  };
}
function New_40(mode, approval){
  return{mode:mode, approval:approval};
}
class Exception extends Object_1 { }
class Dictionary extends Object_1 {
  equals;
  hash;
  count;
  data;
  ContainsKey(k){
    const d=this.data[this.hash(k)];
    return d==null?false:exists((a) => this.equals.apply(null, [(KeyValue(a))[0], k]), d);
  }
  Item(k){
    return this.get(k);
  }
  DAdd(k, v){
    this.add(k, v);
  }
  get(k){
    const d=this.data[this.hash(k)];
    return d==null?notPresent():pick((a) => {
      const a_1=KeyValue(a);
      return this.equals.apply(null, [a_1[0], k])?Some(a_1[1]):null;
    }, d);
  }
  add(k, v){
    const h=this.hash(k);
    const d=this.data[h];
    if(d==null){
      this.count=this.count+1;
      this.data[h]=new Array({K:k, V:v});
    }
    else {
      exists((a) => this.equals.apply(null, [(KeyValue(a))[0], k]), d)?alreadyAdded():void 0;
      this.count=this.count+1;
      d.push({K:k, V:v});
    }
  }
  set(k, v){
    const h=this.hash(k);
    const d=this.data[h];
    if(d==null){
      this.count=this.count+1;
      this.data[h]=new Array({K:k, V:v});
    }
    else {
      const m=tryFindIndex((a) => this.equals.apply(null, [(KeyValue(a))[0], k]), d);
      if(m==null){
        this.count=this.count+1;
        d.push({K:k, V:v});
      }
      else d[m.$0]={K:k, V:v};
    }
  }
  GetEnumerator(){
    return Get0(concat(GetFieldValues(this.data)));
  }
  constructor(i, _1, _2, _3){
    if(i=="New_5"){
      i="New_6";
      _1=[];
      _2=Equals;
      _3=Hash;
    }
    if(i=="New_6"){
      const init=_1;
      const equals=_2;
      const hash=_3;
      super();
      this.equals=equals;
      this.hash=hash;
      this.count=0;
      this.data=[];
      const e=Get(init);
      try {
        while(e.MoveNext())
          {
            const x=e.Current;
            this.set(x.K, x.V);
          }
      }
      finally {
        const _4=e;
        if(typeof _4=="object"&&isIDisposable(_4))e.Dispose();
      }
    }
  }
}
let _c_1=Lazy((_i) => class $StartupCode_AIChatClient {
  static {
    _c_1=_i(this);
  }
  static doc;
  static loadedMarkerName;
  static {
    this.loadedMarkerName="CodexFsAiChatLoaded";
    this.doc=globalThis.document;
  }
});
function New_41(Node_1, Left, Right, Height, Count){
  return{
    Node:Node_1, 
    Left:Left, 
    Right:Right, 
    Height:Height, 
    Count:Count
  };
}
function TryParseBigInt(s, min, max, r){
  let o, _1;
  o=0n;
  try {
    _1=(o=BigInt(s),true);
  }
  catch(m_1){
    _1=false;
  }
  const m=[_1, o];
  if(m[0]){
    const x=m[1];
    const ok=x===x-x%1n&&x>=min&&x<=max;
    if(ok)r.set(x);
    return ok;
  }
  else return false;
}
function concat_2(o){
  let r=[];
  let k;
  for(var k_1 in o)r.push.apply(r, o[k_1]);
  return r;
}
class KeyNotFoundException extends Error {
  constructor(i, _1){
    if(i=="New"){
      i="New_1";
      _1="The given key was not present in the dictionary.";
    }
    if(i=="New_1"){
      const message=_1;
      super(message);
    }
  }
}
class ArgumentException extends Error {
  constructor(i, _1){
    if(i=="New_2"){
      const message=_1;
      super(message);
    }
  }
}
Main();

