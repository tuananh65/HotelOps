import {useEffect,useRef,useState} from "react";
import {Link,useSearchParams} from "react-router-dom";
import {useAuth} from "../auth/AuthProvider";
import {TimeLogFailure} from "./timeLogApi";
import {readCorrection,readCorrections,saveCorrection,type Correction,type CorrectionDetail} from "./timeCorrectionsApi";
import {hotelInput,utcInput} from "./CorrectionRequestForm";
const stamp=(v:string)=>new Intl.DateTimeFormat("en-GB",{timeZone:"Asia/Ho_Chi_Minh",dateStyle:"short",timeStyle:"medium"}).format(new Date(v));
export function TimeCorrectionsPage(){
  const {state,actions}=useAuth();const [params]=useSearchParams();
  const owner=state.status==="AUTHENTICATED"&&state.pending===null&&!state.session?.mustChangePassword?state.authorityGeneration:null;
  const [branch,setBranch]=useState(params.get("branchId")??"");const [department,setDepartment]=useState("");
  const [query,setQuery]=useState<{branch:number;department:number|null;page:number;id:number|null;revision:number}|null>(()=>{const b=Number(params.get("branchId")),id=Number(params.get("correctionId"));return Number.isSafeInteger(b)&&b>0&&Number.isSafeInteger(id)&&id>0?{branch:b,department:null,page:1,id,revision:0}:null;});
  const [result,setResult]=useState<{owner:typeof owner;key:string;items?:Correction[];total?:number;detail?:CorrectionDetail;error?:string}|null>(null);
  const [start,setStart]=useState("");const [end,setEnd]=useState("");const [reason,setReason]=useState("");const [busy,setBusy]=useState(false);const [locked,setLocked]=useState(false);const [message,setMessage]=useState("");
  const active=useRef<AbortController|null>(null);const saving=useRef(false);const key=JSON.stringify(query);
  useEffect(()=>{setResult(null);setLocked(false);setReason("");setMessage("");if(!owner||!query)return;const c=new AbortController();active.current=c;
    void (async()=>{try{if(query.id){const detail=await readCorrection(query.branch,query.id,c.signal);if(!c.signal.aborted){setResult({owner,key,detail});setStart(hotelInput(detail.correction.proposedStart));setEnd(hotelInput(detail.correction.proposedEnd));}}
      else{const data=await readCorrections(query.branch,query.department,query.page,c.signal);if(!c.signal.aborted)setResult({owner,key,...data});}}
      catch(e){if(!c.signal.aborted){if(e instanceof TimeLogFailure&&e.status===401)actions.invalidateSession(owner);setResult({owner,key,error:e instanceof Error?e.message:"Corrections unavailable."});}}})();return()=>{c.abort();active.current?.abort();};
  },[owner,key,actions]);
  if(!owner)return null;const visible=result?.owner===owner&&result.key===key?result:null;const detail=visible?.detail;const row=detail?.correction;
  async function decide(decision:"APPLY"|"REJECT"){
    if(!owner||!query||!row?.canDecide||locked||saving.current)return;saving.current=true;setBusy(true);setMessage("");const c=new AbortController();active.current=c;
    try{await saveCorrection(query.branch,row.correctionId,row.version,{decision,start:decision==="APPLY"?utcInput(start):null,end:decision==="APPLY"?utcInput(end):null,reason:reason.trim()||null},true,c.signal);
      if(!c.signal.aborted){setLocked(true);setMessage("MSG08: Decision saved. Reload to view the updated interval and decision.");}}
    catch(e){if(!c.signal.aborted){if(e instanceof TimeLogFailure&&e.status===401)actions.invalidateSession(owner);setLocked(true);setMessage(e instanceof Error?e.message:"Outcome unconfirmed. Reload before retrying.");}}
    finally{saving.current=false;if(!c.signal.aborted)setBusy(false);}
  }
  return <main><Link to="/my-time-log">My Time Log</Link><h1>Time corrections</h1><p>Hotel time (UTC+7). Department review requires current mapping and TIME_CORRECTION_DECIDE.</p>
    <form onSubmit={e=>{e.preventDefault();const b=Number(branch),d=department?Number(department):null;if(!Number.isSafeInteger(b)||b<=0||d!==null&&(!Number.isSafeInteger(d)||d<=0))return;setQuery({branch:b,department:d,page:1,id:null,revision:(query?.revision??0)+1});}}>
      <label>Branch ID<input required type="number" min="1" value={branch} disabled={busy} onChange={e=>{setBranch(e.target.value);setQuery(null);}}/></label>
      <label>Department ID (empty for my requests)<input type="number" min="1" value={department} disabled={busy} onChange={e=>{setDepartment(e.target.value);setQuery(null);}}/></label><button disabled={busy}>Load corrections</button>
    </form>
    {visible?.error&&<p role="alert">{visible.error}</p>}{query&&!visible&&<p>Loading...</p>}
    {visible?.items&&<><ul>{visible.items.map(r=><li key={r.correctionId}><button onClick={()=>setQuery(q=>q&&({...q,id:r.correctionId}))}>Correction {r.correctionId}: {r.status}, employee {r.employeeId}, {stamp(r.requestedAt)}</button></li>)}</ul>
      <button disabled={query?.page===1} onClick={()=>setQuery(q=>q&&({...q,page:q.page-1}))}>Previous</button><button disabled={(query?.page??1)*50>=(visible.total??0)} onClick={()=>setQuery(q=>q&&({...q,page:q.page+1}))}>Next</button></>}
    {row&&<section><h2>Correction {row.correctionId}: {row.status}</h2><p>{row.kind} {row.workItemId}, employee {row.employeeId}</p>
      <p>Original: {stamp(row.originalStart)} - {stamp(row.originalEnd)}</p><p>Requested: {stamp(row.proposedStart)} - {stamp(row.proposedEnd)}</p><p>{row.reason}</p>
      {row.appliedStart&&row.appliedEnd&&<p>Applied: {stamp(row.appliedStart)} - {stamp(row.appliedEnd)}</p>}{row.decidedAt&&<p>Decided by {row.decidedBy} at {stamp(row.decidedAt)}: {row.decisionReason}</p>}
      <h3>Other intervals in this workday window</h3><ul>{detail?.otherIntervals.map((i,n)=><li key={n}>{stamp(i.start)} - {i.end?stamp(i.end):"Running"}</li>)}</ul>
      {row.canDecide&&<fieldset disabled={busy||locked}><legend>Review correction</legend>
        <label>Applied start<input type="datetime-local" step="1" value={start} onChange={e=>setStart(e.target.value)}/></label><label>Applied end<input type="datetime-local" step="1" value={end} onChange={e=>setEnd(e.target.value)}/></label>
        <label>Decision reason (required for rejection)<textarea maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}/></label>
        <button disabled={!start||!end} onClick={()=>void decide("APPLY")}>Apply correction</button><button disabled={reason.trim().length<10} onClick={()=>void decide("REJECT")}>Reject</button>
      </fieldset>}
      <button disabled={busy} onClick={()=>setQuery(q=>q&&({...q,revision:q.revision+1}))}>Reload correction</button>
    </section>}{message&&<p role="status">{message}</p>}
  </main>;
}
