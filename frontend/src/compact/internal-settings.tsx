import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { useAuth } from "./auth";
import { api, ApiError, message, type Row } from "./api";
import { Dialog, Empty, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import StatusSwitch from "./status-switch";
import "./internal-settings.css";

export default function InternalSettings({section}:{section?:"requests"|"room"}={}){
  const {branch,can}=useAuth();
  const [tab,setTab]=useState(location.hash.includes("tab=room")?"room":"requests");
  const [revision,bump]=useState(0),[edit,setEdit]=useState<Row|null>(null),[pending,setPending]=useState(false),[error,setError]=useState("");
  const [recipientMode,setRecipientMode]=useState("roles");
  const room=(section||tab)==="room";
  const query=useQuery(`work/${room?"room-issue-types":"internal-request-types"}?branchId=${branch}`,revision);
  const rows:Row[]=room?query.data||[]:query.data?.items||[];
  async function save(row:Row,values:Row){
    if(pending)return;setPending(true);setError("");
    try{
      const path=room?"room-issue-types":`internal-request-types${row.referenceId?`/${row.referenceId}`:""}`;
      await api(`work/${path}?branchId=${branch}`,{name:row.name,description:row.description||null,defaultDepartmentId:row.defaultDepartmentId||null,senderRoleIds:row.senderRoleIds,senderDepartmentIds:row.senderDepartmentIds,requireDescription:row.requireDescription??false,recipientDepartmentIds:row.recipientDepartmentIds,recipientUserIds:row.recipientUserIds,recipientRoleIds:row.recipientRoleIds,impacts:row.impacts,requireNote:false,isActive:row.isActive,kind:row.kind,referenceId:row.referenceId,createNew:room&&!row.referenceId,rowVersion:row.rowVersion||null,...values},room||row.referenceId?"PUT":"POST");
      setEdit(null);bump(n=>n+1);
    }catch(e){if(e instanceof ApiError&&e.code==="STALE_ROW_VERSION"){setEdit(null);setError("Dữ liệu đã được thay đổi. Vui lòng mở Chỉnh sửa để xem bản mới nhất.");}else setError(message(e));bump(n=>n+1);}finally{setPending(false);}
  }
  return <div className="internal-settings">
    <PageTitle><h1>Cấu hình nội bộ</h1><p>Quản lý loại yêu cầu và báo cáo trong chi nhánh</p></PageTitle>
    {!section&&<div className="tabs" aria-label="Nội dung cấu hình">
      <button aria-pressed={!room} onClick={()=>{setTab("requests");setError("");}}>Yêu cầu nội bộ</button>
      {can("ROOM_SUPPLY_CONFIG",false)&&<button aria-pressed={room} onClick={()=>{setTab("room");setError("");}}>Báo cáo phòng</button>}
    </div>}
    <div className="internal-settings-toolbar"><h2>{room?"Loại báo cáo phòng":"Loại yêu cầu nội bộ"}</h2><button className="primary" onClick={()=>{setError("");setRecipientMode("roles");setEdit({name:"",description:"",kind:"BROKEN",isActive:true,requireDescription:true});}}>{room?"Thêm loại báo cáo phòng":"Thêm loại yêu cầu"}</button></div>
    {error&&<AlertPopup>{error}</AlertPopup>}
    <State {...query} retry={()=>bump(n=>n+1)}>
      <table><thead><tr><th>Tên</th><th>Mô tả</th>{!room&&<><th>Được tạo</th><th>Nơi nhận</th><th>Nội dung bắt buộc</th></>}<th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{rows.map(row=><tr key={row.referenceId||row.kind}>
          <td data-label="Tên"><strong>{row.name}</strong></td><td data-label="Mô tả">{row.description||"—"}</td>
          {!room&&<>
            <td data-label="Được tạo">{row.senderRoleIds?(query.data?.roles||[]).filter((r:Row)=>row.senderRoleIds.includes(r.roleId)).map((r:Row)=>r.name).join(", ")||"Vai trò không còn hoạt động":"Vai trò có quyền tạo"}<small>{row.senderDepartmentIds?(query.data?.departments||[]).filter((d:Row)=>row.senderDepartmentIds.includes(d.departmentId)).map((d:Row)=>d.name).join(", ")||"Bộ phận không còn hoạt động":"Mọi bộ phận hợp lệ"}</small></td>
            <td data-label="Nơi nhận">{row.recipientRoleIds?(query.data?.recipientRoles||[]).filter((r:Row)=>row.recipientRoleIds.includes(r.roleId)).map((r:Row)=>r.name).join(", ")||"Vai trò không còn hoạt động":[...(query.data?.departments||[]).filter((d:Row)=>(row.recipientDepartmentIds||[row.defaultDepartmentId]).includes(d.departmentId)).map((d:Row)=>d.name),...(query.data?.recipients||[]).filter((u:Row)=>row.recipientUserIds?.includes(u.userId)).map((u:Row)=>u.fullName)].join(", ")||"Bộ phận trong chi nhánh"}</td>
            <td data-label="Nội dung bắt buộc">Tiêu đề, bộ phận gửi, nơi nhận{row.requireDescription?", mô tả chi tiết":""}</td>
          </>}
          <td data-label="Trạng thái"><StatusSwitch active={row.isActive} label={row.name} disabled={pending||query.loading} onChange={()=>save(row,{isActive:!row.isActive})}/></td>
          <td data-label="Thao tác"><button disabled={pending} onClick={()=>{setError("");setRecipientMode(row.recipientRoleIds?"roles":"targets");setEdit(row);}}>Chỉnh sửa</button></td>
        </tr>)}</tbody>
      </table>{!rows.length&&<Empty>Chưa có loại yêu cầu. Thêm loại để nhân viên lựa chọn khi tạo yêu cầu nội bộ.</Empty>}
    </State>
    {!room&&<section className="request-routing-reference"><h2>Luồng chuyên biệt</h2><p>Các tuyến dưới đây có quy trình riêng, chỉ xem tại đây. Cấu hình loại thông thường không thay đổi quyền hoặc bước duyệt của các luồng này.</p>
      <article><h3>Bổ sung vật tư giữa ca</h3><p><strong>Được tạo:</strong> Nhân viên buồng phòng có ca đang hoạt động, được cấp quyền tạo.</p><p><strong>Nơi nhận:</strong> Thủ kho cùng chi nhánh; cấp đủ hoặc từ chối.</p><p><strong>Cần có:</strong> Vật tư trong bộ ca, số lượng nguyên dương, lý do ít nhất 10 ký tự.</p></article>
      <article><h3>Bổ sung hàng cho kho</h3><p><strong>Được tạo:</strong> Thủ kho có quyền bổ sung kho và ca hợp lệ.</p><p><strong>Nơi nhận:</strong> Quản lý vận hành duyệt → thủ kho ghi nhận hàng thực nhận.</p><p><strong>Cần có:</strong> Kho nhận trong chi nhánh, mặt hàng, số lượng dương, lý do ít nhất 10 ký tự.</p></article>
    </section>}
    {edit&&<Dialog title={edit.referenceId?"Chỉnh sửa loại":room?"Thêm loại báo cáo phòng":"Thêm loại yêu cầu"} close={()=>!pending&&setEdit(null)}>
      <form className="internal-settings-form" onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);const ids=(key:string)=>f.getAll(key).map(Number);if(!room&&(!ids("senderRoleId").length||(recipientMode==="roles"?!ids("recipientRoleId").length:!ids("recipientDepartmentId").length&&!ids("recipientUserId").length))){setError("Chọn vai trò được gửi và ít nhất một nơi nhận.");return;}save(edit,{name:f.get("name"),description:f.get("description")||null,defaultDepartmentId:null,...(!room?{senderRoleIds:ids("senderRoleId"),senderDepartmentIds:ids("senderDepartmentId").length?ids("senderDepartmentId"):null,requireDescription:f.has("requireDescription"),recipientRoleIds:recipientMode==="roles"?ids("recipientRoleId"):null,recipientDepartmentIds:recipientMode==="targets"?ids("recipientDepartmentId"):null,recipientUserIds:recipientMode==="targets"?ids("recipientUserId"):null}:{kind:edit.referenceId?edit.kind:f.get("kind"),requireNote:false,impacts:["READINESS","SAFETY"].map(code=>({code,name:String(f.get(`impact-${code}`)),isActive:f.has(`enabled-${code}`)}))} )});}}>
        {room&&!edit.referenceId&&<label>Quy trình xử lý<select name="kind" required defaultValue="BROKEN"><option value="BROKEN">Hỏng thiết bị — chuyển bảo trì</option><option value="MISSING">Thiếu đồ dùng — bổ sung từ kho</option></select></label>}
        <label htmlFor="internal-type-name">Tên</label><input id="internal-type-name" name="name" required maxLength={120} defaultValue={edit.name}/>
        <label htmlFor="internal-type-description">{room?"Mô tả":"Hướng dẫn nội dung cần gửi"}</label><textarea id="internal-type-description" name="description" maxLength={2000} rows={3} defaultValue={edit.description||""}/>
        {room&&<>
          <fieldset><legend>Mức ảnh hưởng</legend>{["READINESS","SAFETY"].map(code=>{const value=edit.impacts?.find((i:Row)=>i.code===code);return <section key={code}><label>{code==="SAFETY"?"Ảnh hưởng an toàn":"Ảnh hưởng khả năng phục vụ"}<input name={`impact-${code}`} required maxLength={120} defaultValue={value?.name||(code==="SAFETY"?"An toàn — giữ phòng bảo trì":"Khả năng sẵn sàng phục vụ")}/></label><div className="configuration-checklist"><label><input type="checkbox" name={`enabled-${code}`} defaultChecked={value?.isActive??true}/>Cho phép chọn</label></div></section>;})}<small>Mức an toàn giữ phòng bảo trì cho đến khi được giải phóng theo quy trình.</small></fieldset>
        </>}
        {!room&&<>
          <fieldset><legend>Vai trò được gửi</legend><div className="configuration-checklist">{(query.data?.roles||[]).map((r:Row)=><label key={r.roleId}><input type="checkbox" name="senderRoleId" value={r.roleId} defaultChecked={edit.senderRoleIds?.includes(r.roleId)??true}/>{r.name}</label>)}</div></fieldset>
          <fieldset><legend>Bộ phận được gửi</legend><p>Để trống để cho phép mọi bộ phận hợp lệ. Nếu chọn, người tạo phải đồng thời thuộc bộ phận này và có vai trò được gửi.</p><div className="configuration-checklist">{(query.data?.departments||[]).map((d:Row)=><label key={d.departmentId}><input type="checkbox" name="senderDepartmentId" value={d.departmentId} defaultChecked={edit.senderDepartmentIds?.includes(d.departmentId)??false}/>{d.name}</label>)}</div></fieldset>
          <fieldset><legend>Nơi nhận yêu cầu</legend><label>Cách chọn nơi nhận<select value={recipientMode} onChange={e=>setRecipientMode(e.target.value)}><option value="roles">Người thuộc vai trò được chọn</option><option value="targets">Bộ phận hoặc người cụ thể</option></select></label>
            {recipientMode==="roles"?<><p>Người gửi chọn một người nhận đủ quyền xử lý, cùng chi nhánh và thuộc vai trò sau.</p><div className="configuration-checklist">{(query.data?.recipientRoles||[]).map((r:Row)=><label key={r.roleId}><input type="checkbox" name="recipientRoleId" value={r.roleId} defaultChecked={edit.recipientRoleIds?.includes(r.roleId)??false}/>{r.name}</label>)}</div></>:<>
              <p>Người gửi chọn một trong các bộ phận hoặc người cụ thể sau.</p><strong>Bộ phận nhận</strong><div className="configuration-checklist">{(query.data?.departments||[]).map((d:Row)=><label key={d.departmentId}><input type="checkbox" name="recipientDepartmentId" value={d.departmentId} defaultChecked={(edit.recipientDepartmentIds||[edit.defaultDepartmentId]).includes(d.departmentId)}/>{d.name}</label>)}</div>
              <strong>Người nhận</strong><div className="configuration-checklist">{(query.data?.recipients||[]).map((u:Row)=><label key={u.userId}><input type="checkbox" name="recipientUserId" value={u.userId} defaultChecked={edit.recipientUserIds?.includes(u.userId)??false}/>{u.fullName} · {u.employeeCode}</label>)}</div>
            </>}
          </fieldset>
          <fieldset><legend>Nội dung cần có</legend><p>Luôn cần tiêu đề, bộ phận gửi và nơi nhận. Hướng dẫn phía trên được hiển thị khi chọn loại yêu cầu.</p><div className="configuration-checklist"><label><input type="checkbox" name="requireDescription" defaultChecked={edit.requireDescription??false}/>Bắt buộc mô tả chi tiết</label></div></fieldset>
        </>}
        {error&&<AlertPopup>{error}</AlertPopup>}
        <button className="primary" disabled={pending||query.loading||!!query.error}>Lưu thay đổi</button>
      </form>
    </Dialog>}
  </div>;
}
