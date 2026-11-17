import {TimeLogFailure} from "./timeLogApi";
export interface Correction { correctionId:number; branchId:number; departmentId:number; intervalId:number; employeeId:number; kind:string; workItemId:number; status:"PENDING"|"APPLIED"|"REJECTED"; requestedAt:string; originalStart:string; originalEnd:string; proposedStart:string; proposedEnd:string; reason:string; appliedStart:string|null; appliedEnd:string|null; decidedBy:number|null; decidedAt:string|null; decisionReason:string|null; version:string; canDecide:boolean }
export interface CorrectionDetail { correction:Correction; otherIntervals:{start:string;end:string|null}[] }
const stamp=(s:unknown)=>typeof s==="string"&&/(Z|\+00:00)$/.test(s)&&Number.isFinite(Date.parse(s));
const positive=(n:unknown)=>typeof n==="number"&&Number.isSafeInteger(n)&&n>0;
function decode(v:Correction,branch:number):Correction {
  if(!v||v.branchId!==branch||![v.correctionId,v.departmentId,v.intervalId,v.employeeId,v.workItemId].every(positive)
    ||!["PENDING","APPLIED","REJECTED"].includes(v.status)||typeof v.kind!=="string"||typeof v.reason!=="string"
    ||![v.requestedAt,v.originalStart,v.originalEnd,v.proposedStart,v.proposedEnd].every(stamp)
    ||![v.appliedStart,v.appliedEnd,v.decidedAt].every(s=>s===null||stamp(s))||typeof v.canDecide!=="boolean"
    ||typeof v.version!=="string"||!/^[A-Za-z0-9+/]{11}=$/.test(v.version))throw new Error("Invalid correction response.");
  return v;
}
const route=(branch:number)=>{if(!positive(branch))throw new Error("Select a branch.");return `/api/branches/${branch}/time-corrections`;};
async function request(url:string,signal:AbortSignal,init:RequestInit={}) {
  const response=await fetch(url,{...init,signal,credentials:"same-origin",cache:"no-store",redirect:"error"});
  if(!response.ok){const p=await response.json().catch(()=>null);const e=new TimeLogFailure(response.status);e.message=p?.title??"Correction unavailable.";throw e;}return response;
}
export async function readCorrections(branch:number,department:number|null,page:number,signal:AbortSignal) {
  const data=await(await request(`${route(branch)}?page=${page}${department?`&departmentId=${department}`:""}`,signal)).json();
  if(data?.branchId!==branch||data.departmentId!==department||data.page!==page||data.pageSize!==50||!Number.isSafeInteger(data.total)||data.total<0||!Array.isArray(data.items)||data.items.length>50)throw new Error("Invalid correction list.");
  return {total:data.total as number,items:data.items.map((v:Correction)=>decode(v,branch)) as Correction[]};
}
export async function readCorrection(branch:number,id:number,signal:AbortSignal):Promise<CorrectionDetail> {
  if(!positive(id))throw new Error("Invalid correction.");
  const response=await request(`${route(branch)}/${id}`,signal);const data=await response.json();
  const correction=decode(data?.correction,branch);
  if(correction.correctionId!==id||response.headers.get("ETag")!==`"${correction.version}"`||!Array.isArray(data.otherIntervals)
    ||!data.otherIntervals.every((i:{start:string;end:string|null})=>i&&stamp(i.start)&&(i.end===null||stamp(i.end))))throw new Error("Invalid correction detail.");
  return {correction,otherIntervals:data.otherIntervals};
}
export async function saveCorrection(branch:number,id:number,version:string,body:unknown,decision:boolean,signal:AbortSignal) {
  if(!positive(id)||!/^[A-Za-z0-9+/]{11}=$/.test(version))throw new Error("Reload before saving.");
  const csrf=await(await request("/api/auth/csrf",signal)).json();
  if(csrf?.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!csrf.csrfToken.trim()||/[\r\n]/.test(csrf.csrfToken))throw new Error("Invalid CSRF response.");
  const data=await(await request(`${route(branch)}/${decision?`${id}/decision`:`intervals/${id}`}`,signal,{method:"POST",headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":`"${version}"`},body:JSON.stringify(body)})).json();
  const correction=decode(data?.correction,branch);
  if(decision?correction.correctionId!==id:correction.intervalId!==id)throw new Error("Correction outcome unconfirmed.");
  return correction;
}
