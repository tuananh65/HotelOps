import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { safeId } from "./contracts";

type Summary = {employeeId:number;reason:string;departmentId:number;status:string;startedAt:string;endedAt:string|null};
export function UnscheduledWorkdaySummary({branchId,workdayId}:{branchId:number;workdayId:number}) {
  const [result,setResult]=useState<Summary|null>(null),[error,setError]=useState("");
  useEffect(()=>{
    const abort=new AbortController();setResult(null);setError("");
    void createHttpClient()({method:"GET",url:`/api/branches/${branchId}/workdays/${workdayId}/unscheduled-summary`,expectedStatus:200,signal:abort.signal,
      decode(v){
        if(!isObject(v)||!isObject(v.workday)||!safeId(v.employeeId)||typeof v.reason!=="string"||v.reason.length>1000)return;
        const d=v.workday;
        if(d.branchId!==branchId||d.workdayId!==workdayId||d.isUnscheduled!==true||d.shiftAssignmentId!==null
          ||!safeId(d.departmentId)||!['WORKING','ENDED'].includes(String(d.status))||typeof d.startedAtUtc!=="string"
          ||!(d.endedAtUtc===null||typeof d.endedAtUtc==="string"))return;
        return {employeeId:v.employeeId,reason:v.reason,departmentId:d.departmentId,status:d.status,startedAt:d.startedAtUtc,endedAt:d.endedAtUtc} as Summary;
      }}).then(r=>{if(abort.signal.aborted)return;if(r.ok)setResult(r.data);else setError("This workday is unavailable or you no longer have access.");});
    return()=>abort.abort();
  },[branchId,workdayId]);
  return <section aria-label="Unscheduled workday notification"><h2>Unscheduled workday {workdayId}</h2>
    {error?<p role="alert">{error}</p>:result?<><p>Employee {result.employeeId}; branch {branchId}; department {result.departmentId}.</p>
      <p>{result.status}: {result.startedAt}{result.endedAt?` — ${result.endedAt}`:""}</p><p>Reason: {result.reason}</p></>:<p>Loading authorized workday...</p>}
  </section>;
}
