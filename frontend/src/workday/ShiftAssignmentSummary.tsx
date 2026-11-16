import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";
import { safeId } from "./contracts";
type Summary={name:string;departmentName:string;shiftDate:string;startUtc:string;endUtc:string;assignmentStatus:string};
export function ShiftAssignmentSummary({branchId,assignmentId}:{branchId:number;assignmentId:number}) {
  const {state,actions}=useAuth(),owner=state.authorityGeneration;
  const [result,setResult]=useState<Summary|null>(null),[error,setError]=useState("");
  useEffect(()=>{
    const abort=new AbortController();setResult(null);setError("");
    void createHttpClient()({method:"GET",url:"/api/branches/"+branchId+"/workdays/own-shifts/"+assignmentId,expectedStatus:200,signal:abort.signal,
      decode(v){
        if(!isObject(v)||v.branchId!==branchId||v.shiftAssignmentId!==assignmentId||!safeId(v.shiftId)||!safeId(v.departmentId)
          ||typeof v.name!=="string"||typeof v.departmentName!=="string"||typeof v.shiftDate!=="string"
          ||typeof v.startUtc!=="string"||typeof v.endUtc!=="string"||v.assignmentStatus!=="ASSIGNED")return;
        return v as Summary;
      }}).then(r=>{
        if(abort.signal.aborted)return;
        if(r.ok)setResult(r.data);
        else {setError("Assignment unavailable or access has changed.");if(r.error.kind==="http"&&r.error.status===401)actions.invalidateSession(owner);}
      });
    return()=>abort.abort();
  },[branchId,assignmentId,owner,actions]);
  return <section aria-label="Assigned shift"><h2>Assigned shift</h2>{error?<p role="alert">{error}</p>:result?<>
    <p>{result.name} ? {result.departmentName}; branch {branchId}</p><p>{result.shiftDate}: {result.assignmentStatus}</p>
    <p>{new Date(result.startUtc).toLocaleString()} ? {new Date(result.endUtc).toLocaleString()}</p>
  </>:<p>Loading authorized assignment...</p>}</section>;
}
