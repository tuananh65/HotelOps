import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import { safeId } from "./contracts";
type Assignment={id:number;employeeId:number;employeeName:string;status:string};
type Shift={id:number;templateId:number;templateName:string;date:string;startUtc:string;endUtc:string;status:string;requiredStaff:number;assignments:Assignment[]};
type Page={branchId:number;departmentId:number;from:string;to:string;timezone:string;shifts:Shift[];nextCursor:number|null};
function assignment(v:unknown):v is Assignment {return isObject(v)&&safeId(v.id)&&safeId(v.employeeId)&&typeof v.employeeName==="string"&&typeof v.status==="string";}
function shift(v:unknown):v is Shift {return isObject(v)&&safeId(v.id)&&safeId(v.templateId)&&typeof v.templateName==="string"&&typeof v.date==="string"
  &&typeof v.startUtc==="string"&&Number.isFinite(Date.parse(v.startUtc))&&typeof v.endUtc==="string"&&Number.isFinite(Date.parse(v.endUtc))&&typeof v.status==="string"
  &&typeof v.requiredStaff==="number"&&Number.isSafeInteger(v.requiredStaff)&&Array.isArray(v.assignments)&&v.assignments.every(assignment);}
export function ShiftRosterPanel() {
  const {state,actions}=useAuth(),owner=state.authorityGeneration;
  const [branch,setBranch]=useState(""),[department,setDepartment]=useState(""),[from,setFrom]=useState("");
  const [result,setResult]=useState<Page|null>(null),[error,setError]=useState(""),[busy,setBusy]=useState(false);
  const request=useRef<AbortController|null>(null),sending=useRef(false),current=useRef(owner);current.current=owner;
  useEffect(()=>{setResult(null);setError("");return()=>request.current?.abort();},[owner]);
  function clear(){request.current?.abort();setResult(null);setError("");}
  async function load(after=0) {
    if(sending.current)return;
    const b=Number(branch),d=Number(department);
    if(!safeId(b)||!safeId(d)||!/^\d{4}-\d{2}-\d{2}$/.test(from)){setError("Select branch, department and the first date of the seven-day window.");return;}
    const abort=new AbortController();request.current=abort;sending.current=true;setBusy(true);setError("");setResult(null);
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
  function local(value:string,zone:string) {try{return new Intl.DateTimeFormat(undefined,{dateStyle:"medium",timeStyle:"short",timeZone:zone}).format(new Date(value));}catch{return value+" (UTC)";}}
  return <section><h2>Weekly roster</h2><form onSubmit={e=>{e.preventDefault();void load();}}><fieldset disabled={busy}>
    <label>Branch ID<input required inputMode="numeric" value={branch} onChange={e=>{clear();setBranch(e.target.value);}} /></label>
    <label>Department ID<input required inputMode="numeric" value={department} onChange={e=>{clear();setDepartment(e.target.value);}} /></label>
    <label>Week starting<input required type="date" value={from} onChange={e=>{clear();setFrom(e.target.value);}} /></label><button>Load roster</button>
  </fieldset></form>{busy&&<p>Loading roster...</p>}{error&&<p role="alert">{error}</p>}
  {result&&<><p>{result.from} through {result.to}; {result.timezone}. Includes cancelled assignments.</p>
    {result.shifts.length===0?<p>No shifts in this page.</p>:<table><thead><tr><th>Shift</th><th>Date / time</th><th>Status</th><th>Required staff</th><th>Assignments</th></tr></thead><tbody>
      {result.shifts.map(s=><tr key={s.id}><td>{s.templateName} (shift {s.id}, template {s.templateId})</td><td>{s.date}<br/>{local(s.startUtc,result.timezone)} ? {local(s.endUtc,result.timezone)}</td>
        <td>{s.status}</td><td>{s.requiredStaff}</td><td>{s.assignments.length===0?"No assignments":<ul>{s.assignments.map(a=><li key={a.id}>{a.employeeName} ({a.employeeId}): {a.status}</li>)}</ul>}</td></tr>)}
    </tbody></table>}
    {result.nextCursor!==null&&<button disabled={busy} onClick={()=>void load(result.nextCursor!)}>Next page</button>}
  </>}
  </section>;
}
