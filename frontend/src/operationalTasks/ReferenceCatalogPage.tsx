import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Priority = { id:number; code:string; label:string; description:string|null; order:number; isActive:boolean; meaningLocked:boolean; version:string };
const positive=(v: unknown): v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
function valid(v: unknown): v is Priority {
 return isObject(v)&&positive(v.id)&&typeof v.code==="string"&&typeof v.label==="string"&&(v.description===null||typeof v.description==="string")&&typeof v.order==="number"&&Number.isInteger(v.order)&&typeof v.isActive==="boolean"&&typeof v.meaningLocked==="boolean"&&typeof v.version==="string"&&/^"[A-F0-9]{64}"$/.test(v.version);
}
export function ReferenceCatalogPage() {
  const {state}=useAuth(),owner=state.status==="AUTHENTICATED"&&!state.pending?state.authorityGeneration:null;
  const current=useRef(owner);current.current=owner;
  const abort=useRef<AbortController|null>(null),sending=useRef(false);
  const [rows,setRows]=useState<Priority[]>([]),[busy,setBusy]=useState(false),[loaded,setLoaded]=useState(false),[message,setMessage]=useState("");
  const [selected,setSelected]=useState<Priority|null>(null),[label,setLabel]=useState("");
  const [group,setGroup]=useState("INCIDENT_CATEGORY"),[code,setCode]=useState(""),[description,setDescription]=useState(""),[order,setOrder]=useState("0"),[active,setActive]=useState(true),[confirmation,setConfirmation]=useState(false),[editing,setEditing]=useState(false),[after,setAfter]=useState(0);
  const [uncertain,setUncertain]=useState(false);
  useEffect(()=>{setRows([]);setLoaded(false);setSelected(null);setMessage("");return()=>abort.current?.abort();},[owner]);
  async function load(cursor=0) {
    if(!owner||sending.current)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");
    try {
      const result=await fetch("/api/reference-data/catalog/"+group+"?afterId="+cursor,{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!result.ok)throw Error("Reference catalog unavailable ("+result.status+"). A global administrator MASTER_DATA_MANAGE grant is required.");
      const data: unknown=await result.json();if(!Array.isArray(data)||!data.every(valid))throw Error("Configuration response could not be confirmed.");
      if(!controller.signal.aborted&&current.current===owner){setRows(data);setLoaded(true);setSelected(null);setEditing(false);setAfter(cursor);setUncertain(false);}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setMessage(String(e));}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  async function save() {
    if(!owner||!editing||!loaded||sending.current||uncertain)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");let submitted=false;
    try {
      const first=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!first.ok)throw Error("Session validation failed.");
      const csrf: unknown=await first.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Session changed. Reload.");
      submitted=true;
      const result=await fetch("/api/reference-data/catalog/"+group+(selected?"/"+selected.id:""),{method:selected?"PUT":"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,...(selected?{"If-Match":selected.version}:{})},
        body:JSON.stringify({code,label,description:description||null,order:Number(order),isActive:active,confirmation})});
      if(!result.ok){const problem: unknown=await result.json();throw Error(isObject(problem)&&typeof problem.title==="string"?problem.title:"Save was not confirmed.");}
      const data: unknown=await result.json();if(!valid(data)||(selected!==null&&data.id!==selected.id))throw Error("Save response could not be confirmed.");
      if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setEditing(false);setMessage("Saved. Reload the catalog to continue.");}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setMessage(String(e)+" Reload before retrying.");if(submitted)setUncertain(true);}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner)return null;
  function edit(r:Priority|null) {setSelected(r);setCode(r?.code??"");setLabel(r?.label??"");setDescription(r?.description??"");setOrder(String(r?.order??0));setActive(r?.isActive??true);setConfirmation(false);setEditing(true);}
  return <section><h2>Operational reference data</h2>
    <label>Group<select disabled={busy} value={group} onChange={e=>{setGroup(e.target.value);setRows([]);setLoaded(false);setEditing(false);setSelected(null);setMessage("");setAfter(0);}}>
      <option value="INCIDENT_CATEGORY">Incident categories</option><option value="CLEANING_TYPE">Cleaning types</option><option value="BLOCKER">Blocker reasons</option><option value="TASK_PAUSE_REASON">Task pause reasons</option>
    </select></label>
    <button disabled={busy} onClick={()=>void load()}>Load catalog</button>{message&&<p role="status">{message}</p>}
    {loaded&&<><button disabled={busy||uncertain||group==="TASK_PAUSE_REASON"} onClick={()=>edit(null)}>Create value</button>
      <table><thead><tr><th>Code</th><th>Label</th><th>Order</th><th>Status</th><th>Action</th></tr></thead><tbody>{rows.map(r=><tr key={r.id}>
        <td>{r.code}</td><td>{r.label}</td><td>{r.order}</td><td>{r.isActive?"Active":"Inactive"}</td><td><button disabled={busy||uncertain} onClick={()=>edit(r)}>Edit</button></td>
      </tr>)}</tbody></table>
      {after>0&&<button disabled={busy} onClick={()=>void load()}>First page</button>}
      {rows.length===100&&<button disabled={busy} onClick={()=>void load(rows[rows.length-1].id)}>Next page</button>}
    </>}
    {editing&&<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy||uncertain}>
      <legend>{selected?"Edit value":"Create value"}</legend>{selected?.meaningLocked&&<p>Historical or reserved meaning is preserved. Only order and permitted activity changes can be saved.</p>}
      <label>Code<input required maxLength={60} readOnly={selected?.meaningLocked} value={code} onChange={e=>setCode(e.target.value)} /></label>
      <label>Label<input required maxLength={150} readOnly={selected?.meaningLocked} value={label} onChange={e=>setLabel(e.target.value)} /></label>
      <label>Description<textarea maxLength={4000} readOnly={selected?.meaningLocked} value={description} onChange={e=>setDescription(e.target.value)} /></label>
      <label>Display order<input required type="number" min="-2147483648" max="2147483647" step="1" value={order} onChange={e=>setOrder(e.target.value)} /></label>
      <label><input type="checkbox" checked={active} disabled={!selected||group==="TASK_PAUSE_REASON"&&selected.code==="END_OF_SHIFT"} onChange={e=>{setActive(e.target.checked);setConfirmation(false);}} />Active</label>
      {!active&&<label><input required type="checkbox" checked={confirmation} onChange={e=>setConfirmation(e.target.checked)} />Confirm deactivation; historical references are retained.</label>}
      <button>Save</button><button type="button" onClick={()=>setEditing(false)}>Cancel</button>
    </fieldset></form>}
  </section>;
}
