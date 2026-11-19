import { referenceLabel } from "../i18n/referenceLabel";
import { userMessage } from "../i18n/userMessage";
import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";
import { safeId } from "./contracts";

type Summary = {employeeId:number;reason:string;departmentId:number;status:string;startedAt:string;endedAt:string|null};
export function UnscheduledWorkdaySummary({branchId,workdayId}:{branchId:number;workdayId:number}) {
  const { state, actions } = useAuth();
  const owner = state.authorityGeneration;
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
      }}).then(r=>{
        if(abort.signal.aborted)return;
        if(r.ok)setResult(r.data);
        else {
          setError("Ngày làm việc chưa khả dụng hoặc bạn không còn quyền.");
          if(r.error.kind === "http" && r.error.status === 401) actions.invalidateSession(owner);
        }
      });
    return()=>abort.abort();
  },[branchId,workdayId,owner,actions]);
  return <section aria-label="Thông báo ngày làm việc ngoài kế hoạch"><h2>Ngày làm việc ngoài kế hoạch {workdayId}</h2>
    {error?<p role="alert">{userMessage(error)}</p>:result?<><p>Nhân viên {referenceLabel(result, "employeeId")}; Chi nhánh {branchId}; Bộ phận {referenceLabel(result, "departmentId")}.</p>
      <p>{displayCode(result.status)}: {result.startedAt}{result.endedAt?` — ${result.endedAt}`:""}</p><p>Lý do: {result.reason}</p></>:<p>Đang tải ngày làm việc được phép truy cập...</p>}
  </section>;
}
