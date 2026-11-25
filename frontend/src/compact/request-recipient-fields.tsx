import { useState } from "react";
import type { Row } from "./api";

export default function RequestRecipientFields({catalog,departments}:{catalog:Row;departments:Row[]}) {
  const [id,setId]=useState("");
  const selected=catalog.items?.find((r:Row)=>String(r.referenceId)===id);
  const restricted=selected&&(selected.recipientRoleIds!=null||selected.recipientDepartmentIds!=null||selected.recipientUserIds!=null);
  const targets=departments.filter(d=>!restricted||selected.recipientDepartmentIds?.includes(d.departmentId));
  return <>
    {catalog.configured&&<label>Loại yêu cầu<select name="categoryId" required value={id} onChange={e=>setId(e.target.value)}>
      <option value="">Chọn loại yêu cầu</option>{(catalog.items||[]).filter((r:Row)=>r.isActive).map((r:Row)=><option key={r.referenceId} value={r.referenceId}>{r.name}</option>)}
    </select></label>}
    {selected?.description&&<p>{selected.description}</p>}
    <label>Gửi đến<select key={id} name="requestRecipient" required defaultValue={selected?.defaultDepartmentId?`department:${selected.defaultDepartmentId}`:""} disabled={catalog.configured&&!selected}>
      <option value="">Chọn một nơi nhận</option>
      <optgroup label="Bộ phận">{targets.map(d=><option key={d.departmentId} value={`department:${d.departmentId}`}>{d.name}</option>)}</optgroup>
      <optgroup label="Người nhận phù hợp vai trò">{(catalog.recipients||[]).filter((u:Row)=>selected?.recipientRoleIds!=null?u.roleIds?.some((roleId:number)=>selected.recipientRoleIds.includes(roleId)):selected?.recipientUserIds?.includes(u.userId)).map((u:Row)=><option key={u.userId} value={`user:${u.userId}`}>{u.fullName} · {u.employeeCode}</option>)}</optgroup>
    </select></label>
    {catalog.configured&&!catalog.items?.length&&<p>Chưa có loại yêu cầu cho vai trò của bạn. Liên hệ Quản lý vận hành.</p>}
  </>;
}
