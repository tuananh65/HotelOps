import { usePagedRows } from "./use-paged-rows";
import { useEffect, useRef, useState } from "react";
import { api, ApiError, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { ConfirmationActions, Dialog, Empty, State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";
import PageTitle, { PageTitleTarget } from "./page-title";
import Work from "./work";
import Documents from "./documents";
import RoomIssues from "./room-issues";
import ReusableInventory from "./reusable-inventory";
import ReplacementQueue, { ReplacementPanel } from "./asset-replacement";
import { useDomainRequests } from "./use-domain-requests";
import "./requests.css";

const types:Record<string,string>={CONSUMABLE_REFILL_REQUEST:"Bổ sung tiêu hao",WAREHOUSE_REPLENISHMENT_REQUEST:"Bổ sung hàng cho kho",GENERAL_INTERNAL_REQUEST:"Yêu cầu nội bộ",INCIDENT:"Sự cố phòng"};
const statuses:Record<string,string>={PENDING_SUPERVISOR:"Chờ giám sát xác nhận",PENDING_STOREKEEPER:"Chờ kho cấp phát",PENDING_OPERATIONS_MANAGER:"Chờ quản lý vận hành xác nhận",PENDING_RECEIPT:"Chờ nhận hàng thực tế",FULFILLED:"Đã cấp phát",COMPLETED:"Đã hoàn thành",REJECTED:"Đã từ chối",CANCELLED:"Đã hủy"};
const actions:Record<string,string>={claim:"Nhận xử lý",release:"Trả về hàng đợi",approve:"Xác nhận yêu cầu",reject:"Từ chối",issue:"Xác nhận cấp phát",cancel:"Hủy yêu cầu",receive:"Ghi nhận hàng thực nhận"};
const roles:Record<string,string>={HOUSEKEEPING_SUPERVISOR:"Giám sát buồng phòng",STOREKEEPER:"Thủ kho",OPERATION_MANAGER:"Quản lý vận hành",HK_STAFF:"Nhân viên buồng phòng",MAINTENANCE_SUPERVISOR:"Trưởng đội bảo trì",RECEPTION:"Lễ tân"};
const queues=[['all','Tất cả trong quyền'],['mine','Tôi đã gửi'],['action','Cần tôi xử lý'],['progress','Đang xử lý'],['completed','Hoàn thành'],['rejected','Từ chối']];
const roomTypes:Record<string,string>={ROOM_DURABLE_ITEM_MISSING:"Báo thiếu đồ dùng",ROOM_EQUIPMENT_DAMAGE:"Báo thiết bị hỏng",DURABLE_ITEM_REMEDIATION:"Bổ sung đồ dùng cho phòng",MAINTENANCE_REMEDIATION:"Khắc phục thiết bị",ROOM_REINSPECTION:"Kiểm tra lại phòng"};
Object.assign(types,roomTypes);
Object.assign(types,{REUSABLE_WRITE_OFF:"Ghi giảm vật tư tái sử dụng",ASSET_REPLACEMENT:"Thay mới tài sản"});
Object.assign(statuses,{APPROVED:"Đã ghi giảm",REPLACEMENT_CONFIRMED:"Chờ OM xử lý",REPLACEMENT_APPROVED:"Đã đồng ý thay mới",REPLACEMENT_REJECTED:"Đã từ chối",REPLACEMENT_INSTALLED:"Đã thay thực tế",REPLACEMENT_PROPOSED:"Chờ giám sát xác nhận"});
Object.assign(statuses,{ACCEPTED:"Đã tiếp nhận",PENDING_MAINTENANCE_SUPERVISOR:"Chờ trưởng đội bảo trì",PENDING_REINSPECTION:"Chờ kiểm tra lại",REWORK_REQUIRED:"Cần khắc phục lại"});
const textStatus=(value:string)=>statuses[value]||({OPEN:"Mới",IN_PROGRESS:"Đang xử lý",CLOSED:"Đã đóng",PENDING:"Chờ xử lý"} as Record<string,string>)[value]||value;
function requestError(error:unknown){const labels:Record<string,string>={REQUEST_ALREADY_CLAIMED:"Yêu cầu đã được người khác tiếp nhận. Tải lại để xem người đang xử lý.",ACTIVE_SHIFT_REQUIRED:"Ca đã hết giờ hoặc chưa bắt đầu; không thể cấp thêm vật tư vào ca này.",STOCK_INSUFFICIENT:"Kho không đủ số lượng để cấp phát.",INVALID_REQUEST_QUANTITIES:"Chọn ít nhất một vật tư với số lượng lớn hơn 0.",IDEMPOTENCY_KEY_REUSED:"Nội dung lần gửi lại đã thay đổi. Đóng và mở lại biểu mẫu trước khi gửi.",REQUEST_READ_ONLY:"Bạn chỉ được xem yêu cầu này.",USE_REQUEST_WORKFLOW:"Mở yêu cầu trong hộp yêu cầu để xử lý đúng quy trình."};return error instanceof ApiError&&labels[error.code]?labels[error.code]:message(error);}

export default function Requests(){
  const {branch,can,profile}=useAuth();
  const manager=profile.roles.some(r=>r.code==='OPERATION_MANAGER'&&(r.branchId===branch||r.branchId===null));
  const params=new URLSearchParams(location.hash.split('?')[1]);
  const [tab,setTab]=useState(params.get('tab')||'inbox'),[queue,setQueue]=useState('all'),[search,setSearch]=useState(''),[filters,setFilters]=useState(false),[page,setPage]=useState(1),[revision,bump]=useState(0);
  const [id,setId]=useState(Number(params.get('id'))||0),[create,setCreate]=useState(params.get('create')||''),[command,setCommand]=useState(''),[error,setError]=useState(''),[pending,setPending]=useState(false);
  const [sort,setSort]=useState('latest');
  const list=usePagedRows(`requests?branchId=${branch}&queue=${queue}&search=${encodeURIComponent(search)}`,revision);
  const domains=useDomainRequests(revision,queue,search);
  const [replacement,setReplacement]=useState<number|null>(null);
  const previousBranch=useRef(branch);
  useEffect(()=>{if(previousBranch.current===branch)return;previousBranch.current=branch;setId(0);setReplacement(null);setCommand('');setCreate('');setError('');setPage(1);},[branch]);
  const inbox={loading:list.loading||domains.loading,error:list.error||domains.error};
  const ordered=[...(list.data||[]),...domains.data].sort((a,b)=>sort==='title'?a.title.localeCompare(b.title,'vi'):(sort==='oldest'?1:-1)*(new Date(a.updatedAt).getTime()-new Date(b.updatedAt).getTime()));
  const pageRows=ordered.slice((page-1)*25,page*25);
  const allowed=useQuery(`requests/types?branchId=${branch}`,revision);
  const detail=useQuery(id?`requests/${id}`:null,revision);
  const [operationKey,setKey]=useState(()=>crypto.randomUUID());
  const data=detail.data;
  const roomDomain=!!data&&!!roomTypes[data.request.requestType];
  const roomRoot=roomDomain&&data.parentRequestWorkId?data.parentRequestWorkId:id;
  function openAction(value:string){setCommand(value);setKey(crypto.randomUUID());setError('');}
  async function act(body:Row){
    if(pending)return;setPending(true);setError('');
    try{await api(`requests/${id}/${command==='receive'?'receive':`actions/${command}`}`,{rowVersion:data.request.rowVersion,operationKey,...body});setCommand('');bump(n=>n+1);}
    catch(e){setError(requestError(e));bump(n=>n+1);}finally{setPending(false);}
  }
  function open(row:Row){
    if(row.domain==='replacement'){setReplacement(row.workId);return;}
    if(row.domain==='write-offs'){setTab('write-offs');return;}
    if(row.structured){setId(row.workId);return;}
    location.hash=row.requestType==='INCIDENT'?`work?kind=INCIDENT&id=${row.workId}`:`requests?tab=ordinary&id=${row.workId}&legacy=1`;
  }
  return <div className="request-workspace">
    <PageTitle><h1>Yêu cầu nội bộ</h1><p>Theo dõi người xử lý, cấp phát và kết quả trong cùng một nơi</p></PageTitle>
    <nav className="request-tabs" aria-label="Nhóm yêu cầu">{[['inbox','Hộp yêu cầu'],['ordinary','Yêu cầu thông thường'],...(can('MR_VIEW')?[['materials','Vật tư & chứng từ']]:[]),...(can('INCIDENT_VIEW')||can('IR_CREATE')?[['rooms','Sự cố phòng']]:[]),...(domains.writeOffAccess?[['write-offs','Ghi giảm vật tư']]:[]),...(domains.replacementAccess?[['replacements','Thay tài sản']]:[])].map(([key,name])=><button key={key} aria-pressed={tab===key} onClick={()=>{setTab(key);setId(0);setCreate('');bump(n=>n+1);}}>{name}</button>)}</nav>
    <PageTitleTarget.Provider value={null}>
    {tab==='ordinary'?<Work onlyKind="INTERNAL_REQUEST" personal={!manager} />:tab==='materials'?<Documents materialOnly />:tab==='rooms'?<RoomIssues />:tab==='write-offs'&&domains.writeOffAccess?<ReusableInventory key={branch} branch={branch} initialTab="write-offs"/>:tab==='replacements'&&domains.replacementAccess?<ReplacementQueue key={branch} branch={branch}/>:<>
      <div className="toolbar"><label>Tìm yêu cầu<input value={search} onChange={e=>{setSearch(e.target.value);setPage(1);}} placeholder="Tên yêu cầu…"/></label><button aria-expanded={filters} onClick={()=>setFilters(!filters)}>Bộ lọc</button>{(allowed.data||[]).length>0&&<button className="primary" onClick={()=>{setCreate(allowed.data[0].code);setError('');}}>Tạo yêu cầu</button>}</div>
      {filters&&<div className="toolbar"><label>Hàng đợi<select value={queue} onChange={e=>{setQueue(e.target.value);setPage(1);}}>{queues.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select></label><label>Sắp xếp<select value={sort} onChange={e=>{setSort(e.target.value);setPage(1);}}><option value="latest">Mới nhất</option><option value="oldest">Cũ nhất</option><option value="title">Tên yêu cầu</option></select></label><button onClick={()=>{setSearch('');setQueue('all');setSort('latest');setPage(1);}}>Đặt lại</button></div>}
      {error&&<AlertPopup>{error}</AlertPopup>}
      <State {...inbox} retry={()=>bump(n=>n+1)}>{pageRows.length?<table><thead><tr><th>Yêu cầu</th><th>Loại</th><th>Trạng thái</th><th>Người / bộ phận xử lý</th><th>Cập nhật</th></tr></thead><tbody>{pageRows.map((r:Row)=><tr key={r.requestKey||`request:${r.workId}`}><td data-label="Yêu cầu"><button className="request-title" onClick={()=>open(r)}>{r.title}</button></td><td data-label="Loại">{types[r.requestType]||r.requestType}</td><td data-label="Trạng thái"><span className="badge">{textStatus(r.status)}</span></td><td data-label="Xử lý">{roles[r.queueRole]||'—'}{r.handlerUserId?` · ${r.handlerName || 'Nhân viên '+r.handlerUserId}`:''}</td><td data-label="Cập nhật">{timestamp(r.updatedAt)}</td></tr>)}</tbody></table>:<Empty>Chưa có yêu cầu trong phạm vi đã chọn.</Empty>}
      <div className="toolbar"><span>{ordered.length} yêu cầu · Trang {page}/{Math.max(1,Math.ceil((ordered.length)/25))}</span><button disabled={page<=1||list.loading} onClick={()=>setPage(page-1)}>Trước</button><button disabled={page*25>=(ordered.length)||list.loading} onClick={()=>setPage(page+1)}>Tiếp</button></div></State>
    </>}
    {replacement&&domains.replacementAccess&&<Dialog title="Đề xuất thay tài sản" close={()=>setReplacement(null)}><ReplacementPanel key={`${branch}:${replacement}`} id={replacement} onSaved={()=>bump(n=>n+1)}/></Dialog>}
    {create&&<CreateRequest key={create} type={create} choices={allowed.data||[]} allocationId={Number(params.get('allocation'))||0} change={setCreate} close={()=>setCreate('')} saved={workId=>{setCreate('');setId(workId);bump(n=>n+1);}}/>}
    {!!id&&tab==='inbox'&&<Dialog title={data?.request?.title||'Chi tiết yêu cầu'} className="room-drawer" close={()=>!pending&&setId(0)}><State {...detail} retry={()=>bump(n=>n+1)}>{data&&<>
      <p><span className="badge">{textStatus(data.request.status)}</span></p><p className="request-meta">{roles[data.request.queueRole]||'Đã kết thúc xử lý'}{data.request.handlerUserId?` · ${data.request.handlerName || 'Nhân viên '+data.request.handlerUserId}`:''}</p>{data.description&&<p>{data.description}</p>}
      {data.parentRequestWorkId&&<a href={`#requests?id=${data.parentRequestWorkId}`}>Yêu cầu liên quan</a>}
      {!data.request.structured&&<a href={data.request.requestType==='INCIDENT'?`#work?kind=INCIDENT&id=${id}`:`#requests?tab=ordinary&id=${id}&legacy=1`}>Mở chi tiết yêu cầu</a>}
      {roomDomain&&<p><a className="button primary" href={`#room-issues?id=${roomRoot}`}>Mở quy trình khắc phục và kiểm tra phòng</a></p>}
      <div className="request-lines">{data.items.map((r:Row)=><div className="panel" key={r.itemId}><strong>{r.name}</strong><p>Đề nghị: {r.requestedQuantity} · Được duyệt: {r.approvedQuantity??'Chưa duyệt'} · {data.request.requestType === "CONSUMABLE_REFILL_REQUEST" ? (data.request.status === "FULFILLED" ? "Đã cấp phát" : "Chưa cấp phát") : `Đã nhận: ${r.receivedQuantity}`}</p>{r.currentStock!==null&&<p>Tồn kho hiện tại: {r.currentStock}</p>}</div>)}</div>
      <div className="request-actions">{data.actions.filter((a:string)=>!roomDomain||['claim','release'].includes(a)).map((a:string)=><button key={a} disabled={pending} onClick={()=>openAction(a)}>{actions[a]||a}</button>)}</div>
      {!data.actions.length&&<p>{roomDomain?'Xem kết quả và thao tác được phép trong quy trình phòng.':'Chỉ xem tại bước này.'}</p>}
      <h3>Lịch sử xử lý</h3><ol className="request-timeline">{data.history.map((h:Row)=><li key={h.eventId}><strong>{textStatus(h.newStatus)}</strong><small>{timestamp(h.at)} · {roles[h.actorRole]||h.actorRole} · Nhân viên {h.actorUserId}</small>{h.newQueue&&<p>Chuyển đến: {roles[h.newQueue]||h.newQueue}</p>}{h.reason&&<p>{h.reason}</p>}</li>)}</ol>
    </>}</State></Dialog>}
    {command&&data&&<Dialog className="confirmation-popup" title={actions[command]||command} close={()=>!pending&&setCommand('')}><form onSubmit={e=>{e.preventDefault();const f=new FormData(e.currentTarget);act({reason:f.get('reason')||null,...(command==='receive'?{note:f.get('reason')||null,supplierName:f.get('supplierName')||null,items:data.items.map((r:Row)=>({itemId:r.itemId,quantity:Number(f.get(`qty-${r.itemId}`))})).filter((r:Row)=>r.quantity>0)}:command==='approve'?{approvedItems:data.items.map((r:Row)=>({itemId:r.itemId,quantity:Number(f.get(`qty-${r.itemId}`))}))}:{})});}}>
      {command==='approve'&&<p>Xác nhận số lượng cần xử lý. Thao tác này chưa làm tăng tồn kho.</p>}
      {command==='receive'&&<><p>Chỉ nhập số lượng hàng đã nhận thực tế. Để 0 với mặt hàng chưa nhận.</p><label>Nguồn giao hàng<input name="supplierName" maxLength={250}/></label></>}
      {['approve','receive'].includes(command)&&data.items.map((r:Row)=>{const max=command==='receive'?(r.approvedQuantity??r.requestedQuantity)-r.receivedQuantity:r.requestedQuantity;return <label className="request-line" key={r.itemId}>{r.name}<input name={`qty-${r.itemId}`} type="number" min={command==='receive'?0:0.0001} max={max} step="0.0001" required defaultValue={command==='receive'?0:max}/></label>;})}
      <label>{command==='reject'?'Lý do từ chối':'Ghi chú'}<textarea name="reason" required={command==='reject'} minLength={command==='reject'?10:undefined} maxLength={2000}/></label>
      {error&&<AlertPopup>{error}</AlertPopup>}<ConfirmationActions pending={pending} cancel={()=>setCommand('')}/>
    </form></Dialog>}
    </PageTitleTarget.Provider>
  </div>;
}

function CreateRequest({type,choices,change,close,saved}:{type:string;choices:Row[];allocationId:number;change:(v:string)=>void;close:()=>void;saved:(id:number)=>void}){
  const {branch}=useAuth();const refill=type==='CONSUMABLE_REFILL_REQUEST';
  const [warehouse,setWarehouse]=useState(''),[pending,setPending]=useState(false),[error,setError]=useState(''),[key]=useState(()=>crypto.randomUUID());
  const [selected,setSelected]=useState<Record<number,boolean>>({});
  const [quantities,setQuantities]=useState<Record<number,string>>({});
  const source=useQuery(refill?`requests/refill-options?branchId=${branch}`:`workspace/options?branchId=${branch}`,0,false);
  const lines:Row[]=source.data?.items||[];
  async function submit(f:FormData){
    if(pending)return;
    const items=lines.filter(r=>selected[r.itemId]).map(r=>({itemId:r.itemId,quantity:Number(quantities[r.itemId])}));
    if(!items.length||items.some(r=>!Number.isFinite(r.quantity)||r.quantity<=0)){setError('Chọn ít nhất một vật tư và nhập số lượng lớn hơn 0.');return;}
    setPending(true);setError('');
    try{const result=await api(`requests/${refill?'refills':'replenishments'}`,{operationKey:key,reason:f.get('reason'),...(refill?{allocationId:source.data.allocationId}:{branchId:branch,warehouseId:Number(warehouse)}),items});saved(result.request?.workId??result.workId);}
    catch(e){setError(requestError(e));}finally{setPending(false);}
  }
  return <Dialog title={refill?'Yêu cầu vật tư tiêu hao':'Tạo yêu cầu nội bộ'} close={()=>!pending&&close()}><form onSubmit={e=>{e.preventDefault();void submit(new FormData(e.currentTarget));}}>
    {choices.length>1&&<label>Loại yêu cầu<select value={type} onChange={e=>change(e.target.value)}>{choices.map(r=><option key={r.code} value={r.code}>{r.name}</option>)}</select></label>}
    <State {...source}>
      {refill?source.data&&<p>Ca hiện tại: <strong>{source.data.shiftName}</strong> · Bắt đầu {timestamp(source.data.startedAt)}</p>:<label>Kho nhận<select required value={warehouse} onChange={e=>setWarehouse(e.target.value)}><option value="">Chọn kho</option>{(source.data?.warehouses||[]).map((r:Row)=><option key={r.warehouseId} value={r.warehouseId}>{r.name}</option>)}</select></label>}
      <p>Chọn vật tư cần lấy và số lượng. Quản lý vận hành duyệt trước khi thủ kho cấp phát.</p>
      <div className="table-scroll"><table><thead><tr><th>Chọn</th><th>Vật tư</th><th>Đơn vị</th><th>Số lượng cần lấy</th></tr></thead><tbody>{lines.map(r=><tr key={r.itemId}>
        <td><input type="checkbox" aria-label={`Chọn ${r.name}`} checked={!!selected[r.itemId]} onChange={e=>setSelected(old=>({...old,[r.itemId]:e.target.checked}))}/></td>
        <td>{r.name}</td><td>{r.unit||'Đơn vị cơ sở'}</td>
        <td><input aria-label={`Số lượng ${r.name}`} type="number" min="0.0001" max="999999999" step="0.0001" disabled={!selected[r.itemId]||pending} required={!!selected[r.itemId]} value={quantities[r.itemId]??''} onChange={e=>setQuantities(old=>({...old,[r.itemId]:e.target.value}))}/></td>
      </tr>)}</tbody></table></div>
      {!lines.length&&<Empty>Chưa có vật tư phù hợp để gửi yêu cầu.</Empty>}
      <label>Lý do yêu cầu<textarea name="reason" required minLength={10} maxLength={2000}/></label>
      {error&&<AlertPopup>{error}</AlertPopup>}
      <button className="primary" disabled={pending||!choices.some(r=>r.code===type)||!lines.some(r=>selected[r.itemId])}>{pending?'Đang gửi…':'Gửi yêu cầu'}</button>
    </State></form></Dialog>;
}
