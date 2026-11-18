import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { PrioritySlaPage } from "./PrioritySlaPage";

type CatalogValue = { id:number; code:string; label:string; description:string|null; order:number; materialNeed?:boolean|null; isActive:boolean; meaningLocked:boolean; version:string };
const positive=(v: unknown): v is number=>typeof v==="number"&&Number.isSafeInteger(v)&&v>0;
function valid(v: unknown): v is CatalogValue {
 return isObject(v)&&positive(v.id)&&typeof v.code==="string"&&typeof v.label==="string"&&(v.description===null||typeof v.description==="string")&&(typeof v.order==="number"&&Number.isSafeInteger(v.order))&&(v.materialNeed===undefined||v.materialNeed===null||typeof v.materialNeed==="boolean")&&typeof v.isActive==="boolean"&&typeof v.meaningLocked==="boolean"&&typeof v.version==="string"&&/^"[A-F0-9]{64}"$/.test(v.version);
}
export function ReferenceCatalogPage() {
  const {state}=useAuth();
  const generation=useRef({authority:state.authorityGeneration,key:0});
  if(generation.current.authority!==state.authorityGeneration)generation.current={authority:state.authorityGeneration,key:generation.current.key+1};
  if(state.status!=="AUTHENTICATED"||state.pending||state.session?.mustChangePassword)return null;
  return <BoundReferenceCatalogPage key={generation.current.key} />;
}
function BoundReferenceCatalogPage() {
  const {state}=useAuth(),owner=state.status==="AUTHENTICATED"&&!state.pending?state.authorityGeneration:null;
  const current=useRef(owner);current.current=owner;
  const abort=useRef<AbortController|null>(null),sending=useRef(false);
  const [rows,setRows]=useState<CatalogValue[]>([]),[busy,setBusy]=useState(false),[loaded,setLoaded]=useState(false),[message,setMessage]=useState("");
  const [selected,setSelected]=useState<CatalogValue|null>(null),[label,setLabel]=useState("");
  const [group,setGroup]=useState("INCIDENT_CATEGORY"),[code,setCode]=useState(""),[description,setDescription]=useState(""),[order,setOrder]=useState("0"),[active,setActive]=useState(true),[confirmation,setConfirmation]=useState(false),[editing,setEditing]=useState(false),[after,setAfter]=useState(0);
  const [material,setMaterial]=useState("");
  const endpoint=group==="REQUEST_TYPE"?"/api/reference-data/request-types":"/api/reference-data/catalog/"+group;
  const [uncertain,setUncertain]=useState(false);
  useEffect(()=>{setRows([]);setLoaded(false);setSelected(null);setMessage("");return()=>abort.current?.abort();},[owner]);
  async function load(cursor=0) {
    if(!owner||sending.current)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");
    try {
      const result=await fetch(endpoint+"?afterId="+cursor,{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!result.ok)throw Error("Danh mục tham chiếu chưa khả dụng ("+result.status+"). Cần quyền MASTER_DATA_MANAGE của quản trị viên toàn hệ thống.");
      const data: unknown=await result.json();if(!Array.isArray(data)||!data.every(valid))throw Error("Không xác minh được phản hồi cấu hình.");
      if(!controller.signal.aborted&&current.current===owner){setRows(data);setLoaded(true);setSelected(null);setEditing(false);setAfter(cursor);setUncertain(false);}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setMessage(String(e));}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  async function save() {
    if(!owner||!editing||!loaded||sending.current||uncertain)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");let submitted=false;
    try {
      const first=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!first.ok)throw Error("Xác minh phiên đăng nhập thất bại.");
      const csrf: unknown=await first.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      submitted=true;
      const result=await fetch(endpoint+(selected?"/"+selected.id:""),{method:selected?"PUT":"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,...(selected?{"If-Match":selected.version}:{})},
        body:JSON.stringify({code,label,description:description||null,order:Number(order),materialNeed:material===""?null:material==="true",isActive:active,confirmation})});
      if(!result.ok){
        const problem: unknown=await result.json();
        const code=isObject(problem)&&typeof problem.code==="string"?problem.code:null;
        if(result.status===400&&code!==null&&["INVALID_REFERENCE_INPUT","INVALID_REQUEST_TYPE","DEACTIVATION_CONFIRMATION_REQUIRED","ACTIVE_CLASSIFIED_TYPE_REQUIRED","REQUEST_TYPE_CLASSIFICATION_REQUIRED"].includes(code)||result.status===409&&code==="MSG35"){
          if(!controller.signal.aborted&&current.current===owner){
            setMessage(code==="MSG35"?"MSG35: Mã đã tồn tại. Sửa và lưu lại.":"Dữ liệu nhập bị từ chối. Sửa trường bắt buộc và lưu lại.");
          }
          return;
        }
        throw Error(isObject(problem)&&typeof problem.title==="string"?problem.title:"Chưa xác nhận lưu.");
      }
      const data: unknown=await result.json();if(!valid(data)||(selected!==null&&data.id!==selected.id))throw Error("Không xác minh được phản hồi lưu.");
      if(!controller.signal.aborted&&current.current===owner){setRows([]);setLoaded(false);setSelected(null);setEditing(false);setMessage("Đã lưu. Tải lại danh mục để tiếp tục.");}
    }catch(e){if(!controller.signal.aborted&&current.current===owner){setMessage(String(e)+"Tải lại trước khi thử lại.");if(submitted)setUncertain(true);}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner)return null;
  function edit(r:CatalogValue|null) {setSelected(r);setCode(r?.code??"");setLabel(r?.label??"");setDescription(r?.description??"");setOrder(String(r?.order??0));setActive(r?.isActive??true);setConfirmation(false);setMaterial(r?.materialNeed==null?"":String(r.materialNeed));setEditing(true);}
  return <section><h2>Dữ liệu danh mục vận hành</h2>
    <label>Nhóm<select disabled={busy} value={group} onChange={e=>{setGroup(e.target.value);setRows([]);setLoaded(false);setEditing(false);setSelected(null);setMessage("");setAfter(0);}}>
      <option value="PRIORITY">Nhãn ưu tiên và thời hạn SLA</option><option value="REQUEST_TYPE">Loại yêu cầu nội bộ</option><option value="INCIDENT_CATEGORY">Nhóm sự cố</option><option value="CLEANING_TYPE">Loại vệ sinh</option><option value="BLOCKER">Lý do bị chặn</option><option value="TASK_PAUSE_REASON">Lý do tạm dừng công việc</option>
    </select></label>
    {group==="PRIORITY"?<PrioritySlaPage />:<>
    <button disabled={busy} onClick={()=>void load()}>Tải danh mục</button>{message&&<p role="status">{message}</p>}
    {loaded&&<><button disabled={busy||uncertain||group==="TASK_PAUSE_REASON"} onClick={()=>edit(null)}>Tạo giá trị</button>
      <table><thead><tr><th>Mã</th><th>Nhãn</th><th>Thứ tự</th>{group==="REQUEST_TYPE"&&<th>Phân loại khi tiếp nhận</th>}<th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>{rows.map(r=><tr key={r.id}>
        <td>{r.code}</td><td>{r.label}</td><td>{r.order}</td>{group==="REQUEST_TYPE"&&<td>{r.materialNeed==null?"Chưa phân loại":r.materialNeed?"Nhu cầu vật tư":"Yêu cầu nội bộ"}</td>}<td>{r.isActive?"Đang hoạt động":"Ngừng hoạt động"}</td><td><button disabled={busy||uncertain} onClick={()=>edit(r)}>Chỉnh sửa</button></td>
      </tr>)}</tbody></table>
      {after>0&&<button disabled={busy} onClick={()=>void load()}>Trang đầu</button>}
      {rows.length===100&&<button disabled={busy} onClick={()=>void load(rows[rows.length-1].id)}>Trang sau</button>}
    </>}
    {editing&&<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy||uncertain}>
      <legend>{selected?"Chỉnh sửa giá trị":"Tạo giá trị"}</legend>{selected?.meaningLocked&&<p>Giữ nguyên ý nghĩa lịch sử hoặc dành riêng. Chỉ lưu được thay đổi thứ tự hiển thị và trạng thái hoạt động được phép.</p>}
      <label>Mã<input required maxLength={group==="REQUEST_TYPE"?40:60} readOnly={selected?.meaningLocked} value={code} onChange={e=>setCode(e.target.value)} /></label>
      <label>Nhãn<input required maxLength={150} readOnly={selected?.meaningLocked} value={label} onChange={e=>setLabel(e.target.value)} /></label>
      <label>Mô tả<textarea maxLength={4000} readOnly={selected?.meaningLocked} value={description} onChange={e=>setDescription(e.target.value)} /></label>
      <label>Thứ tự hiển thị<input required type="number" min="-2147483648" max="2147483647" step="1" value={order} onChange={e=>setOrder(e.target.value)} /></label>
      {group==="REQUEST_TYPE"&&<label>Phân loại khi tiếp nhận<select required={active} disabled={selected?.meaningLocked} value={material} onChange={e=>setMaterial(e.target.value)}><option value="">Chưa phân loại</option><option value="false">Yêu cầu nội bộ</option><option value="true">Cần vật tư? Chuyển sang yêu cầu vật tư</option></select></label>}
      <label><input type="checkbox" checked={active} disabled={!selected||group==="TASK_PAUSE_REASON"&&selected.code==="END_OF_SHIFT"} onChange={e=>{setActive(e.target.checked);setConfirmation(false);}} />Đang hoạt động</label>
      {!active&&<label><input required type="checkbox" checked={confirmation} onChange={e=>setConfirmation(e.target.checked)} />Xác nhận vô hiệu hóa; tham chiếu lịch sử được giữ lại.</label>}
      <button>Lưu</button><button type="button" onClick={()=>setEditing(false)}>Hủy</button>
    </fieldset></form>}
    </>}
  </section>;
}
