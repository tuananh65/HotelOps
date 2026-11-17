import {useEffect,useRef,useState} from "react";
import {useAuth} from "../auth/AuthProvider";
import type {AuthorityGeneration} from "../auth/authStore";
import type {TimeLogEntry} from "./timeLogApi";
import {TimeLogFailure} from "./timeLogApi";
import {saveCorrection} from "./timeCorrectionsApi";
export const hotelInput=(value:string)=>new Date(Date.parse(value)+7*3600000).toISOString().slice(0,19);
export const utcInput=(value:string)=>new Date(value+"+07:00").toISOString();
export function CorrectionRequestForm({branch,interval,owner,onSaved}:{branch:number;interval:TimeLogEntry;owner:AuthorityGeneration;onSaved:()=>void}) {
  const {actions}=useAuth();const [open,setOpen]=useState(false);const [start,setStart]=useState(hotelInput(interval.start));const [end,setEnd]=useState(interval.end?hotelInput(interval.end):"");
  const [reason,setReason]=useState("");const [busy,setBusy]=useState(false);const [error,setError]=useState("");const [locked,setLocked]=useState(false);
  const active=useRef<AbortController|null>(null);const saving=useRef(false);useEffect(()=>()=>active.current?.abort(),[]);
  if(!interval.end)return null;
  return <><button disabled={busy||locked} onClick={()=>setOpen(v=>!v)}>Request correction</button>{open&&<form onSubmit={e=>{e.preventDefault();if(saving.current||locked)return;saving.current=true;setBusy(true);setError("");const controller=new AbortController();active.current=controller;
    void (async()=>{try{await saveCorrection(branch,interval.intervalId,interval.version,{start:utcInput(start),end:utcInput(end),reason},false,controller.signal);if(!controller.signal.aborted){setLocked(true);onSaved();}}
      catch(error){if(!controller.signal.aborted){if(error instanceof TimeLogFailure&&error.status===401)actions.invalidateSession(owner);setError(error instanceof Error?error.message:"Outcome unconfirmed. Reload before retrying.");setLocked(true);}}
      finally{saving.current=false;if(!controller.signal.aborted)setBusy(false);}})();}}>
    <p>Proposed times in hotel time (UTC+7). Refresh the time log after any conflict.</p>
    <label>Start<input type="datetime-local" step="1" required value={start} onChange={e=>setStart(e.target.value)}/></label>
    <label>End<input type="datetime-local" step="1" required value={end} onChange={e=>setEnd(e.target.value)}/></label>
    <label>Reason<textarea required minLength={10} maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}/></label>
    <button disabled={busy||locked||reason.trim().length<10}>Submit correction</button>{error&&<p role="alert">{error}</p>}
  </form>}</>;
}
