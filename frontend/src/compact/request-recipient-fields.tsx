import { useState } from "react";
import type { Row } from "./api";

export default function RequestRecipientFields({catalog,departments,onTypeChange}:{catalog:Row;departments:Row[];onTypeChange?:(type:Row|undefined)=>void}) {
  const [id,setId]=useState("");
  const selected=catalog.items?.find((r:Row)=>String(r.referenceId)===id);
  const restricted=selected&&(selected.recipientRoleIds!=null||selected.recipientDepartmentIds!=null||selected.recipientUserIds!=null);
  const targets=departments.filter(d=>!restricted||selected.recipientDepartmentIds?.includes(d.departmentId));
  return <>
    {catalog.configured&&<label>Loại yêu cầu<select name="categoryId" required value={id} onChange={e=>{setId(e.target.value);onTypeChange?.(catalog.items?.find((r:Row)=>String(r.referenceId)===e.target.value));}}>
      <option value="">Chọn loại yêu cầu</option>{(catalog.items||[]).filter((r:Row)=>r.isActive).map((r:Row)=><option key={r.referenceId} value={r.referenceId}>{r.name}</option>)}
    </select></label>}
    {selected&&<section className="panel" aria-label="Quy định loại yêu cầu">
      <p><strong>Được tạo:</strong> {selected.senderRoleIds?(catalog.roles||[]).filter((r:Row)=>selected.senderRoleIds.includes(r.roleId)).map((r:Row)=>r.name).join(", "):"Vai trò có quyền tạo yêu cầu"} · {selected.senderDepartmentIds?(catalog.departments||[]).filter((d:Row)=>selected.senderDepartmentIds.includes(d.departmentId)).map((d:Row)=>d.name).join(", "):"Bộ phận đang công tác hợp lệ"}</p>
      <p><strong>Nơi nhận:</strong> {selected.recipientRoleIds?(catalog.recipientRoles||[]).filter((r:Row)=>selected.recipientRoleIds.includes(r.roleId)).map((r:Row)=>r.name).join(", "):"Chọn bộ phận hoặc người trong danh sách bên dưới"}</p>
      <p><strong>Nội dung bắt buộc:</strong> Tiêu đề, bộ phận gửi, nơi nhận{selected.requireDescription?", mô tả chi tiết":""}.</p>
      {selected.description&&<p><strong>Hướng dẫn:</strong> {selected.description}</p>}
    </section>}
    <label>Gửi đến<select key={id} name="requestRecipient" required defaultValue={selected?.defaultDepartmentId?`department:${selected.defaultDepartmentId}`:""} disabled={catalog.configured&&!selected}>
      <option value="">Chọn một nơi nhận</option>
      <optgroup label="Bộ phận">{targets.map(d=><option key={d.departmentId} value={`department:${d.departmentId}`}>{d.name}</option>)}</optgroup>
      <optgroup label="Người nhận phù hợp vai trò">{(catalog.recipients||[]).filter((u:Row)=>selected?.recipientRoleIds!=null?u.roleIds?.some((roleId:number)=>selected.recipientRoleIds.includes(roleId)):selected?.recipientUserIds?.includes(u.userId)).map((u:Row)=><option key={u.userId} value={`user:${u.userId}`}>{u.fullName} · {u.employeeCode}</option>)}</optgroup>
    </select></label>
    {catalog.configured&&!catalog.items?.length&&<p>Chưa có loại yêu cầu cho vai trò và bộ phận của bạn. Liên hệ Quản lý vận hành.</p>}
  </>;
}
