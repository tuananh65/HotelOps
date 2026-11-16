import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Technician = {userId:number;name:string};
const id=(v:unknown):v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
export function PreventiveTechnicianSelect({url,value,onChange,optional=false}:{url:string;value:string;onChange:(value:string)=>void;optional?:boolean}) {
  const {state}=useAuth();
  const owner=state.status==="AUTHENTICATED"&&!state.pending?state.authorityGeneration:null;
  const current=useRef(owner);current.current=owner;
  const mounted=useRef(owner).current;
  const abort=useRef<AbortController|null>(null),sending=useRef(false);
  const [rows,setRows]=useState<Technician[]>([]),[next,setNext]=useState<number|null>(null);
  const [busy,setBusy]=useState(false),[message,setMessage]=useState("");
  useEffect(()=>()=>abort.current?.abort(),[]);
  async function load(more=false){
    if(!owner||owner!==mounted||sending.current)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");
    try{
      const response=await fetch(url+"?afterId="+(more?next??0:0),{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!response.ok)throw Error("Technician list unavailable ("+response.status+").");
      const raw:unknown=await response.json();
      if(!isObject(raw)||!Array.isArray(raw.items)||raw.items.length>100
        ||!raw.items.every(r=>isObject(r)&&id(r.userId)&&typeof r.name==="string")
        ||!(raw.nextAfterId===null||id(raw.nextAfterId)))throw Error("Invalid technician list.");
      if(!controller.signal.aborted&&current.current===owner){
        const items=raw.items as Technician[];
        setRows(old=>more?[...old.filter(r=>!items.some(n=>n.userId===r.userId)),...items]:items);
        setNext(raw.nextAfterId);if(!items.length)setMessage(raw.nextAfterId?"No eligible technicians on this page. Load more.":"No further eligible technicians.");
      }
    }catch(error){if(!controller.signal.aborted&&current.current===owner){setRows([]);setNext(null);setMessage(String(error));}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner||owner!==mounted)return null;
  return <div><button type="button" disabled={busy} onClick={()=>void load()}>Load eligible technicians</button>
    {next!==null&&<button type="button" disabled={busy} onClick={()=>void load(true)}>More technicians</button>}
    <label>{optional?"Default technician (optional)":"Technician"}<select disabled={busy} value={value} onChange={e=>onChange(e.target.value)}>
      <option value="">{optional?"No default technician":"Select technician"}</option>
      {value&&!rows.some(r=>String(r.userId)===value)&&<option value={value} disabled>Current selection #{value} ? reload eligibility</option>}
      {rows.map(r=><option key={r.userId} value={r.userId}>{r.name} (#{r.userId})</option>)}
    </select></label>{message&&<p role="status">{message}</p>}
  </div>;
}
