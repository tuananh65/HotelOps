import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { planningRequest } from "../operationalTasks/planning";
import { TaskProgressError } from "../operationalTasks/progress";

type Branch={id:number;name:string;isPrimary:boolean};
export function EmployeeBranchRemovalPanel({user,branch,version,branches,onChanged}:{user:number;branch:number;version:string;branches:Branch[];onChanged:()=>void}) {
  const {state,actions}=useAuth();const owner=state.authorityGeneration;
  const [replacement,setReplacement]=useState(""),[department,setDepartment]=useState(""),[reason,setReason]=useState("");
  const [options,setOptions]=useState<{id:number;name:string}[]>([]),[confirmed,setConfirmed]=useState(false),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
  const active=useRef<AbortController|null>(null),saving=useRef(false),generation=useRef(owner);generation.current=owner;
  const primary=branches.find(b=>b.id===branch)?.isPrimary===true;
  useEffect(()=>()=>active.current?.abort(),[owner,user,branch,version]);
  useEffect(()=>{
    const abort=new AbortController();setOptions([]);setDepartment("");
    if(!replacement)return()=>abort.abort();
    void planningRequest(`/api/users/${user}/branches/${replacement}/departments`,abort.signal).then(r=>r.json()).then((body:unknown)=>{
      if(!isObject(body)||body.userId!==user||body.branchId!==Number(replacement)||!Array.isArray(body.replacementDepartments))throw Error("Invalid replacement membership");
      const options=body.replacementDepartments.flatMap(d=>isObject(d)&&typeof d.id==="number"&&Number.isSafeInteger(d.id)&&d.id>0&&typeof d.name==="string"?[{id:d.id,name:d.name}]:[]);
      if(!abort.signal.aborted)setOptions(options);
    }).catch(e=>{if(!abort.signal.aborted){setMessage("Replacement membership unavailable. Reload before retrying.");if(e instanceof TaskProgressError&&e.status===401)actions.invalidateSession(owner);}});
    return()=>abort.abort();
  },[owner,user,replacement,actions]);
  async function remove(){
    if(saving.current||!confirmed||!reason.trim()||primary&&(!replacement||!options.some(d=>d.id===Number(department))))return;
    saving.current=true;setBusy(true);setMessage("");const abort=new AbortController();active.current=abort;
    try{
      const csrf:unknown=await (await planningRequest("/api/auth/csrf",abort.signal)).json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||generation.current!==owner)
        throw Error("Session changed; reload before saving.");
      const response=await fetch(`/api/users/${user}/branches/${branch}/remove`,{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":version},
        body:JSON.stringify({reason,...(primary?{replacementBranchId:Number(replacement),replacementDepartmentId:Number(department)}:{})})});
      if(!response.ok){
        if(response.status===401)throw new TaskProgressError(401);
        const problem:unknown=await response.json();
        const details=isObject(problem)&&Array.isArray(problem.dependencies)?problem.dependencies.flatMap(d=>isObject(d)&&typeof d.kind==="string"&&typeof d.count==="number"?[`${d.kind}: ${d.count}`]:[]).join("; "):"";
        throw Error((isObject(problem)&&typeof problem.title==="string"?problem.title:"Removal rejected")+(details?": "+details:""));
      }
      if(response.status!==204)throw Error("Branch removal receipt invalid");
      if(!abort.signal.aborted&&generation.current===owner){setMessage("MSG08: Branch access and memberships removed; history retained and employee sessions revoked.");onChanged();}
    }catch(e){
      if(!abort.signal.aborted&&generation.current===owner){
        if(e instanceof TaskProgressError&&e.status===401)actions.invalidateSession(owner);
        setMessage(`${e instanceof Error?e.message:"Outcome not confirmed"}. Reload current memberships before retrying.`);
        // A stale version or ambiguous response must not be silently replayed.
        setConfirmed(false);
      }
    }finally{saving.current=false;if(!abort.signal.aborted&&generation.current===owner)setBusy(false);}
  }
  return <details><summary>Remove this branch access</summary><form onSubmit={e=>{e.preventDefault();void remove();}}><fieldset disabled={busy}>
    <p>All department memberships in this branch will end. Existing roles, grants, Head mappings and open responsibilities must be resolved first; none will be transferred automatically.</p>
    {primary&&<><label>Replacement primary branch<select required value={replacement} onChange={e=>setReplacement(e.target.value)}><option value="">Select active branch</option>{branches.filter(b=>b.id!==branch).map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
      <label>Replacement primary department<select required value={department} onChange={e=>setDepartment(e.target.value)}><option value="">Select active membership</option>{options.map(d=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label></>}
    <label>Reason<input required minLength={10} maxLength={250} value={reason} onChange={e=>setReason(e.target.value)}/></label>
    <label><input required type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)}/>Confirm branch removal and employee session revocation.</label>
    <button disabled={!confirmed}>Remove branch access</button>
  </fieldset></form>{message&&<p role="status">{message}</p>}</details>;
}
