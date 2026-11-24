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
  const room=(section||tab)==="room";
  const query=useQuery(`work/${room?"room-issue-types":"internal-request-types"}?branchId=${branch}`,revision);
  const options=useQuery(`workspace/options?branchId=${branch}`);
  const departments=(options.data?.departments||[]) as Row[];
  const rows:Row[]=room?["BROKEN","MISSING"].map(kind=>(query.data||[]).find((r:Row)=>r.kind===kind)||{kind,name:kind==="BROKEN"?"Hỏng đồ vật":"Thiếu đồ vật",isActive:false}):query.data?.items||[];
  async function save(row:Row,values:Row){
    if(pending)return;setPending(true);setError("");
    try{
      const path=room?"room-issue-types":`internal-request-types${row.referenceId?`/${row.referenceId}`:""}`;
      await api(`work/${path}?branchId=${branch}`,{name:row.name,description:row.description||null,defaultDepartmentId:row.defaultDepartmentId||null,senderRoleIds:row.senderRoleIds,recipientDepartmentIds:row.recipientDepartmentIds,recipientUserIds:row.recipientUserIds,impacts:row.impacts,requireNote:row.requireNote,isActive:row.isActive,kind:row.kind,rowVersion:row.rowVersion||null,...values},room||row.referenceId?"PUT":"POST");
      setEdit(null);bump(n=>n+1);
    }catch(e){if(e instanceof ApiError&&e.code==="STALE_ROW_VERSION"){setEdit(null);setError("Dữ liệu đã được thay đổi. Vui lòng mở Chỉnh sửa để xem bản mới nhất.");}else setError(message(e));bump(n=>n+1);}finally{setPending(false);}
  }
  return <div className="internal-settings">
    <PageTitle><h1>Cấu hình nội bộ</h1><p>Quản lý loại yêu cầu và báo cáo trong chi nhánh</p></PageTitle>
    {!section&&<div className="tabs" aria-label="Nội dung cấu hình">
      <button aria-pressed={!room} onClick={()=>{setTab("requests");setError("");}}>Yêu cầu nội bộ</button>
      {can("ROOM_SUPPLY_CONFIG",false)&&<button aria-pressed={room} onClick={()=>{setTab("room");setError("");}}>Báo cáo phòng</button>}
    </div>}
    <div className="internal-settings-toolbar"><h2>{room?"Loại báo cáo phòng":"Loại yêu cầu nội bộ"}</h2>{!room&&<button className="primary" onClick={()=>{setError("");setEdit({name:"",description:"",isActive:true});}}>Thêm loại yêu cầu</button>}</div>
    {error&&<AlertPopup>{error}</AlertPopup>}
    <State {...query} retry={()=>bump(n=>n+1)}>
      <table><thead><tr><th>Tên</th><th>Mô tả</th>{!room&&<th>Người / bộ phận nhận</th>}<th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{rows.map(row=><tr key={row.referenceId||row.kind}>
          <td data-label="Tên"><strong>{row.name}</strong></td><td data-label="Mô tả">{row.description||"—"}</td>
          {!room&&<td data-label="Người / bộ phận nhận">{row.recipientDepartmentIds||row.recipientUserIds?[...departments.filter(d=>row.recipientDepartmentIds?.includes(d.departmentId)).map(d=>d.name),...(query.data?.recipients||[]).filter((u:Row)=>row.recipientUserIds?.includes(u.userId)).map((u:Row)=>u.fullName)].join(", ")||"Nơi nhận không còn hoạt động":departments.find(d=>d.departmentId===row.defaultDepartmentId)?.name||"Chọn khi tạo yêu cầu"}</td>}
          <td data-label="Trạng thái"><StatusSwitch active={row.isActive} label={row.name} disabled={pending||query.loading} onChange={()=>save(row,{isActive:!row.isActive})}/></td>
          <td data-label="Thao tác"><button disabled={pending} onClick={()=>{setError("");setEdit(row);}}>Chỉnh sửa</button></td>
        </tr>)}</tbody>
      </table>{!rows.length&&<Empty>Chưa có loại yêu cầu. Thêm loại để nhân viên lựa chọn khi tạo yêu cầu nội bộ.</Empty>}
    </State>
    {edit&&<Dialog title={edit.referenceId||room?"Chỉnh sửa loại":"Thêm loại yêu cầu"} close={()=>!pending&&setEdit(null)}>
      <form className="internal-settings-form" onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);const ids=(key:string)=>f.getAll(key).map(Number);if(!room&&(!ids("senderRoleId").length||!ids("recipientDepartmentId").length&&!ids("recipientUserId").length)){setError("Chọn ít nhất một vai trò được gửi và một nơi nhận.");return;}save(edit,{name:f.get("name"),description:f.get("description")||null,defaultDepartmentId:null,...(!room?{senderRoleIds:ids("senderRoleId"),recipientDepartmentIds:ids("recipientDepartmentId"),recipientUserIds:ids("recipientUserId")}:{requireNote:f.has("requireNote"),impacts:["READINESS","SAFETY"].map(code=>({code,name:String(f.get(`impact-${code}`)),isActive:f.has(`enabled-${code}`)}))} )});}}>
        <label htmlFor="internal-type-name">Tên</label><input id="internal-type-name" name="name" required maxLength={120} defaultValue={edit.name}/>
        <label htmlFor="internal-type-description">Mô tả</label><textarea id="internal-type-description" name="description" maxLength={2000} rows={3} defaultValue={edit.description||""}/>
        {room&&<>
          <fieldset><legend>Mức ảnh hưởng</legend>{["READINESS","SAFETY"].map(code=>{const value=edit.impacts?.find((i:Row)=>i.code===code);return <section key={code}><label>{code==="SAFETY"?"Ảnh hưởng an toàn":"Ảnh hưởng khả năng phục vụ"}<input name={`impact-${code}`} required maxLength={120} defaultValue={value?.name||(code==="SAFETY"?"An toàn — giữ phòng bảo trì":"Khả năng sẵn sàng phục vụ")}/></label><div className="configuration-checklist"><label><input type="checkbox" name={`enabled-${code}`} defaultChecked={value?.isActive??true}/>Cho phép chọn</label></div></section>;})}<small>Mức an toàn giữ phòng bảo trì cho đến khi được giải phóng theo quy trình.</small></fieldset>
        </>}
        {!room&&<State {...options}>
          <fieldset><legend>Vai trò được gửi</legend><div className="configuration-checklist">{(query.data?.roles||[]).map((r:Row)=><label key={r.roleId}><input type="checkbox" name="senderRoleId" value={r.roleId} defaultChecked={edit.senderRoleIds?.includes(r.roleId)??true}/>{r.name}</label>)}</div></fieldset>
          <fieldset><legend>Gửi đến người / bộ phận</legend><p>Người gửi chọn một nơi nhận trong danh sách này.</p>
            <div className="configuration-checklist">{departments.map(d=><label key={d.departmentId}><input type="checkbox" name="recipientDepartmentId" value={d.departmentId} defaultChecked={edit.recipientDepartmentIds?.includes(d.departmentId)??edit.defaultDepartmentId===d.departmentId}/>{d.name}</label>)}</div>
            <div className="configuration-checklist">{(query.data?.recipients||[]).map((r:Row)=><label key={r.userId}><input type="checkbox" name="recipientUserId" value={r.userId} defaultChecked={edit.recipientUserIds?.includes(r.userId)??false}/>{r.fullName} · {r.employeeCode}</label>)}</div>
          </fieldset>
        </State>}
        {error&&<AlertPopup>{error}</AlertPopup>}
        <button className="primary" disabled={pending||(!room&&(options.loading||!!options.error))}>Lưu thay đổi</button>
      </form>
    </Dialog>}
  </div>;
}
