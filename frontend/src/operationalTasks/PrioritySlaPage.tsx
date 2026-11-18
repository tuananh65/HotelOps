import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Priority = { id: number; code: string; label: string; responseMinutes: number | null; resolutionMinutes: number | null; configured: boolean; order: number; version: string };
const positive=(v: unknown): v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
function valid(v: unknown): v is Priority {
  return isObject(v)&&positive(v.id)&&typeof v.code==="string"&&["LOW","NORMAL","HIGH","CRITICAL"].includes(v.code)
    &&typeof v.label==="string"&&(v.responseMinutes===null||positive(v.responseMinutes))&&(v.resolutionMinutes===null||positive(v.resolutionMinutes))
    &&typeof v.order==="number"&&Number.isSafeInteger(v.order)&&typeof v.configured==="boolean"&&typeof v.version==="string"&&/^"[A-F0-9]{64}"$/.test(v.version);
}
export function PrioritySlaPage() {
  const {state}=useAuth();
  const generation=useRef({authority:state.authorityGeneration,key:0});
  if(generation.current.authority!==state.authorityGeneration)generation.current={authority:state.authorityGeneration,key:generation.current.key+1};
  if(state.status!=="AUTHENTICATED"||state.pending||state.session?.mustChangePassword)return null;
  return <BoundPrioritySlaPage key={generation.current.key} />;
}
function BoundPrioritySlaPage() {
  const {state}=useAuth(),owner=state.status==="AUTHENTICATED"&&!state.pending?state.authorityGeneration:null;
  const current=useRef(owner);current.current=owner;
  const abort=useRef<AbortController|null>(null),sending=useRef(false);
  const [rows,setRows]=useState<Priority[]>([]),[busy,setBusy]=useState(false),[loaded,setLoaded]=useState(false),[message,setMessage]=useState("");
  const [selected,setSelected]=useState<Priority|null>(null),[label,setLabel]=useState(""),[response,setResponse]=useState(""),[resolution,setResolution]=useState("");
  const [uncertain,setUncertain]=useState(false);
  useEffect(()=>{setRows([]);setLoaded(false);setSelected(null);setMessage("");return()=>abort.current?.abort();},[owner]);
  async function load() {
    if(!owner||sending.current)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");
    try {
      const result=await fetch("/api/reference-data/priority-sla",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!result.ok)throw Error("Cấu hình ưu tiên chưa khả dụng ("+result.status+"). Cần quyền MASTER_DATA_MANAGE của quản trị viên toàn hệ thống.");
      const data: unknown=await result.json();if(!Array.isArray(data)||!data.every(valid))throw Error("Không xác minh được phản hồi cấu hình.");
      if(!controller.signal.aborted&&current.current===owner){setRows(data);setLoaded(true);setSelected(null);setUncertain(false);}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setMessage(String(e));}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  async function save() {
    if(!owner||!selected||sending.current||uncertain)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");let submitted=false;
    try {
      const first=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!first.ok)throw Error("Xác minh phiên đăng nhập thất bại.");
      const csrf: unknown=await first.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      submitted=true;
      const result=await fetch("/api/reference-data/priority-sla/"+selected.id,{method:"PUT",credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,"If-Match":selected.version},
        body:JSON.stringify({label,responseMinutes:Number(response),resolutionMinutes:Number(resolution)})});
      if(!result.ok){
        const problem: unknown=await result.json();
        const code=isObject(problem)&&typeof problem.code==="string"?problem.code:null;
        if(result.status===400&&code!==null&&["INVALID_PRIORITY_SLA"].includes(code)){
          if(!controller.signal.aborted&&current.current===owner){
            setMessage(code==="MSG35"?"MSG35: Mã đã tồn tại. Sửa và lưu lại.":"Dữ liệu nhập bị từ chối. Sửa trường bắt buộc và lưu lại.");
          }
          return;
        }
        throw Error(isObject(problem)&&typeof problem.title==="string"?problem.title:"Chưa xác nhận lưu.");
      }
      const data: unknown=await result.json();if(!valid(data)||data.id!==selected.id)throw Error("Không xác minh được phản hồi lưu.");
      if(!controller.signal.aborted&&current.current===owner){setRows(old=>old.map(r=>r.id===data.id?data:r));setSelected(null);setMessage("Đã lưu. Tiếp nhận sau này dùng thời hạn này; hạn sự cố hiện có giữ nguyên.");}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setMessage(String(e)+"Tải lại trước khi thử lại.");if(submitted)setUncertain(true);}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner)return null;
  return <section><h2>Nhãn ưu tiên và thời hạn SLA</h2><p>Thay đổi áp dụng cho lần tiếp nhận sau. Thời hạn sự cố và chu kỳ xử lý trước giữ nguyên.</p>
    <button disabled={busy} onClick={()=>void load()}>Tải độ ưu tiên</button>{message&&<p role="status">{message}</p>}
    {loaded&&<table><thead><tr><th>Mã</th><th>Nhãn</th><th>Thứ tự</th><th>Số phút phản hồi</th><th>Số phút xử lý</th><th>Cấu hình</th><th>Thao tác</th></tr></thead>
      <tbody>{rows.map(r=><tr key={r.id}><td>{r.code}</td><td>{r.label}</td><td>{r.order}</td><td>{r.responseMinutes??"Chưa cấu hình"}</td><td>{r.resolutionMinutes??"Chưa cấu hình"}</td><td>{r.configured?"Đang hoạt động":"Chưa khả dụng"}</td>
        <td><button disabled={busy||uncertain} onClick={()=>{setSelected(r);setLabel(r.label);setResponse(r.responseMinutes?.toString()??"");setResolution(r.resolutionMinutes?.toString()??"");}}>Chỉnh sửa</button></td></tr>)}</tbody></table>}
    {selected&&<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy||uncertain}><legend>{selected.code}</legend>
      <label>Nhãn<input required maxLength={150} value={label} onChange={e=>setLabel(e.target.value)} /></label>
      <label>Thời hạn phản hồi mục tiêu (phút)<input required type="number" min="1" max="2147483647" step="1" value={response} onChange={e=>setResponse(e.target.value)} /></label>
      <label>Thời hạn xử lý mục tiêu (phút)<input required type="number" min="1" max="2147483647" step="1" value={resolution} onChange={e=>setResolution(e.target.value)} /></label>
      <button>Lưu</button><button type="button" onClick={()=>setSelected(null)}>Hủy</button>
    </fieldset></form>}
  </section>;
}
