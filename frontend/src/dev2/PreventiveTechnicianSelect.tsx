import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { displayCode } from "../i18n/vi";
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
      if(!response.ok)throw Error("Danh sách kỹ thuật viên chưa khả dụng ("+response.status+").");
      const raw:unknown=await response.json();
      if(!isObject(raw)||!Array.isArray(raw.items)||raw.items.length>100
        ||!raw.items.every(r=>isObject(r)&&id(r.userId)&&typeof r.name==="string")
        ||!(raw.nextAfterId===null||id(raw.nextAfterId)))throw Error("Danh sách kỹ thuật viên không hợp lệ.");
      if(!controller.signal.aborted&&current.current===owner){
        const items=raw.items as Technician[];
        setRows(old=>more?[...old.filter(r=>!items.some(n=>n.userId===r.userId)),...items]:items);
        setNext(raw.nextAfterId);if(!items.length)setMessage(raw.nextAfterId?"Không có kỹ thuật viên đủ điều kiện trên trang này. Hãy tải thêm.":"Không còn kỹ thuật viên đủ điều kiện.");
      }
    }catch(error){if(!controller.signal.aborted&&current.current===owner){setRows([]);setNext(null);setMessage(String(error));}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner||owner!==mounted)return null;
  return <div><button type="button" disabled={busy} onClick={()=>void load()}>Tải kỹ thuật viên đủ điều kiện</button>
    {next!==null&&<button type="button" disabled={busy} onClick={()=>void load(true)}>Thêm kỹ thuật viên</button>}
    <label>{optional?"Kỹ thuật viên mặc định (không bắt buộc)":"Kỹ thuật viên"}<SearchableSelect disabled={busy} value={value} onChange={e=>onChange(e.target.value)}>
      <option value="">{optional?"Chưa có kỹ thuật viên mặc định":"Chọn kỹ thuật viên"}</option>
      {value&&!rows.some(r=>String(r.userId)===value)&&<option value={value} disabled>Lựa chọn hiện tại #{displayCode(value)} ? tải lại điều kiện</option>}
      {rows.map(r=><option key={r.userId} value={r.userId}>{r.name} (#{displayCode(r.userId)})</option>)}
    </SearchableSelect></label>{message&&<p role="status">{userMessage(message)}</p>}
  </div>;
}
