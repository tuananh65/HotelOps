import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";

type Category = { id: number; code: string; name: string; description: string | null; departmentId: number | null; isActive: boolean; usageCount: number; version: string };
type Department = { id: number; name: string };
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function valid(v: unknown): v is Category {
  return isObject(v) && positive(v.id) && typeof v.code === "string" && typeof v.name === "string"
    && (v.description === null || typeof v.description === "string") && (v.departmentId === null || positive(v.departmentId))
    && typeof v.isActive === "boolean" && typeof v.usageCount === "number" && Number.isSafeInteger(v.usageCount) && v.usageCount >= 0
    && typeof v.version === "string" && /^"[A-F0-9]{64}"$/.test(v.version);
}
export function WorkCategoriesPage() {
  const { state } = useAuth();
  const generation = useRef({ authority: state.authorityGeneration, key: 0 });
  if (generation.current.authority !== state.authorityGeneration)
    generation.current = { authority: state.authorityGeneration, key: generation.current.key + 1 };
  if (state.status !== "AUTHENTICATED" || state.pending || state.session?.mustChangePassword) return null;
  return <BoundWorkCategoriesPage key={generation.current.key} />;
}
function BoundWorkCategoriesPage() {
  const { state } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const abort = useRef<AbortController | null>(null), sending = useRef(false);
  const [branch,setBranch] = useState(""), [loaded,setLoaded] = useState(false), [busy,setBusy] = useState(false), [message,setMessage] = useState("");
  const [rows,setRows] = useState<Category[]>([]), [departments,setDepartments] = useState<Department[]>([]), [hasMore,setHasMore] = useState(false);
  const [selected,setSelected] = useState<Category | null>(null), [editing,setEditing] = useState(false), [uncertain,setUncertain] = useState(false);
  const [code,setCode] = useState(""), [name,setName] = useState(""), [description,setDescription] = useState(""), [department,setDepartment] = useState("");
  const [active,setActive] = useState(true), [confirmed,setConfirmed] = useState(false);
  function clear() {setLoaded(false);setRows([]);setDepartments([]);setEditing(false);setHasMore(false);}
  useEffect(() => {clear();setMessage("");return () => abort.current?.abort();},[owner]);
  function endpoint(id?: number) {
    if(branch && !positive(Number(branch))) throw Error("Nhập mã chi nhánh hợp lệ.");
    return "/api/work-categories" + (id ? "/" + id : "") + (branch ? "?branchId=" + Number(branch) : "");
  }
  async function load(more = false) {
    if(!owner || sending.current || more && uncertain) return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");
    try {
      const url=endpoint() + (branch ? "&" : "?") + "afterId=" + (more ? rows.at(-1)?.id ?? 0 : 0);
      const response=await fetch(url,{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!response.ok) throw Error("Danh mục nhóm chưa khả dụng (" + response.status + "). Kiểm tra quyền và phạm vi quản trị hiện tại.");
      const value: unknown=await response.json();
      if(!isObject(value)||!Array.isArray(value.items)||!value.items.every(valid)||!Array.isArray(value.departments)
        ||!value.departments.every(d=>isObject(d)&&positive(d.id)&&typeof d.name==="string")) throw Error("Phản hồi danh mục không hợp lệ.");
      if(!controller.signal.aborted&&current.current===owner) {setRows(more?[...rows,...value.items]:value.items);setDepartments(value.departments as Department[]);setLoaded(true);setHasMore(value.items.length===100);setEditing(false);setUncertain(false);}
    } catch(e) {if(!controller.signal.aborted&&current.current===owner){clear();setMessage(String(e));}}
    finally {sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  function edit(row: Category | null) {
    setSelected(row);setCode(row?.code??"");setName(row?.name??"");setDescription(row?.description??"");setDepartment(row?.departmentId?.toString()??"");setActive(row?.isActive??true);setConfirmed(false);setEditing(true);
  }
  async function save() {
    if(!owner||sending.current||uncertain||!loaded)return;
    const controller=new AbortController();abort.current=controller;sending.current=true;setBusy(true);setMessage("");let submitted=false;
    try {
      const response=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal});
      if(!response.ok)throw Error("Xác minh phiên đăng nhập thất bại.");
      const csrf: unknown=await response.json();
      if(!isObject(csrf)||csrf.headerName!=="X-CSRF-TOKEN"||typeof csrf.csrfToken!=="string"||!/^[\x21-\x7e]+$/.test(csrf.csrfToken)||current.current!==owner)throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const url=endpoint(selected?.id);submitted=true;
      const saved=await fetch(url,{method:selected?"PUT":"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:controller.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":csrf.csrfToken,...(selected?{"If-Match":selected.version}:{})},
        body:JSON.stringify({code,name,description:description.trim()||null,departmentId:branch?Number(department):null,isActive:active,confirmation:confirmed})});
      if(!saved.ok){
        const error: unknown=await saved.json();
        const code=isObject(error)&&typeof error.code==="string"?error.code:null;
        // These responses reject the write before commit. Keep the form for correction.
        if(saved.status===409&&code==="MSG35"||saved.status===400&&code!==null&&[
          "INVALID_CATEGORY_INPUT","CATEGORY_SCOPE_MISMATCH","DEACTIVATION_CONFIRMATION_REQUIRED",
          "NEW_CATEGORY_MUST_BE_ACTIVE","ACTIVE_DEPARTMENT_REQUIRED"
        ].includes(code)){
          if(!controller.signal.aborted&&current.current===owner){
            setMessage(code==="MSG35"?"MSG35: Mã nhóm đã tồn tại. Nhập mã khác và lưu lại.":"Thông tin nhóm bị từ chối. Kiểm tra trường bắt buộc, bộ phận và xác nhận vô hiệu hóa.");
          }
          return;
        }
        throw Error(isObject(error)&&typeof error.title==="string"?error.title:"Chưa xác nhận lưu ("+saved.status+").");
      }
      const value: unknown=await saved.json();if(!valid(value))throw Error("Không xác minh được phản hồi lưu.");
      if(!controller.signal.aborted&&current.current===owner){clear();setMessage("Đã lưu nhóm. Tải lại danh mục để tiếp tục.");}
    } catch(e){if(!controller.signal.aborted&&current.current===owner){setMessage(String(e));if(submitted)setUncertain(true);}}
    finally{sending.current=false;if(!controller.signal.aborted&&current.current===owner)setBusy(false);}
  }
  if(!owner)return null;
  return <section><h2>Nhóm công việc</h2>
    <label>Mã chi nhánh (để trống với nhóm dùng chung)<input disabled={busy} type="number" min="1" value={branch} onChange={e=>{setBranch(e.target.value);clear();}} /></label>
    <p>Nhóm dùng chung cần quyền quản trị toàn hệ thống. Nhóm chi nhánh cần quyền chi nhánh và phải thuộc bộ phận trong chi nhánh đó.</p>
    <button disabled={busy} onClick={()=>void load()}>Tải các nhóm</button>
    {message&&<p role="status">{message}</p>}
    {loaded&&<><button disabled={busy||uncertain} onClick={()=>edit(null)}>Tạo nhóm</button>
      {!rows.length?<p>Không có nhóm trong phạm vi này.</p>:<table><thead><tr><th>Mã</th><th>Tên</th><th>Trạng thái</th><th>Sử dụng</th><th>Thao tác</th></tr></thead><tbody>{rows.map(r=><tr key={r.id}><td>{r.code}</td><td>{r.name}</td><td>{r.isActive?"Đang hoạt động":"Ngừng hoạt động"}</td><td>{r.usageCount}</td><td><button disabled={busy||uncertain} onClick={()=>edit(r)}>Chỉnh sửa</button></td></tr>)}</tbody></table>}
      {hasMore&&<button disabled={busy||uncertain} onClick={()=>void load(true)}>Tải thêm</button>}
    </>}
    {editing&&<form onSubmit={e=>{e.preventDefault();void save();}}><fieldset disabled={busy||uncertain}><legend>{selected?"Chỉnh sửa nhóm":"Tạo nhóm"}</legend>
      {selected&&selected.usageCount>0&&<p>Nhóm đã sử dụng giữ mã, tên và mô tả. Có thể đổi trạng thái hoạt động.</p>}
      <label>Mã<input required maxLength={40} readOnly={!!selected?.usageCount} value={code} onChange={e=>setCode(e.target.value)} /></label>
      <label>Tên<input required maxLength={150} readOnly={!!selected?.usageCount} value={name} onChange={e=>setName(e.target.value)} /></label>
      <label>Mô tả<textarea maxLength={4000} readOnly={!!selected?.usageCount} value={description} onChange={e=>setDescription(e.target.value)} /></label>
      {branch&&<label>Bộ phận<select required disabled={selected!==null} value={department} onChange={e=>setDepartment(e.target.value)}><option value="">Chọn bộ phận</option>{departments.map(d=><option key={d.id} value={d.id}>{d.name}</option>)}</select></label>}
      {selected&&<label><input type="checkbox" checked={active} onChange={e=>{setActive(e.target.checked);setConfirmed(false);}} />Đang hoạt động</label>}
      {!active&&<label><input required type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)} />Xác nhận vô hiệu hóa. Công việc hiện có giữ nguyên nhóm này.</label>}
      <button type="submit">Lưu</button><button type="button" onClick={()=>setEditing(false)}>Hủy</button>
    </fieldset></form>}
  </section>;
}
