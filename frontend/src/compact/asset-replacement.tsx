import { useState } from "react";
import { api, message, type Row } from "./api";
import { AlertPopup } from "./alert-popup";
import { State, useQuery, Empty, Dialog } from "./ui";
const labels:Record<string,string>={NONE:"Chưa có đề xuất",REPLACEMENT_PROPOSED:"Chờ giám sát xác nhận",REPLACEMENT_CONFIRMED:"Chờ OM xử lý",REPLACEMENT_APPROVED:"Đã đồng ý thay mới",REPLACEMENT_REJECTED:"Đề xuất bị từ chối",REPLACEMENT_INSTALLED:"Đã thay thực tế — cần kiểm tra kỹ thuật"};

export function ReplacementPanel({id,onSaved}:{id:number;onSaved?:()=>void}){
  const [revision,bump]=useState(0),[error,setError]=useState(""),[pending,setPending]=useState(false);
  const q=useQuery<Row>(`work/${id}/replacement`,revision);const d=q.data;
  return <section className="panel"><h3>Đề xuất thay mới thiết bị</h3><State {...q}>{d&&<>
    <p>{labels[d.state]||d.state}</p>
    {d.history.map((h:Row,index:number)=>{let snapshot:Row={};try{snapshot=JSON.parse(h.snapshotJson||"{}");}catch{/* Old history remains readable. */}
      return <div key={index}><strong>{labels[h.eventType]||h.eventType}</strong><p>{h.reason}</p>{snapshot.Plan&&<p>Phương án: {snapshot.Plan}</p>}{snapshot.ReplacementAssetId&&<p>Tài sản đã lắp: {snapshot.ReplacementAssetId}</p>}</div>;})}
    {error&&<AlertPopup>{error}</AlertPopup>}
    {(d.canPropose||d.canConfirm||d.canDecide||d.canInstall)&&<form onSubmit={async e=>{
      e.preventDefault();if(pending)return;const form=new FormData(e.currentTarget);const button=(e.nativeEvent as SubmitEvent).submitter as HTMLButtonElement|null;
      if(!button?.value)return;setPending(true);setError("");
      try{await api(`work/${id}/replacement/${button.value}`,{rowVersion:d.rowVersion,operationKey:crypto.randomUUID(),reason:form.get("reason"),plan:form.get("plan"),replacementAssetId:form.get("asset")?Number(form.get("asset")):null});bump(x=>x+1);onSaved?.();}
      catch(error){setError(message(error));}finally{setPending(false);}
    }}>
      <label>{d.canPropose?"Kết luận không thể sửa / lý do đề xuất":"Kết quả xử lý / lý do"}<textarea name="reason" required maxLength={2000} disabled={pending}/></label>
      {d.canPropose&&<label>Phương án thay mới<textarea name="plan" required maxLength={2000} disabled={pending}/></label>}
      {d.canInstall&&<><p>Chọn tài sản đã lắp tại vị trí cần thay. Người quản lý tài sản phải cập nhật vị trí thiết bị cũ và thiết bị thay trước khi xác nhận.</p>
        <label>Thiết bị thay<select name="asset" required defaultValue="" disabled={pending}><option value="">Chọn thiết bị đã lắp</option>{d.replacementAssets.map((a:Row)=><option key={a.assetId} value={a.assetId}>{a.name} · {a.assetCode}</option>)}</select></label></>}
      <div className="actions">
        {d.canPropose&&<button value="propose" disabled={pending}>Gửi giám sát xác nhận</button>}
        {d.canConfirm&&<button value="confirm" disabled={pending}>Xác nhận, gửi OM</button>}
        {d.canDecide&&<button value="approve" className="primary" disabled={pending}>Đồng ý thay mới</button>}
        {(d.canConfirm||d.canDecide)&&<button value="reject" className="danger" disabled={pending}>Từ chối</button>}
        {d.canInstall&&<button value="install" disabled={pending}>Ghi nhận đã thay thực tế</button>}
      </div>
    </form>}
  </>}</State></section>;
}

export default function ReplacementQueue({branch}:{branch:number}){
  const [id,setId]=useState<number|null>(()=>Number(new URLSearchParams(location.hash.split("?")[1]).get("replacement"))||null),[revision,bump]=useState(0);
  const q=useQuery<Row[]>(`work/replacements?branchId=${branch}`,revision);
  return <section className="panel"><h3>Đề xuất thay tài sản</h3><State {...q}>{q.data?.length?q.data.map(w=><button key={w.workId} onClick={()=>setId(w.workId)}>{w.title} · {labels[w.status]||w.status}</button>):<Empty>Chưa có đề xuất được giám sát xác nhận.</Empty>}</State>
    {id&&<Dialog title="Xử lý đề xuất thay tài sản" close={()=>setId(null)}><ReplacementPanel id={id} onSaved={()=>bump(x=>x+1)}/></Dialog>}
  </section>;
}
