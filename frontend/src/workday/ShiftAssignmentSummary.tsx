import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";
import { safeId } from "./contracts";
type Summary={name:string;departmentName:string;shiftDate:string;startUtc:string;endUtc:string;assignmentStatus:string;cancellationReason:string|null};
export function ShiftAssignmentSummary({branchId,assignmentId}:{branchId:number;assignmentId:number}) {
  const {state,actions}=useAuth(),owner=state.authorityGeneration;
  const [result,setResult]=useState<Summary|null>(null),[error,setError]=useState("");
  useEffect(()=>{
    const abort=new AbortController();setResult(null);setError("");
    void createHttpClient()({method:"GET",url:"/api/branches/"+branchId+"/workdays/own-shifts/"+assignmentId,expectedStatus:200,signal:abort.signal,
      decode(v){
        if(!isObject(v)||v.branchId!==branchId||v.shiftAssignmentId!==assignmentId||!safeId(v.shiftId)||!safeId(v.departmentId)
          ||typeof v.name!=="string"||typeof v.departmentName!=="string"||typeof v.shiftDate!=="string"
          ||typeof v.startUtc!=="string"||typeof v.endUtc!=="string"||!["ASSIGNED","CANCELLED"].includes(String(v.assignmentStatus))||(v.cancellationReason!==null&&typeof v.cancellationReason!=="string"))return;
        return v as Summary;
      }}).then(r=>{
        if(abort.signal.aborted)return;
        if(r.ok)setResult(r.data);
        else {setError("Không thể phân công hoặc quyền đã thay đổi.");if(r.error.kind==="http"&&r.error.status===401)actions.invalidateSession(owner);}
      });
    return()=>abort.abort();
  },[branchId,assignmentId,owner,actions]);
  return <section aria-label="Ca được phân"><h2>Ca được phân</h2>{error?<p role="alert">{error}</p>:result?<>
    <p>{result.name} ? {result.departmentName}; Chi nhánh {branchId}</p><p>{result.shiftDate}: {result.assignmentStatus}</p>
    {result.cancellationReason&&<p>Lý do hủy: {result.cancellationReason}</p>}
    <p>{new Date(result.startUtc).toLocaleString()} ? {new Date(result.endUtc).toLocaleString()}</p>
  </>:<p>Đang tải phân công được phép truy cập...</p>}</section>;
}
