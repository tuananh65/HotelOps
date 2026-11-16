import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Definition={id:number;code:string;name:string;start:string;end:string;crossesMidnight:boolean;breakMinutes:number;isActive:boolean;version:string};
const positive=(v:unknown):v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
function valid(v:unknown):v is Definition {
 return isObject(v)&&positive(v.id)&&typeof v.code==="string"&&typeof v.name==="string"&&typeof v.start==="string"&&typeof v.end==="string"
   &&typeof v.crossesMidnight==="boolean"&&typeof v.breakMinutes==="number"&&Number.isInteger(v.breakMinutes)&&typeof v.isActive==="boolean"&&typeof v.version==="string"&&/^"[A-F0-9]{64}"$/.test(v.version);
}
export function ShiftDefinitionsPanel() {
  const {state}=useAuth(),owner=state.status==="AUTHENTICATED"&&!state.pending?state.authorityGeneration:null;
  const current=useRef(owner);current.current=owner;
  const abort=useRef<AbortController|null>(null),sending=useRef(false);
  const [rows,setRows]=useState<Definition[]>([]),[busy,setBusy]=useState(false),[loaded,setLoaded]=useState(false),[message,setMessage]=useState("");
  const [selected,setSelected]=useState<Definition|null>(null),[label,setLabel]=useState(""),[response,setResponse]=useState(""),[resolution,setResolution]=useState("");
  const [branch,setBranch]=useState(""),[department,setDepartment]=useState(""),[code,setCode]=useState(""),[overnight,setOvernight]=useState(false),[editing,setEditing]=useState(false);
  const endpoint="/api/branches/"+branch+"/departments/"+department+"/shift-definitions";
  const [uncertain,setUncertain]=useState(false);
  useEffect(()=>{setRows([]);setLoaded(false);setSelected(null);setMessage("");return()=>abort.current?.abort();},[owner]);
  async function load(after=0) {
    if(!owner||sending.current)return;
    if(!positive(Number(branch))||!positive(Number(department))){setMessage("Select a valid branch and department.");return;}
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");
    try {
      const result=await fetch(endpoint+"?afterId="+after,{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!result.ok)throw Error("Shift definitions unavailable ("+result.status+"). Current department mapping and SHIFT_MANAGE are required.");
      const data: unknown=await result.json();if(!Array.isArray(data)||!data.every(valid))throw Error("Configuration response could not be confirmed.");
      if(!controller.signal.aborted&&current.current===owner){setRows(data);setLoaded(true);setSelected(null);setEditing(false);setUncertain(false);}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setMessage(String(e));}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  async function save() {
    if(!owner||!loaded||!editing||sending.current||uncertain)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");let submitted=false;
    try {
      const first=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!first.ok)throw Error("Session validation failed.");
      const csrf: unknown=await first.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Session changed. Reload.");
      submitted=true;
      const result=await fetch(endpoint+(selected?"/"+selected.id:""),{method:selected?"PUT":"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,...(selected?{"If-Match":selected.version}:{})},
        body:JSON.stringify({code,name:label,start:response.length===5?response+":00":response,end:resolution.length===5?resolution+":00":resolution,crossesMidnight:overnight})});
      if(!result.ok){const problem: unknown=await result.json();throw Error(isObject(problem)&&typeof problem.title==="string"?problem.title:"Save was not confirmed.");}
      const data: unknown=await result.json();if(!valid(data)||(selected!==null&&data.id!==selected.id))throw Error("Save response could not be confirmed.");
      if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setEditing(false);setMessage("Saved. Existing scheduled shift times are preserved. Reload definitions before continuing.");}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setMessage(String(e)+" Reload before retrying.");if(submitted)setUncertain(true);}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner)return null;
  function resetScope(){setRows([]);setLoaded(false);setSelected(null);setEditing(false);setMessage("");}
  function edit(row:Definition|null){setSelected(row);setCode(row?.code??"");setLabel(row?.name??"");setResponse(row?.start??"");setResolution(row?.end??"");setOvernight(row?.crossesMidnight??false);setEditing(true);}
  return <section><h2>Shift definitions</h2><p>Times apply when planning new shifts. Existing roster times stay unchanged.</p>
    <fieldset disabled={busy}><label>Branch ID<input inputMode="numeric" value={branch} onChange={e=>{resetScope();setBranch(e.target.value);}}/></label>
    <label>Department ID<input inputMode="numeric" value={department} onChange={e=>{resetScope();setDepartment(e.target.value);}}/></label>
    <button onClick={()=>void load()}>Load definitions</button></fieldset>{message&&<p role="status">{message}</p>}
    {loaded&&<><button disabled={busy||uncertain} onClick={()=>edit(null)}>Create definition</button>
    <table><thead><tr><th>ID / code</th><th>Name</th><th>Start</th><th>End</th><th>Crosses midnight</th><th>Status</th><th>Action</th></tr></thead><tbody>
      {rows.map(r=><tr key={r.id}><td>{r.id} / {r.code}</td><td>{r.name}</td><td>{r.start}</td><td>{r.end}</td><td>{r.crossesMidnight?"Yes":"No"}</td><td>{r.isActive?"Active":"Inactive"}</td>
        <td><button disabled={busy||uncertain} onClick={()=>edit(r)}>Edit</button></td></tr>)}
    </tbody></table>{rows.length===100&&<button disabled={busy} onClick={()=>void load(rows[rows.length-1].id)}>Next page</button>}</>}
    {editing&&<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy||uncertain}><legend>{selected?"Edit definition":"New definition"}</legend>
      <label>Code<input required maxLength={30} value={code} onChange={e=>setCode(e.target.value)} /></label>
      <label>Name<input required maxLength={100} value={label} onChange={e=>setLabel(e.target.value)} /></label>
      <label>Start<input required type="time" step="1" value={response} onChange={e=>setResponse(e.target.value)} /></label>
      <label>End<input required type="time" step="1" value={resolution} onChange={e=>setResolution(e.target.value)} /></label>
      <label><input type="checkbox" checked={overnight} onChange={e=>setOvernight(e.target.checked)}/>Ends on the following day</label>
      <button>Save definition</button><button type="button" onClick={()=>setEditing(false)}>Cancel</button>
    </fieldset></form>}
  </section>;
}
