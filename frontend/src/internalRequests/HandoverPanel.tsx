import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
interface View { requestId:number; version:string; handoverId:number|null; state:string|null; receiverId:number|null; summary:string|null; note:string|null;
  canSubmit:boolean; canDecide:boolean; receivers:{id:number;name:string}[]; }
class Failure extends Error { constructor(readonly status:number){super("Handover unavailable");} }
const version=(value:unknown):value is string=>typeof value==="string"&&/^[A-Za-z0-9+/]{11}=$/.test(value);
async function json(path:string,signal:AbortSignal,init?:RequestInit):Promise<unknown>{
  const response=await fetch(path,{...init,signal,credentials:"same-origin",cache:"no-store",redirect:"error"});
  if(!response.ok)throw new Failure(response.status);
  if(!response.headers.get("content-type")?.includes("json"))throw new Error("Invalid response");return response.json();
}
export function InternalRequestHandoverPanel({id,onSaved}:{id:number;onSaved?:(state:string)=>void}){
  const {state,actions}=useAuth();
  const owner=state.status==="AUTHENTICATED"&&!state.pending&&!state.session?.mustChangePassword?state.authorityGeneration:null;
  const [loaded,setLoaded]=useState<{owner:typeof owner;id:number;view:View}|null>(null);
  const [revision,setRevision]=useState(0);const [receiver,setReceiver]=useState("");const [summary,setSummary]=useState("");const [note,setNote]=useState("");const [reason,setReason]=useState("");
  const [message,setMessage]=useState("");const [busy,setBusy]=useState(false);const sending=useRef(false);const controller=useRef<AbortController|null>(null);
  useEffect(()=>{
    setLoaded(null);setReceiver("");setSummary("");setNote("");setReason("");setMessage("");setBusy(false);sending.current=false;
    const abort=new AbortController();controller.current=abort;
    if(owner)void json(`/api/internal-requests/${id}/handover`,abort.signal).then(raw=>{
      const v=raw as View;
      if(!v||v.requestId!==id||!version(v.version)||typeof v.canSubmit!=="boolean"||typeof v.canDecide!=="boolean"
        ||!Array.isArray(v.receivers)||v.receivers.some(r=>!Number.isSafeInteger(r.id)||r.id<=0||typeof r.name!=="string")
        ||!(v.handoverId===null||Number.isSafeInteger(v.handoverId)&&v.handoverId>0)
        ||!(v.state===null||v.state==="PENDING_ACKNOWLEDGMENT")||![v.summary,v.note].every(s=>s===null||typeof s==="string")
        ||v.canDecide&&(!v.handoverId||v.state!=="PENDING_ACKNOWLEDGMENT"))throw new Error("Invalid handover view");
      if(!abort.signal.aborted)setLoaded({owner,id,view:v});
    }).catch(e=>{if(!abort.signal.aborted){setMessage("Handover unavailable. Reload current access and responsibility.");if(e instanceof Failure&&e.status===401)actions.invalidateSession(owner!);}});
    return()=>abort.abort();
  },[id,owner,revision,actions]);
  const view=owner&&loaded?.owner===owner&&loaded.id===id?loaded.view:null;
  async function send(action:"submit"|"acknowledge"|"reject"){
    const abort=controller.current;
    if(!owner||!view||sending.current||!abort||abort.signal.aborted||action==="submit"&&!view.canSubmit||action!=="submit"&&!view.canDecide)return;
    if(action==="submit"&&(!view.receivers.some(r=>r.id===Number(receiver))||!summary.trim())||action==="reject"&&!reason.trim()){setMessage("Select a currently eligible receiver and enter the summary, or enter the rejection reason.");return;}
    sending.current=true;setBusy(true);setMessage("");
    try{
      const csrf=await json("/api/auth/csrf",abort.signal) as {csrfToken?:unknown};
      if(typeof csrf?.csrfToken!=="string"||!csrf.csrfToken||abort.signal.aborted)throw new Error("Security check failed");
      const result=await json(`/api/internal-requests/${id}/handover${action==="submit"?"":"/decision"}`,abort.signal,{
        method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${view.version}"`},
        body:JSON.stringify(action==="submit"?{receiverId:Number(receiver),summary:summary.trim(),note:note.trim()||null}:{handoverId:view.handoverId,acknowledge:action==="acknowledge",reason:reason.trim()||null})
      }) as {requestId:number;handoverId:number;state:string;version:string;message:string};
      if(result.requestId!==id||!Number.isSafeInteger(result.handoverId)||result.handoverId<=0||!version(result.version)||result.message!=="MSG08"
        ||result.state!==(action==="submit"?"PENDING_ACKNOWLEDGMENT":action==="acknowledge"?"ACKNOWLEDGED":"REJECTED"))throw new Error("Handover not confirmed");
      if(!abort.signal.aborted){setLoaded(null);setSummary("");setNote("");setReason("");setMessage(`MSG08: Handover ${result.state}. Reload current responsibility.`);onSaved?.(result.state);}
    }catch(e){if(!abort.signal.aborted){setLoaded(null);setMessage(e instanceof Failure&&e.status===412?"Request changed. Reload before deciding.":"Handover not confirmed. Reload; no automatic retry was sent.");if(e instanceof Failure&&e.status===401)actions.invalidateSession(owner);}}
    finally{if(!abort.signal.aborted){sending.current=false;setBusy(false);}}
  }
  if(!owner)return null;
  return <section aria-label="Request handover" aria-busy={busy}>
    {message&&<p role="status">{message}</p>}<button disabled={busy} onClick={()=>setRevision(v=>v+1)}>Reload handover</button>
    {view?.state&&<><p>Handover: {view.state}. The sender remains responsible until acknowledgment.</p><p>{view.summary}</p><p>{view.note}</p></>}
    {view?.canSubmit&&<>
      {view.receivers.length===0&&<p role="status">No eligible receiver currently has permission to open this request. The current handler remains responsible.</p>}
      <label>New handler<select aria-label="New handler" value={receiver} disabled={busy} onChange={e=>setReceiver(e.target.value)}><option value="">Select</option>{view.receivers.map(r=><option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
      <label>Status summary<textarea maxLength={4000} value={summary} disabled={busy} onChange={e=>setSummary(e.target.value)}/></label>
      <label>Handover notes<textarea maxLength={4000} value={note} disabled={busy} onChange={e=>setNote(e.target.value)}/></label>
      <button disabled={busy||!view.receivers.some(r=>r.id===Number(receiver))||!summary.trim()} onClick={()=>void send("submit")}>Submit handover</button>
    </>}
    {view?.canDecide&&<>
      <label>Handover rejection reason<textarea maxLength={4000} value={reason} disabled={busy} onChange={e=>setReason(e.target.value)}/></label>
      <button disabled={busy} onClick={()=>void send("acknowledge")}>Acknowledge handover</button>
      <button disabled={busy} onClick={()=>void send("reject")}>Reject handover</button>
    </>}
  </section>;
}
