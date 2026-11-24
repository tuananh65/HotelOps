import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { useAuth } from "./auth";
import { api, ApiError, message, type Row } from "./api";
import { Dialog, Empty, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import StatusSwitch from "./status-switch";
import "./internal-settings.css";

export default function InternalSettings(){
  const {branch,can}=useAuth();
  const [tab,setTab]=useState(location.hash.includes("tab=room")?"room":"requests");
  const [revision,bump]=useState(0),[edit,setEdit]=useState<Row|null>(null),[pending,setPending]=useState(false),[error,setError]=useState("");
  const room=tab==="room";
  const query=useQuery(`work/${room?"room-issue-types":"internal-request-types"}?branchId=${branch}`,revision);
  const options=useQuery(`workspace/options?branchId=${branch}`);
  const departments=(options.data?.departments||[]) as Row[];
  const rows:Row[]=room?["BROKEN","MISSING"].map(kind=>(query.data||[]).find((r:Row)=>r.kind===kind)||{kind,name:kind==="BROKEN"?"Hỏng đồ vật":"Thiếu đồ vật",isActive:false}):query.data?.items||[];
  async function save(row:Row,values:Row){
    if(pending)return;setPending(true);setError("");
    try{
      const path=room?"room-issue-types":`internal-request-types${row.referenceId?`/${row.referenceId}`:""}`;
      await api(`work/${path}?branchId=${branch}`,{name:row.name,description:row.description||null,defaultDepartmentId:row.defaultDepartmentId||null,isActive:row.isActive,kind:row.kind,rowVersion:row.rowVersion||null,...values},room||row.referenceId?"PUT":"POST");
      setEdit(null);bump(n=>n+1);
    }catch(e){if(e instanceof ApiError&&e.code==="STALE_ROW_VERSION"){setEdit(null);setError("Dữ liệu đã được thay đổi. Vui lòng mở Chỉnh sửa để xem bản mới nhất.");}else setError(message(e));bump(n=>n+1);}finally{setPending(false);}
  }
  return <div className="internal-settings">
    <PageTitle><h1>Cấu hình nội bộ</h1><p>Quản lý loại yêu cầu và báo cáo trong chi nhánh</p></PageTitle>
    <div className="tabs" aria-label="Nội dung cấu hình">
      <button aria-pressed={!room} onClick={()=>{setTab("requests");setError("");}}>Yêu cầu nội bộ</button>
      {can("ROOM_SUPPLY_CONFIG",false)&&<button aria-pressed={room} onClick={()=>{setTab("room");setError("");}}>Báo cáo phòng</button>}
    </div>
    <div className="internal-settings-toolbar"><h2>{room?"Loại báo cáo phòng":"Loại yêu cầu nội bộ"}</h2>{!room&&<button className="primary" onClick={()=>{setError("");setEdit({name:"",description:"",isActive:true});}}>Thêm loại yêu cầu</button>}</div>
    {error&&<AlertPopup>{error}</AlertPopup>}
    <State {...query} retry={()=>bump(n=>n+1)}>
      <table><thead><tr><th>Tên</th><th>Mô tả</th>{!room&&<th>Bộ phận nhận mặc định</th>}<th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{rows.map(row=><tr key={row.referenceId||row.kind}>
          <td data-label="Tên"><strong>{row.name}</strong></td><td data-label="Mô tả">{row.description||"—"}</td>
          {!room&&<td data-label="Bộ phận nhận mặc định">{departments.find(d=>d.departmentId===row.defaultDepartmentId)?.name||(row.defaultDepartmentId?"Bộ phận đã ngừng sử dụng":"Chọn khi tạo yêu cầu")}</td>}
          <td data-label="Trạng thái"><StatusSwitch active={row.isActive} label={row.name} disabled={pending||query.loading} onChange={()=>save(row,{isActive:!row.isActive})}/></td>
          <td data-label="Thao tác"><button disabled={pending} onClick={()=>{setError("");setEdit(row);}}>Chỉnh sửa</button></td>
        </tr>)}</tbody>
      </table>{!rows.length&&<Empty>Chưa có loại yêu cầu. Thêm loại để nhân viên lựa chọn khi tạo yêu cầu nội bộ.</Empty>}
    </State>
    {edit&&<Dialog title={edit.referenceId||room?"Chỉnh sửa loại":"Thêm loại yêu cầu"} close={()=>!pending&&setEdit(null)}>
      <form className="internal-settings-form" onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);save(edit,{name:f.get("name"),description:f.get("description")||null,defaultDepartmentId:room?null:Number(f.get("departmentId"))||null});}}>
        <label htmlFor="internal-type-name">Tên</label><input id="internal-type-name" name="name" required maxLength={120} defaultValue={edit.name}/>
        <label htmlFor="internal-type-description">Mô tả</label><textarea id="internal-type-description" name="description" maxLength={2000} rows={3} defaultValue={edit.description||""}/>
        {!room&&<State {...options}><label htmlFor="internal-type-department">Bộ phận nhận mặc định</label><select id="internal-type-department" name="departmentId" defaultValue={edit.defaultDepartmentId||""}><option value="">Chọn khi tạo yêu cầu</option>{edit.defaultDepartmentId&&!departments.some(d=>d.departmentId===edit.defaultDepartmentId)&&<option value={edit.defaultDepartmentId}>Bộ phận đã ngừng sử dụng</option>}{departments.map(d=><option key={d.departmentId} value={d.departmentId}>{d.name}</option>)}</select></State>}
        {error&&<AlertPopup>{error}</AlertPopup>}
        <button className="primary" disabled={pending||(!room&&(options.loading||!!options.error))}>Lưu thay đổi</button>
      </form>
    </Dialog>}
  </div>;
}
