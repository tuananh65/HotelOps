import { usePagedRows } from "./use-paged-rows";
import { useEffect, useRef, useState } from "react";
import { api, ApiError, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { ConfirmationActions, Dialog, Empty, State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";
import PageTitle, { PageTitleTarget } from "./page-title";
import Work from "./work";
import Documents from "./documents";
import InventoryDocuments from "./inventory-documents";
import RoomIssues from "./room-issues";
import ReusableInventory from "./reusable-inventory";
import ReplacementQueue, { ReplacementPanel } from "./asset-replacement";
import { useDomainRequests } from "./use-domain-requests";
import "./requests.css";

const types:Record<string,string>={CONSUMABLE_REFILL_REQUEST:"Bổ sung vật tư giữa ca",WAREHOUSE_REPLENISHMENT_REQUEST:"Bổ sung hàng cho kho",GENERAL_INTERNAL_REQUEST:"Yêu cầu nội bộ",INCIDENT:"Sự cố phòng"};
const statuses:Record<string,string>={PENDING_SUPERVISOR:"Chờ giám sát xác nhận",PENDING_STOREKEEPER:"Chờ kho cấp phát",PENDING_OPERATIONS_MANAGER:"Chờ quản lý vận hành xác nhận",PENDING_RECEIPT:"Chờ nhận hàng thực tế",FULFILLED:"Đã cấp phát",COMPLETED:"Đã hoàn thành",REJECTED:"Đã từ chối",CANCELLED:"Đã hủy"};
const actions:Record<string,string>={claim:"Nhận xử lý",release:"Trả về hàng đợi",approve:"Xác nhận yêu cầu",reject:"Từ chối",issue:"Xác nhận cấp phát",cancel:"Hủy yêu cầu",receive:"Ghi nhận hàng thực nhận"};
const roles:Record<string,string>={HOUSEKEEPING_SUPERVISOR:"Giám sát buồng phòng",STOREKEEPER:"Thủ kho",OPERATION_MANAGER:"Quản lý vận hành",HK_STAFF:"Nhân viên buồng phòng",MAINTENANCE_SUPERVISOR:"Trưởng đội bảo trì",RECEPTION:"Lễ tân"};
const queues=[['all','Tất cả'],['mine','Đã gửi'],['action','Cần xử lý'],['progress','Đang xử lý'],['completed','Hoàn thành'],['rejected','Từ chối']];
const queueHelp:Record<string,string>={all:'Tất cả yêu cầu trong phạm vi bạn được phép xem.',mine:'Yêu cầu do bạn tạo, ở mọi trạng thái, bao gồm cả yêu cầu đã hoàn thành.',action:'Yêu cầu đang chờ bạn hoặc vai trò của bạn xử lý.',progress:'Yêu cầu chưa kết thúc và đang trong quy trình xử lý.',completed:'Yêu cầu đã hoàn tất xử lý, không phân biệt người tạo.',rejected:'Yêu cầu đã bị từ chối hoặc hủy.'};
const roomTypes:Record<string,string>={ROOM_DURABLE_ITEM_MISSING:"Báo thiếu đồ dùng",ROOM_EQUIPMENT_DAMAGE:"Báo thiết bị hỏng",DURABLE_ITEM_REMEDIATION:"Bổ sung đồ dùng cho phòng",MAINTENANCE_REMEDIATION:"Khắc phục thiết bị",ROOM_REINSPECTION:"Kiểm tra lại phòng"};
Object.assign(types,roomTypes);
Object.assign(types,{REUSABLE_WRITE_OFF:"Ghi giảm vật tư tái sử dụng",ASSET_REPLACEMENT:"Thay mới tài sản"});
Object.assign(types,{MISSING_ASSET_PURCHASE:"Mua bổ sung tài sản bị thiếu"});
Object.assign(statuses,{ROOM_ISSUE_ASSET_PURCHASE_REQUESTED:"Chờ OM mua bổ sung",ROOM_ISSUE_ASSET_PURCHASE_APPROVED:"Đã duyệt mua bổ sung",ROOM_ISSUE_ASSET_PURCHASE_REJECTED:"OM từ chối mua bổ sung"});
Object.assign(statuses,{ROOM_ISSUE_ASSET_DELIVERED:"Đã bổ sung — chờ kiểm tra",ROOM_ISSUE_ASSET_DELIVERY_REJECTED:"Cần bổ sung lại tài sản",ROOM_ISSUE_INSPECTION_PASSED:"Đã kiểm tra đạt",ROOM_ISSUE_REJECTED:"Báo cáo đã bị từ chối"});
Object.assign(statuses,{APPROVED:"Đã ghi giảm",REPLACEMENT_CONFIRMED:"Chờ OM xử lý",REPLACEMENT_APPROVED:"Đã đồng ý thay mới",REPLACEMENT_REJECTED:"Đã từ chối",REPLACEMENT_INSTALLED:"Đã thay thực tế",REPLACEMENT_PROPOSED:"Chờ giám sát xác nhận"});
Object.assign(statuses,{ACCEPTED:"Đã tiếp nhận",PENDING_MAINTENANCE_SUPERVISOR:"Chờ trưởng đội bảo trì",PENDING_REINSPECTION:"Chờ kiểm tra lại",REWORK_REQUIRED:"Cần khắc phục lại"});
const textStatus=(value:string)=>statuses[value]||({OPEN:"Mới",IN_PROGRESS:"Đang xử lý",CLOSED:"Đã đóng",PENDING:"Chờ xử lý"} as Record<string,string>)[value]||value;
function requestTone(status:string){
  if(['FULFILLED','COMPLETED','CLOSED','APPROVED','REPLACEMENT_INSTALLED','ROOM_ISSUE_INSPECTION_PASSED'].includes(status))return 'done';
  if(['REJECTED','CANCELLED','REPLACEMENT_REJECTED','ROOM_ISSUE_ASSET_PURCHASE_REJECTED','ROOM_ISSUE_REJECTED'].includes(status))return 'closed';
  if(['IN_PROGRESS','ACCEPTED','REPLACEMENT_APPROVED','ROOM_ISSUE_ASSET_PURCHASE_APPROVED','ROOM_ISSUE_ASSET_DELIVERED'].includes(status))return 'progress';
  return 'pending';
}
function requestError(error:unknown){const labels:Record<string,string>={SUPPLY_WHOLE_QUANTITY_REQUIRED:"Số lượng vật tư bổ sung phải là số nguyên.",APPROVED_LINES_MISMATCH:"Số lượng cấp phải khớp đầy đủ từng mặt hàng đã đề nghị. Tải lại yêu cầu trước khi xác nhận.",REQUEST_ALREADY_CLAIMED:"Yêu cầu đã được người khác tiếp nhận. Tải lại để xem người đang xử lý.",ACTIVE_SHIFT_REQUIRED:"Ca không còn hoạt động hợp lệ; không thể cấp thêm vật tư vào ca này.",STOCK_INSUFFICIENT:"Kho không đủ số lượng để cấp phát.",INVALID_REQUEST_QUANTITIES:"Chọn ít nhất một vật tư với số lượng lớn hơn 0.",IDEMPOTENCY_KEY_REUSED:"Nội dung lần gửi lại đã thay đổi. Đóng và mở lại biểu mẫu trước khi gửi.",REQUEST_READ_ONLY:"Bạn chỉ được xem yêu cầu này.",USE_REQUEST_WORKFLOW:"Mở yêu cầu trong hộp yêu cầu để xử lý đúng quy trình."};return error instanceof ApiError&&labels[error.code]?labels[error.code]:message(error);}

export default function Requests(){
  const {branch,can,profile}=useAuth();
  const manager=profile.roles.some(r=>r.code==='OPERATION_MANAGER'&&(r.branchId===branch||r.branchId===null));
  const requestAccess=can('IR_VIEW');
  const branchRoles=profile.roles.filter(r=>r.branchId===branch||r.branchId===null);
  const storekeeper=branchRoles.length>0&&branchRoles.every(r=>r.code==='STOREKEEPER');
  const params=new URLSearchParams(location.hash.split('?')[1]);
  const defaultQueue=profile.roles.some(r=>r.code==='MAINTENANCE_SUPERVISOR'&&(r.branchId===branch||r.branchId===null))?'action':'all';
  const [tab,setTab]=useState(params.get('tab')||(location.hash.startsWith('#fulfillment')||!requestAccess?'materials':'inbox')),[queue,setQueue]=useState(defaultQueue),[search,setSearch]=useState(''),[filters,setFilters]=useState(false),[page,setPage]=useState(1),[revision,bump]=useState(0);
  const [id,setId]=useState(!['replacement','missing-assets'].includes(params.get('domain')||'')&&(params.get('tab')||'inbox')==='inbox'&&!location.hash.startsWith('#fulfillment')?Number(params.get('id'))||0:0),[create,setCreate]=useState(params.get('create')||''),[command,setCommand]=useState(''),[error,setError]=useState(''),[pending,setPending]=useState(false);
  const [sort,setSort]=useState('latest');
  useEffect(()=>{const refresh=()=>bump(n=>n+1);const timer=window.setInterval(()=>{if(!pending&&document.visibilityState==='visible')refresh();},30000);window.addEventListener('focus',refresh);return()=>{window.clearInterval(timer);window.removeEventListener('focus',refresh);};},[pending]);
  const list=usePagedRows(requestAccess?`requests?branchId=${branch}&queue=${queue}&search=${encodeURIComponent(search)}`:null,revision);
  const domains=useDomainRequests(revision,queue,search);
  const [replacement,setReplacement]=useState<number|null>(params.get('domain')==='replacement'?Number(params.get('id'))||null:null);
  const [missingAsset,setMissingAsset]=useState<number|null>(params.get('domain')==='missing-assets'?Number(params.get('id'))||null:null);
  const previousBranch=useRef(branch);
  useEffect(()=>{if(previousBranch.current===branch)return;previousBranch.current=branch;setId(0);setReplacement(null);setMissingAsset(null);setCommand('');setCreate('');setError('');setPage(1);},[branch]);
  const inbox={loading:list.loading||domains.loading,error:list.error||domains.error};
  const ordered=[...(list.data||[]),...domains.data].sort((a,b)=>sort==='title'?a.title.localeCompare(b.title,'vi'):(sort==='oldest'?1:-1)*(new Date(a.updatedAt).getTime()-new Date(b.updatedAt).getTime()));
  const pageRows=ordered.slice((page-1)*25,page*25);
  const allowed=useQuery(requestAccess?`requests/types?branchId=${branch}`:null,revision);
  const detail=useQuery(requestAccess&&tab==='inbox'&&id?`requests/${id}`:null,revision);
  const [operationKey,setKey]=useState(()=>crypto.randomUUID());
  const data=detail.data;
  useEffect(()=>{if(command&&!detail.loading&&data&&!data.actions.includes(command))setCommand('');},[command,detail.loading,data]);
  const refillIssue=command==='issue'&&data?.request.requestType==='CONSUMABLE_REFILL_REQUEST';
  const insufficientRefill=refillIssue&&data.items.some((item:Row)=>typeof item.currentStock==='number'&&item.currentStock<item.requestedQuantity);
  const roomDomain=!!data&&!!roomTypes[data.request.requestType];
  const roomRoot=roomDomain&&data.parentRequestWorkId?data.parentRequestWorkId:id;
  function openAction(value:string){setCommand(value);setKey(crypto.randomUUID());setError('');}
  async function act(body:Row){
    if(pending)return;
    if(insufficientRefill){setError('Kho không đủ toàn bộ vật tư đã đề nghị. Đóng hộp xác nhận và chọn Từ chối, ghi rõ vật tư thiếu.');return;}
    setPending(true);setError('');
    try{await api(`requests/${id}/${command==='receive'?'receive':`actions/${command}`}`,{rowVersion:data.request.rowVersion,operationKey,...body});setCommand('');bump(n=>n+1);}
    catch(e){setError(requestError(e));bump(n=>n+1);}finally{setPending(false);}
  }
  function open(row:Row){
    if(row.domain==='missing-assets'){setMissingAsset(row.workId);return;}
    if(row.domain==='replacement'){setReplacement(row.workId);return;}
    if(row.domain==='write-offs'){setTab('write-offs');return;}
    if(row.structured){setId(row.workId);return;}
    location.hash=row.requestType==='INCIDENT'?`work?kind=INCIDENT&id=${row.workId}`:`requests?tab=ordinary&id=${row.workId}&legacy=1`;
  }
  return <div className="request-workspace">
    <PageTitle><h1>Yêu cầu nội bộ</h1><p>Theo dõi người xử lý, cấp phát và kết quả trong cùng một nơi</p></PageTitle>
    <nav className="request-tabs" aria-label="Nhóm yêu cầu">{[...(requestAccess?[['inbox','Hộp yêu cầu'],['ordinary','Yêu cầu thông thường']]:[]),...(can('MR_VIEW')?[['materials','Yêu cầu vật tư']]:[]),...(can('INCIDENT_VIEW')||can('IR_CREATE')?[['rooms','Sự cố phòng']]:[]),...(domains.writeOffAccess?[['write-offs','Ghi giảm vật tư']]:[]),...(domains.replacementAccess?[['replacements','Thay tài sản']]:[])].map(([key,name])=><button key={key} aria-pressed={tab===key} onClick={()=>{setTab(key);setId(0);setCreate('');location.hash=`requests?tab=${key}`;}}>{name}</button>)}</nav>
    <PageTitleTarget.Provider value={null}>
    {tab==='ordinary'&&requestAccess?<Work onlyKind="INTERNAL_REQUEST" personal={!manager} />:tab==='materials'&&can('MR_VIEW')?<><p className="request-context">Yêu cầu vật tư theo chứng từ, gồm lịch sử duyệt và cấp phát. Yêu cầu bổ sung giữa ca được theo dõi trong Hộp yêu cầu.</p>{storekeeper?<InventoryDocuments key={branch} group="fulfillment"/>:<Documents materialOnly />}</>:tab==='rooms'?<RoomIssues />:tab==='write-offs'&&domains.writeOffAccess?<ReusableInventory key={branch} branch={branch} initialTab="write-offs"/>:tab==='replacements'&&domains.replacementAccess?<ReplacementQueue key={branch} branch={branch}/>:<>
      <div className="toolbar"><label>Tìm yêu cầu<input value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}} placeholder="Tên yêu cầu…"/></label><button aria-expanded={filters} onClick={()=>setFilters(!filters)}>Bộ lọc</button>{(allowed.data||[]).length>0&&<button className="primary" onClick={()=>{setCreate(allowed.data[0].code);setError('');}}>Tạo yêu cầu vật tư</button>}{can("IR_CREATE")&&!manager&&<button onClick={()=>{location.hash="requests?tab=ordinary&create=1";setTab("ordinary");setCreate("");}}>Tạo yêu cầu thông thường</button>}</div>
      <nav className="request-queues" aria-label="Trạng thái xử lý">{queues.map(([key,name])=><button key={key} className={`request-queue request-queue-${key}`} aria-pressed={queue===key} onClick={()=>{setQueue(key);setPage(1);}}>{name}</button>)}</nav>
      <p className="request-context" role="status">{queueHelp[queue]}</p>
      {filters&&<div className="toolbar"><label>Sắp xếp<select value={sort} onChange={e=>{setSort(e.target.value);setPage(1);}}><option value="latest">Mới nhất</option><option value="oldest">Cũ nhất</option><option value="title">Tên yêu cầu</option></select></label><button onClick={()=>{setSearch('');setQueue(defaultQueue);setSort('latest');setPage(1);}}>Đặt lại</button></div>}
      {error&&<AlertPopup tone="error">{error}</AlertPopup>}
      <State {...inbox} retry={()=>bump(n=>n+1)}>{pageRows.length?<table><thead><tr><th>Yêu cầu</th><th>Loại</th><th>Trạng thái</th><th>Người / bộ phận xử lý</th><th>Cập nhật</th></tr></thead><tbody>{pageRows.map((r:Row)=><tr className={`request-row request-row-${requestTone(r.status)}`} key={r.requestKey||`request:${r.workId}`}><td data-label="Yêu cầu"><button className="request-title" onClick={()=>open(r)}>{r.title}</button></td><td data-label="Loại">{types[r.requestType]||r.requestType}</td><td data-label="Trạng thái"><span className={`badge request-status request-status-${requestTone(r.status)}`}>{textStatus(r.status)}</span></td><td data-label="Xử lý">{roles[r.queueRole]||'—'}{r.handlerUserId?` · ${r.handlerName || 'Nhân viên '+r.handlerUserId}`:''}</td><td data-label="Cập nhật">{timestamp(r.updatedAt)}</td></tr>)}</tbody></table>:<Empty>Chưa có yêu cầu trong phạm vi đã chọn.</Empty>}
      <div className="toolbar"><span>{ordered.length} yêu cầu · Trang {page}/{Math.max(1,Math.ceil((ordered.length)/25))}</span><button disabled={page<=1||list.loading} onClick={()=>setPage(page-1)}>Trước</button><button disabled={page*25>=(ordered.length)||list.loading} onClick={()=>setPage(page+1)}>Tiếp</button></div></State>
    </>}
    {missingAsset&&domains.replacementAccess&&<RoomIssues key={`${branch}:missing:${missingAsset}`} detailOnly selectedId={missingAsset} onClose={()=>setMissingAsset(null)} onChanged={()=>bump(n=>n+1)}/>}
    {replacement&&domains.replacementAccess&&<Dialog title="Đề xuất thay tài sản" close={()=>setReplacement(null)}><ReplacementPanel key={`${branch}:${replacement}`} id={replacement} onSaved={()=>bump(n=>n+1)}/></Dialog>}
    {create&&tab==='inbox'&&requestAccess&&<CreateRequest key={create} type={create} choices={allowed.data||[]} allocationId={Number(params.get('allocation'))||0} change={setCreate} close={()=>setCreate('')} saved={workId=>{setCreate('');setId(workId);bump(n=>n+1);}}/>}
    {!!id&&tab==='inbox'&&roomDomain&&<div className="request-room-workflow"><RoomIssues key={`${branch}:${roomRoot}`} detailOnly selectedId={roomRoot} onClose={()=>setId(0)} onChanged={()=>bump(n=>n+1)}/></div>}
    {!!id&&tab==='inbox'&&!roomDomain&&<Dialog title={data?.request?.title||'Chi tiết yêu cầu'} className="request-dialog" close={()=>!pending&&setId(0)}><State {...detail} retry={()=>bump(n=>n+1)}>{data&&<>
      <p><span className={`badge request-status request-status-${requestTone(data.request.status)}`}>{textStatus(data.request.status)}</span></p><p className="request-meta">{roles[data.request.queueRole]||'Đã kết thúc xử lý'}{data.request.handlerUserId?` · ${data.request.handlerName || 'Nhân viên '+data.request.handlerUserId}`:''}</p>{data.description&&<p>{data.description}</p>}
      {data.parentRequestWorkId&&<a href={`#requests?id=${data.parentRequestWorkId}`}>Yêu cầu liên quan</a>}
      {!data.request.structured&&<a href={data.request.requestType==='INCIDENT'?`#work?kind=INCIDENT&id=${id}`:`#requests?tab=ordinary&id=${id}&legacy=1`}>Mở chi tiết yêu cầu</a>}
      {data.actions.some((a:string)=>["claim","issue"].includes(a))&&<p className="request-context">Nhận xử lý ghi nhận bạn là người phụ trách; chưa xuất kho hoặc tăng vật tư đang giữ. Xác nhận cấp phát mới chuyển vật tư từ kho cho nhân viên.</p>}
      <div className="request-actions">{data.actions.filter((a:string)=>!roomDomain||['claim','release'].includes(a)).map((a:string)=><button key={a} className={['claim','issue','approve','receive'].includes(a)?'primary':a==='reject'?'danger':undefined} disabled={pending||detail.loading} onClick={()=>openAction(a)}>{actions[a]||a}</button>)}</div>
      <div className="request-lines">{data.items.map((r:Row)=><div className="panel" key={r.itemId}><strong>{r.name}</strong><p>{data.request.requestType === "CONSUMABLE_REFILL_REQUEST" ? `Đề nghị: ${r.requestedQuantity} · Đã cấp: ${r.receivedQuantity} · Chưa cấp: ${Math.max(0,r.requestedQuantity-r.receivedQuantity)}` : `Đề nghị: ${r.requestedQuantity} · Được duyệt: ${r.approvedQuantity??'Chưa duyệt'} · Đã nhận: ${r.receivedQuantity}`}</p>{r.currentStock!==null&&<p>Tồn kho hiện tại: {r.currentStock}</p>}</div>)}</div>
      {data.request.requestType==="CONSUMABLE_REFILL_REQUEST"&&data.request.status==="FULFILLED"&&data.items.some((r:Row)=>r.receivedQuantity<r.requestedQuantity)&&<p>Lịch sử: yêu cầu này từng được cấp một phần theo quy trình cũ. Số đã cấp được giữ nguyên; yêu cầu mới phải được cấp đủ hoặc từ chối.</p>}
      {!data.actions.length&&<p>{roomDomain?'Xem kết quả và thao tác được phép trong quy trình phòng.':'Chỉ xem tại bước này.'}</p>}
      <h3>Lịch sử xử lý</h3><ol className="request-timeline">{data.history.map((h:Row)=><li key={h.eventId}><strong>{textStatus(h.newStatus)}</strong><small>{timestamp(h.at)} · {roles[h.actorRole]||h.actorRole} · Nhân viên {h.actorUserId}</small>{h.newQueue&&<p>Chuyển đến: {roles[h.newQueue]||h.newQueue}</p>}{h.reason&&<p>{h.reason}</p>}</li>)}</ol>
    </>}</State></Dialog>}
    {command&&data&&<Dialog className="confirmation-popup" title={actions[command]||command} close={()=>!pending&&setCommand('')}><form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act({reason:f.get('reason')||null,...(command==='receive'?{note:f.get('reason')||null,supplierName:f.get('supplierName')||null,items:data.items.map((r:Row)=>({itemId:r.itemId,quantity:Number(f.get(`qty-${r.itemId}`))})).filter((r:Row)=>r.quantity>0)}:['approve','issue'].includes(command)?{approvedItems:data.items.map((r:Row)=>({itemId:r.itemId,quantity:refillIssue?r.requestedQuantity:Number(f.get(`qty-${r.itemId}`))}))}:{})});}}>
      {command==='claim'&&<p>Nhận trách nhiệm xử lý yêu cầu. Thao tác này chưa cấp vật tư và không thay đổi tồn kho.</p>}
      {command==='approve'&&<p>Xác nhận số lượng cần xử lý. Thao tác này chưa làm tăng tồn kho.</p>}
      {command==='issue'&&<p>Xác nhận cấp đủ tất cả vật tư đã đề nghị. Nếu thiếu bất kỳ mặt hàng nào, đóng hộp xác nhận và chọn Từ chối. Kho và số dư nhân viên chỉ thay đổi khi cấp đủ thành công.</p>}
      {insufficientRefill&&<p role="alert">Kho không đủ toàn bộ vật tư đã đề nghị. Không thể cấp một phần; hãy từ chối và ghi rõ vật tư thiếu.</p>}
      {command==='receive'&&<><p>Chỉ nhập số lượng hàng đã nhận thực tế. Để 0 với mặt hàng chưa nhận.</p><label>Nguồn giao hàng<input name="supplierName" maxLength={250}/></label></>}
      {['approve','receive','issue'].includes(command)&&data.items.map((r:Row)=>{if(refillIssue)return <div className="request-line" key={r.itemId}><strong>{r.name}</strong><span>Số lượng cấp: {r.requestedQuantity}</span>{typeof r.currentStock==='number'&&<span>Tồn kho: {r.currentStock}{r.currentStock<r.requestedQuantity?' · Không đủ':''}</span>}</div>;const max=command==='receive'?(r.approvedQuantity??r.requestedQuantity)-r.receivedQuantity:command==='issue'?(r.approvedQuantity??r.requestedQuantity):r.requestedQuantity;return <label className="request-line" key={r.itemId}>{r.name}<input name={`qty-${r.itemId}`} type="number" min={command==='approve'?0.0001:0} max={max} step={command==='issue'?1:0.0001} required defaultValue={command==='receive'?0:command==='issue'?Math.min(max,r.currentStock??max):max}/></label>;})}
      <label>{command==='reject'?'Lý do từ chối':'Ghi chú'}<textarea name="reason" required={command==='reject'} minLength={command==='reject'?10:undefined} maxLength={2000}/></label>
      {error&&<AlertPopup tone="error">{error}</AlertPopup>}<ConfirmationActions pending={pending} disabled={insufficientRefill} cancel={()=>setCommand('')}/>
    </form></Dialog>}
    </PageTitleTarget.Provider>
  </div>;
}

function CreateRequest({type,choices,change,close,saved}:{type:string;choices:Row[];allocationId:number;change:(v:string)=>void;close:()=>void;saved:(id:number)=>void}){
  const {branch}=useAuth();const refill=type==='CONSUMABLE_REFILL_REQUEST';
  const [warehouse,setWarehouse]=useState(''),[pending,setPending]=useState(false),[error,setError]=useState(''),[key]=useState(()=>crypto.randomUUID());
  const [selected,setSelected]=useState<Record<number,boolean>>({});
  const [quantities,setQuantities]=useState<Record<number,string>>({});
  const [revision,refresh]=useState(0);
  useEffect(()=>{const reload=()=>refresh(n=>n+1);const timer=window.setInterval(()=>{if(!pending&&document.visibilityState==='visible')reload();},30000);window.addEventListener('focus',reload);window.addEventListener('compact:shift-required',reload);return()=>{window.clearInterval(timer);window.removeEventListener('focus',reload);window.removeEventListener('compact:shift-required',reload);};},[pending]);
  const source=useQuery(refill?`requests/refill-options?branchId=${branch}`:`workspace/options?branchId=${branch}`,revision,false);
  const lines:Row[]=source.data?.items||[];
  async function submit(f:FormData){
    if(pending||source.loading||source.error||!source.data)return;
    const items=lines.filter(r=>selected[r.itemId]).map(r=>({itemId:r.itemId,quantity:Number(quantities[r.itemId])}));
    if(!items.length||items.some(r=>!Number.isFinite(r.quantity)||r.quantity<=0||refill&&!Number.isInteger(r.quantity))){setError('Chọn ít nhất một vật tư và nhập số lượng lớn hơn 0.');return;}
    setPending(true);setError('');
    try{const result=await api(`requests/${refill?'refills':'replenishments'}`,{operationKey:key,reason:f.get('reason'),...(refill?{allocationId:source.data.allocationId}:{branchId:branch,warehouseId:Number(warehouse)}),items});saved(result.request?.workId??result.workId);}
    catch(e){setError(requestError(e));refresh(n=>n+1);}finally{setPending(false);}
  }
  return <Dialog className="request-dialog request-create-dialog" title={refill?'Yêu cầu bổ sung vật tư giữa ca':'Yêu cầu bổ sung hàng cho kho'} close={()=>!pending&&close()}><form onSubmit={e=>{e.preventDefault();void submit(new FormData(e.currentTarget));}}>
    {choices.length>0&&<label>Loại yêu cầu<select disabled={pending} value={type} onChange={e=>change(e.target.value)}>{choices.map(r=><option key={r.code} value={r.code}>{r.name}</option>)}</select></label>}
    <section className="panel" aria-label="Tuyến xử lý yêu cầu"><p><strong>Được tạo:</strong> {refill?'Nhân viên buồng phòng có ca hoạt động và bộ vật tư hợp lệ.':'Thủ kho có quyền bổ sung hàng và ca hoạt động.'}</p><p><strong>Nơi nhận:</strong> {refill?'Thủ kho cùng chi nhánh.':'Quản lý vận hành duyệt, sau đó thủ kho ghi nhận hàng thực nhận.'}</p><p><strong>Cần nhập:</strong> {refill?'Vật tư trong bộ ca, số lượng nguyên dương, lý do ít nhất 10 ký tự.':'Kho nhận, vật tư, số lượng dương, lý do ít nhất 10 ký tự.'}</p></section>
    <State {...source}>
      {refill?source.data&&<p>Ca hiện tại: <strong>{source.data.shiftName}</strong> · Bắt đầu {timestamp(source.data.startedAt)}</p>:<label>Kho nhận<select disabled={pending} required value={warehouse} onChange={e=>setWarehouse(e.target.value)}><option value="">Chọn kho</option>{(source.data?.warehouses||[]).map((r:Row)=><option key={r.warehouseId} value={r.warehouseId}>{r.name}</option>)}</select></label>}
      <p>{refill?'Chọn vật tư tiêu hao hoặc tái sử dụng và số lượng cần lấy. Thủ kho cấp đủ toàn bộ yêu cầu hoặc từ chối nếu thiếu hàng; không cần quản lý vận hành duyệt.':'Chọn vật tư và số lượng cần bổ sung vào kho. Quản lý vận hành duyệt trước khi thủ kho ghi nhận hàng thực nhận.'}</p>
      <fieldset className="request-item-picker" disabled={pending}>
        <legend>Vật tư cần lấy <span>· Đã chọn {lines.filter(r=>selected[r.itemId]).length}</span></legend>
        {lines.map(r=><div className={`request-picker-row${selected[r.itemId]?' is-selected':''}`} key={r.itemId}>
          <label className="request-item-choice"><input type="checkbox" aria-label={`Chọn ${r.name}`} checked={!!selected[r.itemId]} onChange={e=>setSelected(old=>({...old,[r.itemId]:e.target.checked}))}/><span><strong>{r.name}</strong><small>{r.unit||'Đơn vị cơ sở'}</small></span></label>
          <label className="request-item-quantity">Số lượng<input aria-label={`Số lượng ${r.name}`} type="number" min={refill?1:0.0001} max="999999999" step={refill?1:0.0001} disabled={!selected[r.itemId]||pending} required={!!selected[r.itemId]} placeholder="0" value={quantities[r.itemId]??''} onChange={e=>setQuantities(old=>({...old,[r.itemId]:e.target.value}))}/></label>
        </div>)}
      </fieldset>
      {!lines.length&&<Empty>Chưa có vật tư phù hợp để gửi yêu cầu.</Empty>}
      <label>Lý do yêu cầu<textarea disabled={pending} name="reason" required minLength={10} maxLength={2000}/></label>
      {error&&<AlertPopup tone="error">{error}</AlertPopup>}
      <button className="primary" disabled={pending||source.loading||!!source.error||!choices.some(r=>r.code===type)||!lines.some(r=>selected[r.itemId])}>{pending?'Đang gửi…':'Gửi yêu cầu'}</button>
    </State></form></Dialog>;
}
