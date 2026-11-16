import { useEffect, useRef, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { safeId } from "./contracts";
import { createScheduledWorkdayApi } from "./scheduledWorkdayApi";
import { scheduledMessage, type ScheduledWorkdayResponse } from "./scheduledContracts";

type Department = { departmentId: number; code: string; name: string };
export function UnscheduledStartForm({branchId, disabled, current, onBusy, onStarted, onUnknown, invalidate}: {
  branchId: number; disabled: boolean; current: () => boolean; onBusy: (busy: boolean) => void;
  onStarted: (day: ScheduledWorkdayResponse) => void; onUnknown: () => void; invalidate: () => void;
}) {
  const [departments, setDepartments] = useState<Department[]>([]), [department, setDepartment] = useState("");
  const [reason, setReason] = useState(""), [review, setReview] = useState(false), [message, setMessage] = useState("");
  const [api] = useState(() => createScheduledWorkdayApi(globalThis.fetch.bind(globalThis), true));
  const mounted = useRef(true), busy = useRef(false), abort = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();
    void createHttpClient()({method:"GET",url:`/api/branches/${branchId}/workdays/own-departments`,expectedStatus:200,signal:controller.signal,
      decode(v) {
        if (!Array.isArray(v) || !v.every(x => isObject(x) && safeId(x.departmentId) && typeof x.code === "string" && typeof x.name === "string")) return;
        if (new Set(v.map(x=>x.departmentId)).size !== v.length) return;
        return v.map(x=>({departmentId:x.departmentId,code:x.code,name:x.name})) as Department[];
      }}).then(r=>{
        if(controller.signal.aborted || !current()) return;
        if(r.ok){setDepartments(r.data);if(r.data.length===1)setDepartment(String(r.data[0].departmentId));}
        else {setMessage("Verified department memberships unavailable.");if(r.error.kind==="http"&&r.error.status===401)invalidate();}
      });
    return ()=>{mounted.current=false;controller.abort();abort.current?.abort();};
  },[branchId,current]);
  const valid = departments.some(d=>d.departmentId===Number(department)) && reason.trim().length>0 && reason.length<=1000 && !reason.includes("\0");
  async function confirm(){
    if(disabled || busy.current || !review || !valid || !current())return;
    busy.current=true;onBusy(true);setReview(false);abort.current=new AbortController();
    const result=await api.startUnscheduled(branchId,Number(department),reason,()=>mounted.current&&current(),abort.current.signal);
    if(!mounted.current||!current())return;
    if(result.outcome==="CONFIRMED"){setReason("");onStarted(result.data);}
    else {setMessage(scheduledMessage(result));if(result.outcome==="OUTCOME_UNKNOWN")onUnknown();onBusy(false);
      if(result.error.status===401)invalidate();}
    busy.current=false;
  }
  return <section aria-label="Unscheduled start">
    <h2>Start without a shift</h2>
    {departments.length===0&&<p>No verified department membership is available for this branch.</p>}
    <label>Department<select value={department} disabled={disabled} onChange={e=>{setReview(false);setDepartment(e.target.value);}}>
      <option value="">Choose verified department</option>{departments.map(d=><option key={d.departmentId} value={d.departmentId}>{d.code} — {d.name}</option>)}
    </select></label>
    <label>Reason<textarea value={reason} maxLength={1000} disabled={disabled} onChange={e=>{setReview(false);setReason(e.target.value);}}/></label>
    <button disabled={disabled||!valid} onClick={()=>setReview(true)}>Review unscheduled start</button>
    {review&&<div><p>Start in {departments.find(d=>d.departmentId===Number(department))?.name}? The authorized Department Head will be notified.</p>
      <p>{reason}</p><button disabled={disabled||!valid} onClick={()=>void confirm()}>Confirm unscheduled start</button>
      <button disabled={disabled} onClick={()=>setReview(false)}>Cancel review</button></div>}
    {message&&<p role="status">{message}</p>}
  </section>;
}
