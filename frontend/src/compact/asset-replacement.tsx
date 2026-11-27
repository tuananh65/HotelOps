import { useState } from "react";
import { api, message, type Row } from "./api";
import { AlertPopup } from "./alert-popup";
import { State, useQuery, Empty, Dialog } from "./ui";
const labels:Record<string,string>={NONE:"Chưa có đề xuất",REPLACEMENT_PROPOSED:"Chờ giám sát xác nhận",REPLACEMENT_CONFIRMED:"Chờ OM xử lý",REPLACEMENT_APPROVED:"Đã đồng ý thay mới",REPLACEMENT_REJECTED:"Đề xuất bị từ chối",REPLACEMENT_INSTALLED:"Đã thay mới — chờ giám sát buồng phòng kiểm tra"};

export function ReplacementPanel({id,onSaved,installation=false}:{id:number;onSaved?:()=>void;installation?:boolean}){
  const [revision,bump]=useState(0),[error,setError]=useState(""),[pending,setPending]=useState(false),[rejecting,setRejecting]=useState(false);
  const q=useQuery<Row>(`work/${id}/replacement`,revision);const d=q.data;
  return <section className="panel"><h3>Đề xuất thay mới thiết bị</h3><State {...q}>{d&&<>
    <p>{labels[d.state]||d.state}</p>
    {d.history.map((h:Row,index:number)=>{let snapshot:Row={};try{snapshot=JSON.parse(h.snapshotJson||"{}");}catch{/* Old history remains readable. */}
      return <div key={index}><strong>{labels[h.eventType]||h.eventType}</strong>{h.reason&&<p>{h.reason}</p>}{snapshot.Plan&&<p>Phương án: {snapshot.Plan}</p>}{snapshot.ReplacementAssetId&&<p>Đã ghi nhận tài sản thay thế.</p>}</div>;})}
    {error&&<AlertPopup>{error}</AlertPopup>}
    {(d.canPropose||d.canConfirm||d.canDecide||d.canInstall)&&<form onSubmit={async e=>{
      e.preventDefault();if(pending)return;const form=new FormData(e.currentTarget);const button=(e.nativeEvent as SubmitEvent).submitter as HTMLButtonElement|null;
      if(!button?.value)return;setPending(true);setError("");
      try{if(button.value==='reject'&&!String(form.get('reason')||'').trim()){setError('Nhập lý do từ chối.');return;}await api(`work/${id}/replacement/${button.value}`,{rowVersion:d.rowVersion,operationKey:crypto.randomUUID(),reason:form.get("reason"),plan:form.get("plan"),replacementAssetId:form.get("asset")?Number(form.get("asset")):null});bump(x=>x+1);onSaved?.();if(button.value==='approve')location.hash=`assets?replacement=${id}`;}
      catch(error){setError(message(error));}finally{setPending(false);}
    }}>
      {!d.canInstall&&(!d.canDecide||rejecting)&&<label>{d.canPropose?"Kết luận không thể sửa / lý do đề xuất":d.canDecide?"Lý do từ chối":"Kết quả xử lý / lý do"}<textarea name="reason" required maxLength={2000} disabled={pending}/></label>}
      {d.canPropose&&<label>Phương án thay mới<textarea name="plan" required maxLength={2000} disabled={pending}/></label>}
      {d.canInstall&&installation&&<p>Xác nhận đã thay thế đồ vật này? Tài sản sẽ trở về trạng thái bình thường và hệ thống gửi yêu cầu kiểm tra lại cho giám sát buồng phòng.</p>}
      <div className="actions">
        {d.canPropose&&<button value="propose" disabled={pending}>Gửi OM yêu cầu thay thế</button>}
        {d.canConfirm&&<button value="confirm" disabled={pending}>Xác nhận, gửi OM</button>}
        {d.canDecide&&!rejecting&&<button value="approve" className="primary" disabled={pending}>Đồng ý thay mới</button>}
        {d.canDecide&&!rejecting?<button type="button" className="danger" onClick={()=>setRejecting(true)} disabled={pending}>Từ chối</button>:(d.canConfirm||d.canDecide)&&<button value="reject" className="danger" disabled={pending}>Xác nhận từ chối</button>}
        {rejecting&&<button type="button" onClick={()=>setRejecting(false)} disabled={pending}>Quay lại</button>}
        {d.canInstall&&installation&&<button value="install" className="primary" disabled={pending}>Xác nhận đã thay và gửi kiểm tra</button>}
        {d.canInstall&&!installation&&<a className="button-link" href={`#assets?replacement=${id}`}>Mở tài sản để thay mới</a>}
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
