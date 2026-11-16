import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
type Membership={departmentId:number;name:string;grantedAt:string;revokedAt:string|null;provenance:string};
type Period=Membership&{historyId:number;recordedBy:number;restoredBy:number;restoredAt:string};
type Page={userId:number;branchId:number;version:string;memberships:Membership[];history:Period[];departments:{id:number;name:string}[]};
const positive=(v:unknown):v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
function member(v:unknown):v is Membership{return isObject(v)&&positive(v.departmentId)&&typeof v.name==="string"&&typeof v.grantedAt==="string"&&(v.revokedAt===null||typeof v.revokedAt==="string")&&typeof v.provenance==="string";}
export function EmployeeMembershipPage(){
  const {id}=useParams(),target=Number(id),{state,actions}=useAuth(),owner=state.authorityGeneration;
  const [branch,setBranch]=useState(""),[department,setDepartment]=useState(""),[reason,setReason]=useState("");
  const [branches,setBranches]=useState<{id:number;name:string;isPrimary:boolean}[]>([]),[branchesLoading,setBranchesLoading]=useState(false),[branchAttempt,setBranchAttempt]=useState(0);
  const [page,setPage]=useState<Page|null>(null),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
  const [removing,setRemoving]=useState<Membership|null>(null),[confirmed,setConfirmed]=useState(false);
  const abort=useRef<AbortController|null>(null),sending=useRef(false),current=useRef(owner);current.current=owner;
  useEffect(()=>{setPage(null);setRemoving(null);setConfirmed(false);setMessage("");return()=>abort.current?.abort();},[owner,target]);
  useEffect(()=>{
    const controller=new AbortController();setBranches([]);setBranch("");setPage(null);setBranchesLoading(true);
    if(!positive(target)){setBranchesLoading(false);return()=>controller.abort();}
    void createHttpClient()({method:"GET",url:`/api/users/${target}/department-membership-branches`,expectedStatus:200,signal:controller.signal,
      decode(v){if(!Array.isArray(v)||!v.every(b=>isObject(b)&&positive(b.id)&&typeof b.name==="string"&&typeof b.isPrimary==="boolean"))return;
        return v as {id:number;name:string;isPrimary:boolean}[];}}).then(result=>{
      if(controller.signal.aborted||current.current!==owner)return;
      setBranchesLoading(false);
      if(result.ok){setBranches(result.data);if(result.data.length===1)setBranch(String(result.data[0].id));}
      else{setMessage("Current branch memberships could not be loaded. Reload to refresh your authorization.");if(result.error.kind==="http"&&result.error.status===401)actions.invalidateSession(owner);}
    });return()=>controller.abort();
  },[owner,target,branchAttempt]);
  async function load(){
    if(sending.current||!positive(target)||!branches.some(b=>b.id===Number(branch)))return;
    const b=Number(branch),controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setPage(null);setRemoving(null);setConfirmed(false);setMessage("");setDepartment("");
    try{
      const result=await createHttpClient()({method:"GET",url:"/api/users/"+target+"/branches/"+b+"/departments",expectedStatus:200,signal:controller.signal,
        decode(v){if(!isObject(v)||v.userId!==target||v.branchId!==b||typeof v.version!=="string"||!/^"[A-Za-z0-9+/]{11}="$/.test(v.version)
          ||!Array.isArray(v.memberships)||!v.memberships.every(member)||!Array.isArray(v.history)||!v.history.every(h=>isObject(h)&&positive(h.historyId)&&positive(h.recordedBy)&&positive(h.restoredBy)&&typeof h.restoredAt==="string"&&typeof h.revokedAt==="string"&&member(h))
          ||!Array.isArray(v.departments)||!v.departments.every(d=>isObject(d)&&positive(d.id)&&typeof d.name==="string"))return;return v as Page;}});
      if(controller.signal.aborted||current.current!==owner)return;
      if(result.ok)setPage(result.data);else{setMessage("Memberships unavailable. Administrator USER_MANAGE and existing target branch access are required.");if(result.error.kind==="http"&&result.error.status===401)actions.invalidateSession(owner);}
    }finally{sending.current=false;if(current.current===owner)setBusy(false);}
  }
  async function save(remove=false){
    const restore=remove&&removing?.revokedAt!==null;
    const chosen=remove?removing?.departmentId:Number(department);
    if(sending.current||!page||!positive(chosen)||remove&&!confirmed)return;
    const snapshot=page,controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");let dispatched=false;
    try{
      const first=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!first.ok)throw Error("Session validation failed.");const csrf:unknown=await first.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Session changed.");
      dispatched=true;
      const response=await fetch("/api/users/"+snapshot.userId+"/branches/"+snapshot.branchId+"/departments"+(restore?"/restore":remove?"/remove":""),{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":snapshot.version},body:JSON.stringify({departmentId:chosen,reason})});
      if(response.status!==204){const problem:unknown=await response.json();
        if(isObject(problem)&&problem.title==="MEMBERSHIP_DEPENDENCIES"&&Array.isArray(problem.dependencies)){
          const detail=problem.dependencies.filter(d=>isObject(d)&&typeof d.kind==="string"&&typeof d.count==="number")
            .map(d=>`${String(d.kind).toLowerCase().replaceAll("_"," ")}: ${d.count}`).join("; ");
          throw Error("Membership cannot be removed: "+detail);
        }
        throw Error(isObject(problem)&&typeof problem.title==="string"?problem.title:"Save was not confirmed.");}
      if(!controller.signal.aborted&&current.current===owner)setMessage(`Membership ${restore?"restored; previous period preserved":remove?"removed; history preserved":"added"}. The employee must sign in again. Reload memberships to continue.`);
    }catch(e){if(!controller.signal.aborted&&current.current===owner)setMessage(String(e)+" Reload before retrying.");}
    finally{sending.current=false;if(current.current===owner){setBusy(false);if(dispatched)setPage(null);}}
  }
  if(!positive(target))return <p role="alert">Invalid user.</p>;
  return <main><h1>Employee department memberships</h1><Link to={"/users/"+target}>User {target}</Link>
    <p>Manage working departments inside a branch already granted to this employee. Removal preserves history and requires resolving dependent work. Role grants and primary department are managed separately.</p>
    <form onSubmit={e=>{e.preventDefault();void load();}}><fieldset disabled={busy||branchesLoading}><label>Current branch<select required value={branch} onChange={e=>{setBranch(e.target.value);setPage(null);setMessage("");}}>
      <option value="">Select branch</option>{branches.map(b=><option key={b.id} value={b.id}>{b.name}{b.isPrimary?" (primary)":""}</option>)}</select></label><button disabled={!branch}>Load memberships</button>
      <button type="button" onClick={()=>setBranchAttempt(n=>n+1)}>Reload branches</button></fieldset></form>
    {branchesLoading&&<p role="status">Loading current branches...</p>}
    {message&&<p role="status">{message}</p>}{page&&<><table><thead><tr><th>Department</th><th>Granted</th><th>Revoked</th><th>Provenance</th><th>Action</th></tr></thead><tbody>{page.memberships.map(m=><tr key={m.departmentId}><td>{m.name}</td><td>{m.grantedAt}</td><td>{m.revokedAt??"Active"}</td><td>{m.provenance}</td><td><button disabled={busy||m.revokedAt!==null&&!page.departments.some(d=>d.id===m.departmentId)} onClick={()=>{setRemoving(m);setConfirmed(false);setReason("");}}>{m.revokedAt===null?"Remove":"Restore"} membership</button></td></tr>)}</tbody></table>
      {page.history.length>0&&<details><summary>Previous membership periods</summary><table><thead><tr><th>Department</th><th>Granted</th><th>Revoked</th><th>Provenance</th><th>Recorded by</th><th>Restored</th><th>Restored by</th></tr></thead><tbody>{page.history.map(h=><tr key={h.historyId}><td>{h.name}</td><td>{h.grantedAt}</td><td>{h.revokedAt}</td><td>{h.provenance}</td><td>{h.recordedBy}</td><td>{h.restoredAt}</td><td>{h.restoredBy}</td></tr>)}</tbody></table></details>}
      {removing?<form onSubmit={e=>{e.preventDefault();void save(true);}}><fieldset disabled={busy}><legend>{removing.revokedAt===null?"Remove":"Restore"} {removing.name}</legend>
        <label>Reason<input required minLength={10} maxLength={250} value={reason} onChange={e=>setReason(e.target.value)}/></label>
        <label><input required type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)}/>Confirm {removing.revokedAt===null?"removal":"restoration"} of this membership and revocation of employee sessions.</label>
        <button disabled={!confirmed}>Confirm {removing.revokedAt===null?"removal":"restoration"}</button><button type="button" onClick={()=>{setRemoving(null);setConfirmed(false);}}>Cancel</button>
      </fieldset></form>:<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy}><legend>Add department membership</legend>
        <label>Department<select required value={department} onChange={e=>setDepartment(e.target.value)}><option value="">Select department</option>{page.departments.filter(d=>!page.memberships.some(m=>m.departmentId===d.id)).map(d=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
        <label>Reason<input required minLength={10} maxLength={250} value={reason} onChange={e=>setReason(e.target.value)}/></label><button>Add membership</button>
      </fieldset></form>}</>}
  </main>;
}
