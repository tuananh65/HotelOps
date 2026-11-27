import { AlertPopup } from "./alert-popup";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "./auth";
import { api, ApiError, message, timestamp, type Row } from "./api";
import { ConfirmationActions, Dialog, Empty, Pager, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import RoomIssueReportForm from "./room-issue-report-form";
import { ReplacementPanel } from "./asset-replacement";
import "./housekeeping-mobile.css";
import "./room-issue-detail.css";

const stages: Record<string,string> = {
  REPLACEMENT_REJECTED:"Kho từ chối — chờ giám sát xác minh", REPLACEMENT_REOPENED:"Đã xác minh lại — chờ kho cấp thay thế",
  REPORTED:"Chờ giám sát buồng phòng xác nhận", APPROVED:"Đã xác nhận, chờ yêu cầu kho",
  REPAIR_REQUESTED:"Chờ trưởng đội bảo trì tiếp nhận", REPAIR_CREATED:"Đang khắc phục",
  MATERIAL_REQUESTED:"Chờ cấp và bổ sung đồ", REMEDY_REPORTED:"Chờ giám sát kiểm tra",
  INSPECTION_REQUESTED:"Giám sát cần kiểm tra lại", INSPECTION_FAILED:"Kiểm tra không đạt, cần khắc phục tiếp",
  INSPECTION_PASSED:"Đã kiểm tra đạt", REJECTED:"Đã từ chối báo cáo",
  ASSET_PURCHASE_REQUESTED:"Chờ OM mua bổ sung", ASSET_PURCHASE_APPROVED:"Đã duyệt mua bổ sung", ASSET_PURCHASE_REJECTED:"OM từ chối mua bổ sung", MISSING_USED_VERIFIED:"Đã xác minh và ghi giảm đồ cũ bị thiếu",
  ASSET_DELIVERED:"Đã bổ sung tài sản — chờ giám sát kiểm tra", ASSET_DELIVERY_REJECTED:"Chưa đạt — cần bổ sung lại tài sản",
};
const stageName=(value:string)=>value==="LEGACY_ROOM_ISSUE_RECOVERED"?"Xác minh hồ sơ cũ và chuyển sửa chữa":stages[value?.replace("ROOM_ISSUE_","")]||value;
export default function RoomIssues({detailOnly=false,selectedId,onClose,onChanged}:{detailOnly?:boolean;selectedId?:number;onClose?:()=>void;onChanged?:()=>void}={}){
  const {branch,can,profile}=useAuth();
  const params=new URLSearchParams(location.hash.split("?")[1]);
  const roomId=Number(params.get("room"));
  const [id,setId]=useState(selectedId||Number(params.get("id"))||0),[creating,setCreating]=useState(!detailOnly&&params.get("create")==="1"&&!!roomId);
  const [revision,bump]=useState(0),[pending,setPending]=useState(false),[error,setError]=useState("");
  const isHead=profile.roles.some(r=>r.code==="HOUSEKEEPING_SUPERVISOR"&&(r.branchId===branch||r.branchId===null))&&can("IR_PROCESS",false)&&!profile.roles.some(r=>r.code==="OPERATION_MANAGER"&&(r.branchId===branch||r.branchId===null));
  const inFlight=useRef(false);
  const retries=useRef(new Map<string,Row>());
  const [confirmation,setConfirmation]=useState("");
  const [inspectionOpen,setInspectionOpen]=useState(false),[inspectionResult,setInspectionResult]=useState("PASS");
  const reception=profile.roles.some(r=>r.code==="RECEPTION"&&(r.branchId===branch||r.branchId===null));
  const [page,setPage]=useState(1),[search,setSearch]=useState("");
  useEffect(()=>{setPage(1);setSearch("");},[branch]);
  const list=useQuery(detailOnly?null:`work/room-issues?branchId=${branch}&page=${page}&search=${encodeURIComponent(search)}`,revision);
  const detail=useQuery(id?`work/room-issues/${id}`:null,revision);
  const manager=profile.roles.some(r=>r.code==="OPERATION_MANAGER"&&(r.branchId===branch||r.branchId===null));
  const workflow=useQuery(id&&detail.data&&!manager&&!detail.data.legacy&&!detail.data.canRequestInspection&&!(isHead&&detail.data.canReplaceMaterial)?`requests/${id}`:null,revision);
  const canHandle=workflow.data?.actions?.some((a:string)=>a==="claim"||a==="release")===true||detail.data?.canDecideMissingAssetPurchase===true||detail.data?.canVerifyMissingReusable===true||detail.data?.canVerifyMissingAsset===true||detail.data?.canRecordMissingAssetDelivery===true||detail.data?.canInspectMissingAssetDelivery===true;
  const types=useQuery(creating?`work/room-issue-types?branchId=${branch}`:null,revision);
  const objects=useQuery(creating?`work/room-issue-objects?branchId=${branch}&roomId=${roomId}${params.get("work")?`&workId=${Number(params.get("work"))}`:""}`:null,revision);
  const options=useQuery(isHead||detail.data?.canIssueReusable||(detail.data?.info?.reusable&&detail.data?.store)?`workspace/options?branchId=${branch}`:null,revision);
  const data=detail.data,w=data?.work?.fields,stage=data?.stage?.replace("ROOM_ISSUE_","");
  const reusable=data?.info?.reusable===true;
  const missingReusable=reusable&&data?.info?.kind==="MISSING"&&data?.info?.dirtySources!=null;
  const missingAsset=!!w?.assetId&&data?.info?.kind==="MISSING";
  const remainingReturns=Math.max(0,Number(data?.info?.quantity||0)-Number(data?.reusableRemedy?.returnedQuantity||0));
  const incompleteReusable=reusable&&(Number(data?.reusableRemedy?.issuedQuantity||0)!==Number(data?.info?.quantity)||(data?.info?.kind==="BROKEN"&&remainingReturns>0));
  const technicians=useQuery(data?.lead&&data?.canCreateRepair?`work/technicians?branchId=${branch}`:null,revision);
  const availableTechnicians: Row[] = (technicians.data?.items || []).filter((t: Row) => t.userId !== profile.user.fields.userId);
  const rows=(list.data?.items||[]) as Row[];
  function returnToWork(){const work=Number(params.get("work"));if(!work)return false;location.hash=`${profile.roles.some(r=>r.code==="HK_STAFF")?"cleaning":"work"}?kind=${params.get("workKind")==="REWORK"?"REWORK":"CLEANING"}&id=${work}`;return true;}
  async function save(path:string,body:Row,method="POST"){
    if(inFlight.current)return;inFlight.current=true;setPending(true);setError("");
    // Preserve both the operation key and original version on an uncertain retry.
    const {operationKey:_key,rowVersion:_version,roomRowVersion:_roomVersion,...intent}=body;
    const retryKey=JSON.stringify([path,method,intent]);
    const payload=retries.current.get(retryKey)||{...body,operationKey:crypto.randomUUID()};
    retries.current.set(retryKey,payload);
    try{const result=await api(path,payload,method);retries.current.delete(retryKey);if(path==="work/room-issues"||path==="work/room-issues/batch"){setCreating(false);if(returnToWork())return true;setId(result.workIds?.[0]||result.fields?.workId||0);}bump(n=>n+1);onChanged?.();return true;}
    catch(e){if(e instanceof ApiError&&e.status<500)retries.current.delete(retryKey);setError(message(e));bump(n=>n+1);return false;}finally{inFlight.current=false;setPending(false);}
  }
  const act=(command:string,body:Row={})=>save(`work/room-issues/${id}/${command}`,{rowVersion:w.rowVersion,operationKey:crypto.randomUUID(),roomRowVersion:data.room.rowVersion,...body});
  return <>
    {!detailOnly&&<PageTitle><h1>Sự cố phòng & Khắc phục</h1><p>Theo dõi duyệt, sửa chữa hoặc bổ sung và kiểm tra lại</p></PageTitle>}
    {error&&<AlertPopup tone="error">{error}</AlertPopup>}
    {!detailOnly&&<label className="room-issue-search">Tìm phòng hoặc sự cố<input type="search" value={search} placeholder="Số phòng, tên sự cố..." onChange={e=>{setSearch(e.target.value);setPage(1);}}/></label>}
    {!detailOnly&&<State {...list} retry={()=>bump(n=>n+1)}>
      <div className="room-issue-list">{rows.map(r=><button className="panel room-issue-card" key={r.workId} onClick={()=>{setId(r.workId);setError("");}}><strong>{r.title}</strong><span>{r.legacy?"Sự cố cần xử lý theo hồ sơ gốc":stageName(r.stage)}</span></button>)}</div>
      {!rows.length&&<Empty>{search?"Không tìm thấy sự cố phù hợp.":"Chưa có sự cố phòng trong phạm vi được giao."}</Empty>}
      <Pager page={page} total={list.data?.total||0} pageSize={25} onPage={setPage}/>
    </State>}
    {creating&&<Dialog title="Báo cáo sự cố phòng" close={()=>{if(!pending){setCreating(false);returnToWork();}}}>
      <State {...objects}><State {...types}>
        {objects.data&&<RoomIssueReportForm objects={objects.data} types={types.data||[]} reception={reception} pending={pending} cleaningWorkId={Number(params.get("work"))||null} submit={(type,lines,note)=>{
          const impacts=type.impacts||[{code:"READINESS",isActive:true}];
          save("work/room-issues/batch",{branchId:branch,roomId,typeId:type.referenceId,roomRowVersion:objects.data.rowVersion,operationKey:crypto.randomUUID(),note:note||null,impact:impacts.find((i:Row)=>i.code==="READINESS"&&i.isActive)?.code||impacts.find((i:Row)=>i.isActive)?.code||"READINESS",cleaningWorkId:Number(params.get("work"))||null,lines});
        }}/>}
      </State></State>{error&&<AlertPopup tone="error">{error}</AlertPopup>}
    </Dialog>}
    {!!id&&<Dialog title={w?.title||"Chi tiết sự cố phòng"} className="room-drawer room-issue-drawer" close={()=>{if(!pending){setId(0);onClose?.();}}}>
      <State {...detail}>{data?.legacy&&<section className="panel"><p>Sự cố này được ghi nhận trước quy trình báo cáo phòng hiện tại. Hồ sơ và lịch sử gốc được giữ nguyên.</p>
        {data.canRecover?<form onSubmit={async e=>{e.preventDefault();const f=new FormData(e.currentTarget);await save(`work/room-issues/${id}/recover`,{rowVersion:w.rowVersion,roomRowVersion:data.room.rowVersion,operationKey:crypto.randomUUID(),assetId:Number(f.get("assetId")),note:f.get("note")});}}>
          <h3>Xác minh và chuyển sửa chữa</h3><p>Chỉ chuyển khi đã kiểm tra thực tế và xác định tài sản hỏng. Việc chuyển không đóng sự cố hoặc đưa phòng về sẵn sàng.</p>
          <label>Tài sản hỏng trong phòng<select name="assetId" required defaultValue={w.assetId||""}><option value="">Chọn tài sản đã xác minh</option>{data.assets.filter((a:Row)=>!w.assetId||w.assetId===a.assetId).map((a:Row)=><option key={a.assetId} value={a.assetId}>{a.name}</option>)}</select></label>
          <label>Kết quả xác minh thực tế<textarea name="note" required maxLength={2000}/></label><button className="primary" disabled={pending||!data.assets.length}>Chuyển trưởng đội bảo trì</button>
        </form>:<p>Giám sát buồng phòng cần xác minh hồ sơ trước khi chuyển xử lý.</p>}
      </section>}{data&&!data.legacy&&w&&<div className="room-issue-detail">
        <p><strong>{reusable&&!missingReusable&&stage==="REPORTED"?"Chờ kho cấp thay thế":stageName(data.stage)}</strong></p><p>Phòng {data.room.code} · {data.info.objectName}</p>{w.description&&<p>{w.description}</p>}
        {missingReusable&&<section className="panel">
          <h3>Đồ thay thế bị thiếu</h3><p>Số lượng báo thiếu: {data.info.quantity}. Không trừ thêm đồ sạch khi xử lý báo cáo này.</p>
          <p>Giám sát xác minh số đồ cũ thực tế không thu hồi được trước khi ghi giảm. Số liệu và lịch sử lần thay đồ được giữ để truy vết.</p>
          {data.canVerifyMissingReusable&&<div className="room-report-actions"><button className="primary" disabled={pending} onClick={()=>setConfirmation("approve")}>Xác minh và ghi giảm đồ cũ</button><button className="danger" disabled={pending} onClick={()=>setConfirmation("reject")}>Từ chối báo cáo</button></div>}
        </section>}
        {missingAsset&&<section className="panel">
          <h3>Tài sản trong phòng bị thiếu</h3><p>Giám sát xác minh trước khi chuyển OM xử lý mua bổ sung. Duyệt mua chưa đồng nghĩa tài sản đã được nhận hoặc phòng đã sẵn sàng.</p>
          {data.canDecideMissingAssetPurchase&&<div className="room-report-actions"><button className="primary" disabled={pending} onClick={()=>setConfirmation("approve-purchase")}>Duyệt mua bổ sung</button><button className="danger" disabled={pending} onClick={()=>setConfirmation("reject-purchase")}>Từ chối mua bổ sung</button></div>}
          {data.canRecordMissingAssetDelivery&&<form onSubmit={async event=>{event.preventDefault();const fields=new FormData(event.currentTarget);await act("record-delivery",{replacementAssetId:Number(fields.get("replacementAssetId")),note:fields.get("note")});}}>
            <p>Đăng ký và bố trí tài sản mới trong phòng, đồng thời cập nhật tài sản bị thiếu ra khỏi phòng ở màn Tài sản &amp; Thiết bị, trước khi ghi nhận bàn giao.</p>
            <label>Tài sản đã bổ sung thực tế<select name="replacementAssetId" required defaultValue="" disabled={pending}><option value="">Chọn tài sản đã bố trí</option>{(data.replacementAssets||[]).map((asset:Row)=><option key={asset.assetId} value={asset.assetId}>{asset.name}</option>)}</select></label>
            {!(data.replacementAssets||[]).length&&<p>Chưa có tài sản thay thế phù hợp được ghi nhận trong phòng.</p>}
            <label>Ghi chú bàn giao<textarea name="note" required maxLength={2000} disabled={pending}/></label>
            <button className="primary" disabled={pending||!(data.replacementAssets||[]).length}>Ghi nhận đã bổ sung</button>
          </form>}
          {data.canInspectMissingAssetDelivery&&<button className="primary" disabled={pending} onClick={()=>{setInspectionResult("PASS");setInspectionOpen(true);}}>Kiểm tra tài sản đã bổ sung</button>}
        </section>}
        {reusable&&!missingReusable&&<section className="panel">
          <h3>Thay thế đồ tái sử dụng</h3>
          <p>Số lượng báo cáo: {data.info.quantity}. Đã cấp thay thế: {data.reusableRemedy?.issuedQuantity||0}. Đã nhận đồ hỏng: {data.reusableRemedy?.returnedQuantity||0}.</p>
          {data.info.kind==="BROKEN"&&remainingReturns>0&&<p>Còn {remainingReturns} đồ hỏng chưa nhận về kho. Chưa thể kiểm tra đạt cho đến khi nhận đủ.</p>}
          {data.reusableRemedy?.lossAccountingReviewRequired&&<p>Đồ mất chưa được ghi nhận hoàn trả hoặc xóa sổ. Chênh lệch sổ kho chờ quy trình đối soát mất được phê duyệt riêng.</p>}
          {(data.canIssueReusable||(data.store&&stage==="INSPECTION_FAILED"))&&<State {...options} retry={()=>bump(n=>n+1)}><form key={`${id}:${stage}`} onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act(data.canIssueReusable?"issue-replacement":"report-remedy",{warehouseId:Number(f.get("warehouseId"))||null,quantity:data.canIssueReusable?Number(data.info.quantity):0,returnedQuantity:data.info.kind==="BROKEN"?Number(f.get("returnedQuantity")):0,note:f.get("note")||null});}}>
            <h3>{data.canIssueReusable?"Cấp đủ đồ thay thế":"Bổ sung kết quả khắc phục"}</h3>
            <label>Kho cấp / nhận đồ hỏng<select name="warehouseId" required defaultValue=""><option value="">Chọn kho</option>{(options.data?.warehouses||[]).map((r:Row)=><option key={r.warehouseId} value={r.warehouseId}>{r.name}</option>)}</select></label>
            <p>{data.canIssueReusable?`Cấp đủ ${data.info.quantity} đồ sạch thay thế trong một lần.`:"Đồ thay thế đã cấp đủ; lần này không xuất thêm kho."}</p>
            {data.info.kind==="BROKEN"?<label>Số đồ hỏng thực nhận thêm<input name="returnedQuantity" type="number" min="0" max={remainingReturns} step="0.0001" required defaultValue={0}/></label>:<p>Đồ bị mất: số lượng hoàn trả bằng 0.</p>}
            <label>Ghi chú xử lý<textarea name="note" maxLength={2000}/></label>
            <button className="primary" disabled={pending||!canHandle}>{data.canIssueReusable?"Xác nhận cấp đủ và số thực nhận":"Gửi lại giám sát kiểm tra"}</button>
          </form></State>}
          {data.canIssueReusable&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("reject-replacement")}>Từ chối cấp thay thế</button>}
          {isHead&&stage==="REPLACEMENT_REJECTED"&&<><p>Phòng vẫn bị chặn cho đến khi giám sát quyết định sau xác minh.</p><button disabled={pending||!canHandle} onClick={()=>setConfirmation("approve")}>Xác minh lại và chuyển kho</button><button disabled={pending||!canHandle} onClick={()=>setConfirmation("reject")}>Hủy báo cáo sai</button></>}
        </section>}
        {!reusable&&data.lead&&data.canCreateRepair&&<form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act("create-repair",{userId:Number(f.get("technician")),note:f.get("note")});}}>
          <h3>Tiếp nhận và phân công sửa chữa</h3>
          <p>Chỉ hiển thị nhân viên đang trong ca. Người đang rảnh được xếp trước để thuận tiện phân công.</p>
          <State {...technicians}><label>Nhân viên bảo trì<select name="technician" required defaultValue=""><option value="">Chọn người thực hiện</option>{availableTechnicians.map((t:Row)=><option key={t.userId} value={t.userId}>{t.fullName} · {t.busy ? "Đang làm việc" : "Đang rảnh"} · {t.open} việc đang mở</option>)}</select></label>{!availableTechnicians.length&&<p role="status">Chưa có nhân viên bảo trì đang trong ca để nhận công việc. Kiểm tra phân ca với quản lý vận hành.</p>}</State>
          <label>Hướng dẫn xử lý<textarea name="note" maxLength={2000}/></label>
          <button className="primary" disabled={pending||!canHandle||technicians.loading||!!technicians.error||!availableTechnicians.length}>Tiếp nhận và giao sửa chữa</button>
        </form>}
        {workflow.error&&<State {...workflow} retry={()=>bump(n=>n+1)}>{null}</State>}
        {workflow.data?.actionBlockedReason&&<section className="panel" role="status"><p>{workflow.data.actionBlockedReason==="ACTIVE_SHIFT_REQUIRED"?"Bạn cần được phân ca và đang trong giờ ca tại chi nhánh này để tiếp nhận hoặc xử lý sự cố. Liên hệ quản lý vận hành nếu cần kiểm tra phân ca.":message(new ApiError(409,workflow.data.actionBlockedReason))}</p><button onClick={()=>bump(n=>n+1)}>Kiểm tra lại</button></section>}
        {workflow.data?.request?.handlerUserId&&<p>Người đang xử lý: {workflow.data.request.handlerUserId===profile.user.fields.userId?"Bạn":`Nhân viên ${workflow.data.request.handlerUserId}`}</p>}
        {!reusable&&isHead&&(missingAsset?data.canVerifyMissingAsset:["REPORTED","INSPECTION_FAILED"].includes(stage))&&<button className="primary" disabled={pending||!canHandle} onClick={()=>setConfirmation("approve")}>Xác nhận báo cáo</button>}
        {!reusable&&isHead&&(missingAsset?data.canVerifyMissingAsset:stage==="REPORTED")&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("reject")}>Từ chối báo cáo</button>}
        {!reusable&&isHead&&stage==="APPROVED"&&data.info.kind==="BROKEN"&&<button disabled={pending||!canHandle} onClick={()=>setConfirmation("send-repair")}>Gửi trưởng đội bảo trì khắc phục</button>}

        {!reusable&&!missingAsset&&isHead&&(["APPROVED","INSPECTION_FAILED"].includes(stage)||data.canReplaceMaterial)&&data.info.kind==="MISSING"&&<State {...options}><form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);const number=(key:string)=>Number(f.get(key));act("request-material",{departmentId:number("departmentId"),warehouseId:number("warehouseId"),itemId:number("itemId"),unitId:number("unitId"),quantity:number("quantity")});}}>
          <h3>Yêu cầu kho bổ sung</h3>
          <label>Bộ phận yêu cầu<select name="departmentId" required defaultValue=""><option value="">Chọn bộ phận của bạn</option>{(options.data?.departments||[]).filter((d:Row)=>profile.memberships.some(m=>m.branchId===branch&&m.departmentId===d.departmentId&&m.kind==="EMPLOYMENT")).map((d:Row)=><option key={d.departmentId} value={d.departmentId}>{d.name}</option>)}</select></label>
          <label>Kho cấp<select name="warehouseId" required defaultValue=""><option value="">Chọn kho</option>{(options.data?.warehouses||[]).map((r:Row)=><option key={r.warehouseId} value={r.warehouseId}>{r.name}</option>)}</select></label>
          <label>Vật tư bổ sung<select name="itemId" required defaultValue={data.info.itemId||""}><option value="">Chọn vật tư thay thế</option>{(options.data?.items||[]).filter((r:Row)=>!data.info.itemId||r.itemId===data.info.itemId).map((r:Row)=><option key={r.itemId} value={r.itemId}>{r.name}</option>)}</select></label>
          <label>Đơn vị<select name="unitId" required defaultValue=""><option value="">Chọn đơn vị</option>{(options.data?.references||[]).filter((r:Row)=>r.kind==="UOM").map((r:Row)=><option key={r.referenceId} value={r.referenceId}>{r.name}</option>)}</select></label>
          <label>Số lượng<input name="quantity" type="number" min="0.001" step="0.001" required defaultValue={1}/></label>
          <p>Chỉ yêu cầu số lượng thực sự còn thiếu. Kho tiếp nhận và ghi nhận cấp phát thực tế.</p><button disabled={pending||(!canHandle&&!data.canReplaceMaterial)}>Tạo và gửi yêu cầu vật tư</button>
        </form></State>}
        {!!data.maintenance.length&&<section><h3>Công việc bảo trì</h3>{data.maintenance.map((m:Row)=><div key={m.workId}><p>{can("MAINTENANCE_VIEW")?<a href={`#work?kind=MAINTENANCE&id=${m.workId}`}>{m.title}</a>:m.title} · {m.result==="FAIL"?"Không thể sửa chữa — Cần thay thế":m.status==="CLOSED"?"Đã kiểm tra đạt":m.status==="COMPLETED"?"Đã sửa xong — Chờ kiểm tra phòng":"Đang xử lý"}</p>{data.lead&&m.result==="FAIL"&&<ReplacementPanel id={m.workId} onSaved={()=>bump(n=>n+1)}/>}</div>)}</section>}
        {!!data.materials.length&&<section><h3>Yêu cầu bổ sung</h3>{data.materials.map((d:Row)=><p key={d.documentId}>{can("MR_VIEW")?<a href={`#documents?kind=MATERIAL_REQUEST&id=${d.documentId}`}>Mở phiếu vật tư</a>:"Phiếu vật tư"} · {d.status==="FULLY_ISSUED"?"Đã cấp đủ":"Đang xử lý"}</p>)}</section>}
        {data.lead&&stage==="INSPECTION_REQUESTED"&&data.info.kind==="BROKEN"&&data.maintenance.some((m:Row)=>m.status==="COMPLETED"&&!m.technicalReviewed)&&<form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act("reopen-technical-review",{note:f.get("note")});}}>
          <p>Phiếu cũ chưa có nghiệm thu kỹ thuật. Mở lại để thực hiện checklist và gửi nghiệm thu theo quy trình hiện tại.</p>
          <label>Lý do mở lại<textarea name="note" required minLength={10} maxLength={2000}/></label><button disabled={pending}>Mở lại kiểm tra kỹ thuật</button>
        </form>}
        {!reusable&&data.store&&stage==="MATERIAL_REQUESTED"&&<form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act("report-remedy",{note:f.get("note"),documentId:Number(f.get("documentId"))||null});}}>
          <h3>{data.lead?"Nghiệm thu kỹ thuật":"Báo cáo đã khắc phục"}</h3>{data.store&&<label>Phiếu đã cấp đủ<select name="documentId" required defaultValue=""><option value="">Chọn phiếu</option>{data.materials.filter((d:Row)=>d.status==="FULLY_ISSUED").map((d:Row)=> <option key={d.documentId} value={d.documentId}>Phiếu đã cấp đủ #{d.documentId}</option>)}</select></label>}
          <label>Kết quả sửa chữa / bổ sung thực tế<textarea name="note" required minLength={10} maxLength={2000}/></label><button className="primary" disabled={pending||!canHandle}>Gửi giám sát buồng phòng kiểm tra</button>
        </form>}
        {!reusable&&isHead&&(stage==="REMEDY_REPORTED"||data.canRequestInspection)&&<button className="primary" disabled={pending||(!canHandle&&!data.canRequestInspection)} onClick={()=>setConfirmation("request-inspection")}>Tiếp nhận kiểm tra phòng</button>}
        {(reusable?data.canReviewReusable:data.supervisor)&&stage==="INSPECTION_REQUESTED"&&<button className="primary" disabled={pending||!canHandle} onClick={()=>{setInspectionResult(incompleteReusable?"FAIL":"PASS");setInspectionOpen(true);setError("");}}>Gửi kết quả kiểm tra</button>}
        <section className="room-issue-history"><h3>Lịch sử xử lý</h3>{data.events.map((e:Row,i:number)=><article key={i}><strong>{stageName(e.eventType)}</strong><small>{timestamp(e.occurredAt)}</small>{e.reason&&<p>{e.reason}</p>}</article>)}</section>
      </div>}</State>{error&&<AlertPopup tone="error">{error}</AlertPopup>}
    </Dialog>}
    {inspectionOpen&&<Dialog className="confirmation-popup" title="Gửi kết quả kiểm tra phòng" close={()=>!pending&&setInspectionOpen(false)}><form onSubmit={async e=>{e.preventDefault();const f=new FormData(e.currentTarget);if(await act(inspectionResult==="PASS"?"inspection-pass":"inspection-fail",{note:f.get("note")||null}))setInspectionOpen(false);}}>
      <p>Kiểm tra thực tế theo checklist giấy, sau đó ghi nhận kết quả.</p><label>Kết quả<select name="result" required value={inspectionResult} onChange={e=>setInspectionResult(e.target.value)}><option value="PASS" disabled={incompleteReusable}>Đạt — hoàn tất kiểm tra phòng</option><option value="FAIL">Không đạt — báo khắc phục tiếp</option></select></label><label>Ghi chú (bắt buộc khi không đạt)<textarea name="note" required={inspectionResult==="FAIL"} maxLength={2000}/></label>
      {error&&<AlertPopup tone="error">{error}</AlertPopup>}<ConfirmationActions pending={pending} disabled={!canHandle||(inspectionResult==="PASS"&&incompleteReusable)} cancel={()=>setInspectionOpen(false)}/>
    </form></Dialog>}
    {confirmation&&<Dialog className="confirmation-popup" title="Xác nhận xử lý báo cáo" close={()=>!pending&&setConfirmation("")}><form onSubmit={async e=>{e.preventDefault();if(await act(confirmation,{note:new FormData(e.currentTarget).get("note")}))setConfirmation("");}}><label>{confirmation==="reject"?"Lý do từ chối":"Kết quả xác minh / ghi chú"}<textarea name="note" required={["approve","reject","reject-replacement","approve-purchase","reject-purchase"].includes(confirmation)} minLength={["approve","reject","reject-replacement","approve-purchase","reject-purchase"].includes(confirmation)?10:undefined} maxLength={2000}/></label>{error&&<AlertPopup tone="error">{error}</AlertPopup>}<ConfirmationActions pending={pending} disabled={!canHandle&&!(confirmation==="request-inspection"&&data?.canRequestInspection)} cancel={()=>setConfirmation("")}/></form></Dialog>}
  </>;
}
