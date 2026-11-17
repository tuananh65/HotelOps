import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import { safeId } from "./contracts";
type Assignment={id:number;employeeId:number;employeeName:string;status:string;version:string};
type Shift={id:number;templateId:number;templateName:string;date:string;startUtc:string;endUtc:string;status:string;requiredStaff:number;assignments:Assignment[]};
type Page={branchId:number;departmentId:number;from:string;to:string;timezone:string;shifts:Shift[];nextCursor:number|null};
function assignment(v:unknown):v is Assignment {return isObject(v)&&safeId(v.id)&&safeId(v.employeeId)&&typeof v.employeeName==="string"&&typeof v.status==="string"&&typeof v.version==="string"&&/^"[A-F0-9]{64}"$/.test(v.version);}
function shift(v:unknown):v is Shift {return isObject(v)&&safeId(v.id)&&safeId(v.templateId)&&typeof v.templateName==="string"&&typeof v.date==="string"
  &&typeof v.startUtc==="string"&&Number.isFinite(Date.parse(v.startUtc))&&typeof v.endUtc==="string"&&Number.isFinite(Date.parse(v.endUtc))&&typeof v.status==="string"
  &&typeof v.requiredStaff==="number"&&Number.isSafeInteger(v.requiredStaff)&&Array.isArray(v.assignments)&&v.assignments.every(assignment);}
export function ShiftRosterPanel({revision=0}:{revision?:number}) {
  const {state,actions}=useAuth(),owner=state.authorityGeneration;
  const [branch,setBranch]=useState(""),[department,setDepartment]=useState(""),[from,setFrom]=useState("");
  const [result,setResult]=useState<Page|null>(null),[error,setError]=useState(""),[busy,setBusy]=useState(false);
  const [selected,setSelected]=useState<Assignment|null>(null),[reason,setReason]=useState(""),[confirmed,setConfirmed]=useState(false);
  const [notice,setNotice]=useState<{owner:typeof owner;branch:number;department:number;text:string}|null>(null);
  const observedRevision=useRef(revision);
  const request=useRef<AbortController|null>(null),sending=useRef(false),current=useRef(owner);current.current=owner;
  useEffect(()=>{setResult(null);setSelected(null);setError("");return()=>request.current?.abort();},[owner]);
  useEffect(()=>{
    if(observedRevision.current===revision||busy||sending.current)return;
    observedRevision.current=revision;
    // Refresh only an already admitted roster; never infer a new branch or department.
    if(result)void load();
  },[revision,busy,result]);
  function clear(){request.current?.abort();setResult(null);setError("");}
  async function load(after=0) {
    if(sending.current)return;
    const b=Number(branch),d=Number(department);
    if(!safeId(b)||!safeId(d)||!/^\d{4}-\d{2}-\d{2}$/.test(from)){setError("Select branch, department and the first date of the seven-day window.");return;}
    const abort=new AbortController();request.current=abort;sending.current=true;setBusy(true);setError("");setResult(null);setSelected(null);
    try {
      const response=await createHttpClient()({method:"GET",url:"/api/branches/"+b+"/departments/"+d+"/shift-roster?from="+from+"&afterId="+after,expectedStatus:200,signal:abort.signal,
        decode(v){
          if(!isObject(v)||v.branchId!==b||v.departmentId!==d||v.from!==from||typeof v.to!=="string"||typeof v.timezone!=="string"
            ||!Array.isArray(v.shifts)||v.shifts.length>50||!v.shifts.every(shift)||!(v.nextCursor===null||safeId(v.nextCursor)))return;
          return v as Page;
        }});
      if(abort.signal.aborted||current.current!==owner)return;
      if(response.ok)setResult(response.data);
      else {setError("Roster unavailable. Current department authority and SHIFT_MANAGE are required.");if(response.error.kind==="http"&&response.error.status===401)actions.invalidateSession(owner);}
    } finally {sending.current=false;if(current.current===owner)setBusy(false);}
  }
  async function cancel() {
    if(sending.current||!selected||!result||!confirmed)return;
    const target=selected,snapshot=result,abort=new AbortController();request.current=abort;sending.current=true;setBusy(true);setError("");setNotice(null);let submitted=false,committed=false;
    try {
      const first=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal});
      if(!first.ok)throw Error("Session validation failed.");
      const csrf:unknown=await first.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Session changed.");
      submitted=true;
      const response=await fetch("/api/branches/"+snapshot.branchId+"/departments/"+snapshot.departmentId+"/shift-roster/assignments/"+target.id+"/cancel",{
        method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":target.version},body:JSON.stringify({reason,confirmation:confirmed})});
      if(response.status!==204){const problem:unknown=await response.json();throw Error(isObject(problem)&&typeof problem.title==="string"?problem.title:"Cancellation was not confirmed.");}
      if(!abort.signal.aborted&&current.current===owner){committed=true;setNotice({owner,branch:snapshot.branchId,department:snapshot.departmentId,text:"MSG08: Assignment cancelled; notification queued."});}
    } catch(e){if(!abort.signal.aborted&&current.current===owner)setError(String(e)+" Reload before retrying.");}
    finally {sending.current=false;if(current.current===owner){setBusy(false);if(submitted){setResult(null);setSelected(null);}}}
    if(committed&&!abort.signal.aborted&&current.current===owner)await load();
  }
  function local(value:string,zone:string) {try{return new Intl.DateTimeFormat(undefined,{dateStyle:"medium",timeStyle:"short",timeZone:zone}).format(new Date(value));}catch{return value+" (UTC)";}}
  return <section><h2>Weekly roster</h2><form onSubmit={e=>{e.preventDefault();void load();}}><fieldset disabled={busy}>
    <label>Branch ID<input required inputMode="numeric" value={branch} onChange={e=>{clear();setBranch(e.target.value);}} /></label>
    <label>Department ID<input required inputMode="numeric" value={department} onChange={e=>{clear();setDepartment(e.target.value);}} /></label>
    <label>Week starting<input required type="date" value={from} onChange={e=>{clear();setFrom(e.target.value);}} /></label><button>Load roster</button>
  </fieldset></form>{busy&&<p>Loading roster...</p>}{notice?.owner===owner&&notice.branch===Number(branch)&&notice.department===Number(department)&&<p role="status">{notice.text}</p>}{error&&<p role="alert">{error}</p>}
  {result&&<><p>{result.from} through {result.to}; {result.timezone}. Includes cancelled assignments.</p>
    {result.shifts.length===0?<p>No shifts in this page.</p>:<table><thead><tr><th>Shift</th><th>Date / time</th><th>Status</th><th>Required staff</th><th>Assignments</th></tr></thead><tbody>
      {result.shifts.map(s=><tr key={s.id}><td>{s.templateName} (shift {s.id}, template {s.templateId})</td><td>{s.date}<br/>{local(s.startUtc,result.timezone)} ? {local(s.endUtc,result.timezone)}</td>
        <td>{s.status}</td><td>{s.requiredStaff}</td><td>{s.assignments.length===0?"No assignments":<ul>{s.assignments.map(a=><li key={a.id}>{a.employeeName} ({a.employeeId}): {a.status} {a.status==="ASSIGNED"&&s.status==="PLANNED"&&Date.parse(s.startUtc)>Date.now()&&<button disabled={busy} onClick={()=>{setSelected(a);setReason("");setConfirmed(false);}}>Cancel assignment</button>}</li>)}</ul>}</td></tr>)}
    </tbody></table>}
    {result.nextCursor!==null&&<button disabled={busy} onClick={()=>void load(result.nextCursor!)}>Next page</button>}
  </>}
  {selected&&result&&<form onSubmit={e=>{e.preventDefault();void cancel();}}><fieldset disabled={busy}><legend>Cancel assignment for {selected.employeeName}</legend>
    <label>Reason<textarea required minLength={10} maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}/></label>
    <label><input type="checkbox" required checked={confirmed} onChange={e=>setConfirmed(e.target.checked)}/>Confirm cancellation of this future assignment.</label>
    <button>Confirm cancellation</button><button type="button" onClick={()=>setSelected(null)}>Keep assignment</button>
  </fieldset></form>}
  </section>;
}
