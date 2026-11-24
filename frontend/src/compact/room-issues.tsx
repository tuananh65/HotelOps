import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { useAuth } from "./auth";
import { api, message, timestamp, type Row } from "./api";
import { Dialog, Empty, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import "./housekeeping-mobile.css";

const stages: Record<string,string> = {
  REPORTED:"Chờ giám sát buồng phòng xác nhận", APPROVED:"Đã xác nhận, chờ yêu cầu kho",
  REPAIR_REQUESTED:"Chờ giám sát bảo trì tiếp nhận", REPAIR_CREATED:"Đang khắc phục",
  MATERIAL_REQUESTED:"Chờ cấp và bổ sung đồ", REMEDY_REPORTED:"Chờ giám sát kiểm tra",
  INSPECTION_REQUESTED:"Giám sát cần kiểm tra lại", INSPECTION_FAILED:"Kiểm tra không đạt, cần khắc phục tiếp",
  INSPECTION_PASSED:"Đã kiểm tra đạt", REJECTED:"Đã từ chối báo cáo",
};
const stageName=(value:string)=>stages[value?.replace("ROOM_ISSUE_","")]||value;
export default function RoomIssues(){
  const {branch,can,profile}=useAuth();
  const params=new URLSearchParams(location.hash.split("?")[1]);
  const roomId=Number(params.get("room"));
  const [id,setId]=useState(Number(params.get("id"))||0),[creating,setCreating]=useState(params.get("create")==="1"&&!!roomId);
  const [revision,bump]=useState(0),[pending,setPending]=useState(false),[error,setError]=useState("");
  const [typeId,setType]=useState(""),[object,setObject]=useState("");
  const isHead=profile.roles.some(r=>r.code==="HOUSEKEEPING_SUPERVISOR"&&(r.branchId===branch||r.branchId===null))&&can("IR_PROCESS",false)&&!profile.roles.some(r=>r.code==="OPERATION_MANAGER"&&(r.branchId===branch||r.branchId===null));
  const [confirmation,setConfirmation]=useState("");
  const reception=profile.roles.some(r=>r.code==="RECEPTION"&&(r.branchId===branch||r.branchId===null));
  const list=useQuery(`work/room-issues?branchId=${branch}`,revision);
  const detail=useQuery(id?`work/room-issues/${id}`:null,revision);
  const workflow=useQuery(id?`requests/${id}`:null,revision);
  const canHandle=workflow.data?.actions?.some((a:string)=>a==="claim"||a==="release")===true;
  const types=useQuery(creating?`work/room-issue-types?branchId=${branch}`:null,revision);
  const objects=useQuery(creating?`work/room-issue-objects?branchId=${branch}&roomId=${roomId}`:null,revision);
  const options=useQuery(isHead?`workspace/options?branchId=${branch}`:null,revision);
  const data=detail.data,w=data?.work?.fields,stage=data?.stage?.replace("ROOM_ISSUE_","");
  const rows=(list.data||[]) as Row[];
  const objectRows=[...(objects.data?.assets||[]).map((a:Row)=>({key:`asset:${a.assetId}`,name:a.name})),...(objects.data?.items||[]).map((a:Row)=>({key:`item:${a.itemId}`,name:a.name}))];
  const selectedType=(types.data||[]).find((t:Row)=>String(t.referenceId)===typeId);
  async function save(path:string,body:Row,method="POST"){
    if(pending)return;setPending(true);setError("");
    try{const result=await api(path,body,method);if(path==="work/room-issues"){setCreating(false);setId(result.fields.workId);}bump(n=>n+1);return true;}
    catch(e){setError(message(e));bump(n=>n+1);return false;}finally{setPending(false);}
  }
  const act=(command:string,body:Row={})=>save(`work/room-issues/${id}/${command}`,{rowVersion:w.rowVersion,operationKey:crypto.randomUUID(),roomRowVersion:data.room.rowVersion,...body});
  return <>
    <PageTitle><h1>Sự cố phòng & Khắc phục</h1><p>Theo dõi duyệt, sửa chữa hoặc bổ sung và kiểm tra lại</p></PageTitle>
    {error&&<AlertPopup>{error}</AlertPopup>}
    <State {...list} retry={()=>bump(n=>n+1)}>
      <div className="room-issue-list">{rows.map(r=><button className="panel room-issue-card" key={r.workId} onClick={()=>{setId(r.workId);setError("");}}><strong>{r.title}</strong><span>{stageName(r.stage)}</span></button>)}</div>
      {!rows.length&&<Empty>Chưa có sự cố phòng trong phạm vi được giao.</Empty>}
    </State>
    {creating&&<Dialog title="Báo cáo sự cố phòng" close={()=>!pending&&setCreating(false)}>
      <State {...objects}><State {...types}>
        <form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);const [kind,key]=object.split(":");save("work/room-issues",{branchId:branch,roomId,typeId:Number(typeId),assetId:kind==="asset"?Number(key):null,itemId:kind==="item"?Number(key):null,roomRowVersion:objects.data.rowVersion,operationKey:crypto.randomUUID(),note:f.get("note")||null,quantity:Number(f.get("quantity")||1),impact:f.get("impact")||"READINESS",cleaningWorkId:Number(params.get("work"))||null});}}>
          <p><strong>Phòng {objects.data?.code}</strong></p>
          <label>Loại báo cáo<select required value={typeId} onChange={e=>setType(e.target.value)}><option value="">Chọn loại báo cáo</option>{(types.data||[]).filter((t:Row)=>t.isActive&&(!reception||t.kind==="BROKEN")).map((t:Row)=><option key={t.referenceId} value={t.referenceId}>{t.name}</option>)}</select></label>
          {!(types.data||[]).some((t:Row)=>t.isActive)&&<p>Quản lý vận hành cần cấu hình và bật loại báo cáo trước.</p>}
          <label>Đồ vật thuộc phòng<select required value={object} onChange={e=>setObject(e.target.value)}><option value="">Chọn đồ vật</option>{objectRows.map(o=><option key={o.key} value={o.key}>{o.name}</option>)}</select></label>
          {!objectRows.length&&<p>Phòng chưa có tài sản hoặc vật tư đã ghi nhận được cấp vào phòng.</p>}
          <p>Tiêu đề: {selectedType&&object?`${selectedType.name} · ${objectRows.find(o=>o.key===object)?.name} · Phòng ${objects.data?.code}`:"Tự tạo khi chọn loại và đồ vật"}</p>
          <label>Số lượng<input name="quantity" type="number" min="0.0001" step="0.0001" required defaultValue={1}/></label>
          <label>Mức ảnh hưởng<select name="impact"><option value="READINESS">Khả năng sẵn sàng phục vụ</option><option value="SAFETY">An toàn — giữ phòng bảo trì</option></select></label>
          <label>Ghi chú<textarea name="note" maxLength={2000}/></label>
          <button className="primary" disabled={pending||!typeId||!object}>Gửi giám sát buồng phòng</button>
        </form>
      </State></State>{error&&<AlertPopup>{error}</AlertPopup>}
    </Dialog>}
    {!!id&&<Dialog title={w?.title||"Chi tiết sự cố phòng"} className="room-drawer" close={()=>!pending&&setId(0)}>
      <State {...detail}>{data&&w&&<>
        <p><strong>{stageName(data.stage)}</strong></p><p>Phòng {data.room.code} · {data.info.objectName}</p>{w.description&&<p>{w.description}</p>}
        {workflow.error&&<State {...workflow} retry={()=>bump(n=>n+1)}>{null}</State>}
        {workflow.data?.request?.handlerUserId&&<p>Người đang xử lý: {workflow.data.request.handlerUserId===profile.user.fields.userId?"Bạn":`Nhân viên ${workflow.data.request.handlerUserId}`}</p>}
        {isHead&&["REPORTED","INSPECTION_FAILED"].includes(stage)&&<button className="primary" disabled={pending||!canHandle} onClick={()=>setConfirmation("approve")}>Xác nhận báo cáo</button>}
        {isHead&&stage==="REPORTED"&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("reject")}>Từ chối báo cáo</button>}
        {isHead&&stage==="APPROVED"&&data.info.kind==="BROKEN"&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("send-repair")}>Gửi giám sát bảo trì khắc phục</button>}
        {data.lead&&data.canCreateRepair&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("create-repair")}>Tiếp nhận giám sát sửa chữa</button>}
        {isHead&&(["APPROVED","INSPECTION_FAILED"].includes(stage)||data.canReplaceMaterial)&&data.info.kind==="MISSING"&&<State {...options}><form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);const number=(key:string)=>Number(f.get(key));act("request-material",{departmentId:number("departmentId"),warehouseId:number("warehouseId"),itemId:number("itemId"),unitId:number("unitId"),quantity:number("quantity")});}}>
          <h3>Yêu cầu kho bổ sung</h3>
          <label>Bộ phận yêu cầu<select name="departmentId" required defaultValue=""><option value="">Chọn bộ phận của bạn</option>{(options.data?.departments||[]).filter((d:Row)=>profile.memberships.some(m=>m.branchId===branch&&m.departmentId===d.departmentId&&m.kind==="EMPLOYMENT")).map((d:Row)=><option key={d.departmentId} value={d.departmentId}>{d.name}</option>)}</select></label>
          <label>Kho cấp<select name="warehouseId" required defaultValue=""><option value="">Chọn kho</option>{(options.data?.warehouses||[]).map((r:Row)=><option key={r.warehouseId} value={r.warehouseId}>{r.name}</option>)}</select></label>
          <label>Vật tư bổ sung<select name="itemId" required defaultValue={data.info.itemId||""}><option value="">Chọn vật tư thay thế</option>{(options.data?.items||[]).filter((r:Row)=>!data.info.itemId||r.itemId===data.info.itemId).map((r:Row)=><option key={r.itemId} value={r.itemId}>{r.name}</option>)}</select></label>
          <label>Đơn vị<select name="unitId" required defaultValue=""><option value="">Chọn đơn vị</option>{(options.data?.references||[]).filter((r:Row)=>r.kind==="UOM").map((r:Row)=><option key={r.referenceId} value={r.referenceId}>{r.name}</option>)}</select></label>
          <label>Số lượng<input name="quantity" type="number" min="0.001" step="0.001" required defaultValue={1}/></label>
          <p>Chỉ yêu cầu số lượng thực sự còn thiếu. Kho tiếp nhận và ghi nhận cấp phát thực tế.</p><button disabled={pending||!canHandle}>Tạo và gửi yêu cầu vật tư</button>
        </form></State>}
        {!!data.maintenance.length&&<section><h3>Công việc bảo trì</h3>{data.maintenance.map((m:Row)=><p key={m.workId}>{can("MAINTENANCE_VIEW")?<a href={`#work?kind=MAINTENANCE&id=${m.workId}`}>{m.title}</a>:m.title} · {m.status==="CLOSED"?"Đã nghiệm thu":m.status==="COMPLETED"?"Chờ nghiệm thu":"Đang xử lý"}</p>)}</section>}
        {!!data.materials.length&&<section><h3>Yêu cầu bổ sung</h3>{data.materials.map((d:Row)=><p key={d.documentId}>{can("MR_VIEW")?<a href={`#documents?kind=MATERIAL_REQUEST&id=${d.documentId}`}>Mở phiếu vật tư</a>:"Phiếu vật tư"} · {d.status==="FULLY_ISSUED"?"Đã cấp đủ":"Đang xử lý"}</p>)}</section>}
        {data.lead&&stage==="REPAIR_CREATED"&&data.maintenance.some((m:Row)=>m.status==="PENDING")&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("start-repair")}>Bắt đầu giám sát sửa chữa</button>}
        {((data.lead&&stage==="REPAIR_CREATED"&&data.maintenance.some((m:Row)=>["IN_PROGRESS","COMPLETED"].includes(m.status)))||(data.store&&stage==="MATERIAL_REQUESTED"))&&<form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act("report-remedy",{note:f.get("note"),documentId:Number(f.get("documentId"))||null});}}>
          <h3>Báo cáo đã khắc phục</h3>{data.store&&<label>Phiếu đã cấp đủ<select name="documentId" required defaultValue=""><option value="">Chọn phiếu</option>{data.materials.filter((d:Row)=>d.status==="FULLY_ISSUED").map((d:Row)=><option key={d.documentId} value={d.documentId}>Phiếu đã cấp đủ #{d.documentId}</option>)}</select></label>}
          <label>Kết quả sửa chữa / bổ sung thực tế<textarea name="note" required minLength={10} maxLength={2000}/></label><button disabled={pending||!canHandle}>Gửi giám sát buồng phòng kiểm tra</button>
        </form>}
        {isHead&&stage==="REMEDY_REPORTED"&&<button className="primary" disabled={pending||!canHandle} onClick={()=>setConfirmation("request-inspection")}>Tiếp nhận kiểm tra phòng</button>}
        {data.supervisor&&stage==="INSPECTION_REQUESTED"&&<form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act(f.get("result")==="PASS"?"inspection-pass":"inspection-fail",{note:f.get("note")||null});}}>
          <h3>Kiểm tra theo checklist giấy</h3><label>Kết quả<select name="result" required><option value="PASS">Đạt — đánh giá điều kiện sẵn sàng phòng</option><option value="FAIL">Không đạt — báo khắc phục tiếp</option></select></label><label>Ghi chú (bắt buộc khi không đạt)<textarea name="note" maxLength={2000}/></label><button disabled={pending||!canHandle}>Gửi kết quả kiểm tra</button>
        </form>}
        <h3>Lịch sử xử lý</h3>{data.events.map((e:Row,i:number)=><section key={i}><strong>{stageName(e.eventType)}</strong><small>{timestamp(e.occurredAt)}</small>{e.reason&&<p>{e.reason}</p>}</section>)}
      </>}</State>{error&&<AlertPopup>{error}</AlertPopup>}
    </Dialog>}
    {confirmation&&<Dialog title="Xác nhận xử lý báo cáo" close={()=>!pending&&setConfirmation("")}><form onSubmit={async e=>{e.preventDefault();if(await act(confirmation,{note:new FormData(e.currentTarget).get("note")}))setConfirmation("");}}><label>{confirmation==="reject"?"Lý do từ chối":"Kết quả xác minh / ghi chú"}<textarea name="note" required={["approve","reject"].includes(confirmation)} minLength={["approve","reject"].includes(confirmation)?10:undefined} maxLength={2000}/></label>{error&&<AlertPopup>{error}</AlertPopup>}<button disabled={pending||!canHandle}>Xác nhận</button></form></Dialog>}
  </>;
}
