import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { TimeLogFailure } from "./timeLogApi";
import { readCorrection, readCorrections, saveCorrection, type Correction, type CorrectionDetail } from "./timeCorrectionsApi";
import { hotelInput, utcInput } from "./CorrectionRequestForm";
import { correctionMessage } from "./correctionMessage";
import { useWorkTimeScope, WorkTimeBranchField } from "./useWorkTimeScope";
import "./work-time.css";
const stamp=(v:string)=>new Intl.DateTimeFormat("vi-VN",{timeZone:"Asia/Ho_Chi_Minh",dateStyle:"short",timeStyle:"medium"}).format(new Date(v));
export function TimeCorrectionsPage() {
  const [params]=useSearchParams();
  const requestedBranch=Number(params.get("branchId")), requestedId=Number(params.get("correctionId"));
  const scope=useWorkTimeScope("corrections",requestedBranch);
  const [busy,setBusy]=useState(false);
  return <main className="time-workspace">
    <header className="time-heading"><div><h1>Đề nghị điều chỉnh thời gian</h1><p>Theo dõi đề nghị đã gửi và kết quả xử lý. Thời gian theo giờ Việt Nam (UTC+7).</p></div><Link to="/my-time-log">Mở nhật ký để tạo đề nghị</Link></header>
    <WorkTimeBranchField scope={scope} disabled={busy}/>
    {scope.branch && scope.owner && <CorrectionsBody key={`${scope.owner}:${scope.branch.branchId}:${requestedId}`} branch={scope.branch.branchId} own={scope.branch.canReadOwnTime} departments={scope.branch.correctionDepartments} initialId={requestedBranch===scope.branch.branchId&&Number.isSafeInteger(requestedId)&&requestedId>0?requestedId:null} onBusyChange={setBusy}/>}
    <nav className="time-links"><Link to="/workday">Lịch ca &amp; ngày làm việc</Link><Link to="/home">Trang chủ</Link></nav>
  </main>;
}
function CorrectionsBody({branch,own,departments,initialId,onBusyChange}:{branch:number;own:boolean;departments:{id:number;name:string}[];initialId:number|null;onBusyChange:(busy:boolean)=>void}) {
  const {state,actions}=useAuth();const owner=state.authorityGeneration;
  const [department,setDepartment]=useState(own?"":String(departments[0]?.id??""));
  const [page,setPage]=useState(1),[id,setId]=useState(initialId),[revision,setRevision]=useState(0);
  const [result,setResult]=useState<{key:string;items?:Correction[];total?:number;detail?:CorrectionDetail;error?:string}|null>(null);
  const [start,setStart]=useState(""),[end,setEnd]=useState(""),[reason,setReason]=useState("");
  const [busy,setBusy]=useState(false),[locked,setLocked]=useState(false),[message,setMessage]=useState(""),[success,setSuccess]=useState(false);
  const active=useRef<AbortController|null>(null),saving=useRef(false);const key=`${owner}:${branch}:${department}:${page}:${id}:${revision}`;
  useEffect(()=>{onBusyChange(busy);return()=>onBusyChange(false);},[busy,onBusyChange]);
  useEffect(()=>{
    setResult(null);setLocked(false);setReason("");setMessage("");setSuccess(false);const c=new AbortController();active.current=c;
    void (async()=>{try{
      if(id){const detail=await readCorrection(branch,id,c.signal);if(!c.signal.aborted){setResult({key,detail});setStart(hotelInput(detail.correction.proposedStart));setEnd(hotelInput(detail.correction.proposedEnd));}}
      else {const data=await readCorrections(branch,department?Number(department):null,page,c.signal);if(!c.signal.aborted)setResult({key,...data});}
    }catch(e){if(!c.signal.aborted){if(e instanceof TimeLogFailure&&e.status===401)actions.invalidateSession(owner);setResult({key,error:e instanceof TimeLogFailure&&e.status===403?"Bạn không có quyền xem yêu cầu trong phạm vi này.":"Chưa tải được đề nghị điều chỉnh. Vui lòng thử lại."});}}})();
    return()=>{c.abort();active.current?.abort();};
  },[owner,key,branch,department,page,id,revision,actions]);
  const visible=result?.key===key?result:null,detail=visible?.detail,row=detail?.correction;
  async function decide(decision:"APPLY"|"REJECT") {
    if(!row?.canDecide||locked||saving.current)return;
    saving.current=true;setBusy(true);setMessage("");setSuccess(false);const c=new AbortController();active.current=c;
    try {await saveCorrection(branch,row.correctionId,row.version,{decision,start:decision==="APPLY"?utcInput(start):null,end:decision==="APPLY"?utcInput(end):null,reason:reason.trim()||null},true,c.signal);
      if(!c.signal.aborted){setLocked(true);setSuccess(true);setMessage("Đã lưu quyết định. Tải lại để xem kết quả mới nhất.");}}
    catch(e){if(!c.signal.aborted){if(e instanceof TimeLogFailure&&e.status===401)actions.invalidateSession(owner);setLocked(true);setMessage(correctionMessage(e));}}
    finally{saving.current=false;if(!c.signal.aborted)setBusy(false);}
  }
  return <>
    <section className="time-card time-toolbar" aria-label="Phạm vi đề nghị">
      {departments.length>0 ? <label>Phạm vi xem<select aria-label="Phạm vi xem" disabled={busy} value={department} onChange={e=>{setDepartment(e.target.value);setPage(1);setId(null);}}>{own&&<option value="">Đề nghị của tôi</option>}{departments.map(d=><option key={d.id} value={d.id}>Xét duyệt: {d.name}</option>)}</select></label> : <strong>Đề nghị của tôi</strong>}
      {id!==null&&<button disabled={busy} onClick={()=>setId(null)}>Quay lại danh sách</button>}
      <button disabled={busy} onClick={()=>setRevision(n=>n+1)}>Tải lại đề nghị</button>
    </section>
    {visible?.error&&<p role="alert">{visible.error}</p>}{!visible&&<LoadingIndicator label="Đang tải đề nghị điều chỉnh"/>}
    {visible?.items&&<>
      {visible.items.length===0?<div className="time-empty"><p>Chưa có đề nghị điều chỉnh trong phạm vi này.</p>{own&&!department&&<Link to="/my-time-log">Vào nhật ký thời gian, chọn khoảng đã kết thúc rồi bấm Đề nghị điều chỉnh.</Link>}</div>:<ul className="time-list">{visible.items.map(r=><li key={r.correctionId}><div className="time-toolbar"><button onClick={()=>setId(r.correctionId)}>Xem đề nghị #{r.correctionId}</button><span className="time-status">{displayCode(r.status)}</span></div>
        <p>{displayCode(r.kind)} · Gửi lúc {stamp(r.requestedAt)}</p><p>Đề xuất: {stamp(r.proposedStart)} – {stamp(r.proposedEnd)}</p><p>{r.reason}</p>
      </li>)}</ul>}
      <nav className="time-pagination"><button disabled={page===1} onClick={()=>setPage(p=>p-1)}>Trang trước</button><span>Trang {page} · {visible.total} đề nghị</span><button disabled={page*50>=(visible.total??0)} onClick={()=>setPage(p=>p+1)}>Trang sau</button></nav>
    </>}
    {row&&<section className="time-card"><h2>Đề nghị #{row.correctionId}</h2><p><span className="time-status">{displayCode(row.status)}</span> · {displayCode(row.kind)}</p>
      <div className="time-columns"><div><h3>Thời gian ban đầu</h3><p>{stamp(row.originalStart)} – {stamp(row.originalEnd)}</p></div><div><h3>Thời gian đề xuất</h3><p>{stamp(row.proposedStart)} – {stamp(row.proposedEnd)}</p></div></div>
      <h3>Lý do đề nghị</h3><p>{row.reason}</p>
      {row.appliedStart&&row.appliedEnd&&<p>Đã áp dụng: {stamp(row.appliedStart)} – {stamp(row.appliedEnd)}</p>}{row.decidedAt&&<p>Xử lý lúc {stamp(row.decidedAt)}{row.decisionReason?`: ${row.decisionReason}`:""}</p>}
      <h3>Các khoảng thời gian khác trong cùng ngày</h3>{detail?.otherIntervals.length===0?<p>Không có khoảng thời gian khác.</p>:<ul>{detail?.otherIntervals.map((i,n)=><li key={n}>{stamp(i.start)} – {i.end?stamp(i.end):"Đang thực hiện"}</li>)}</ul>}
      {row.canDecide&&<fieldset className="time-correction-form" disabled={busy||locked}><legend>Xét duyệt đề nghị</legend>
        <label>Bắt đầu được duyệt<input type="datetime-local" step="1" value={start} onChange={e=>setStart(e.target.value)}/></label><label>Kết thúc được duyệt<input type="datetime-local" step="1" value={end} onChange={e=>setEnd(e.target.value)}/></label>
        <label>Lý do quyết định (bắt buộc khi từ chối)<textarea maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}/></label>
        <div className="time-toolbar"><button disabled={!start||!end} onClick={()=>void decide("APPLY")}>Áp dụng điều chỉnh</button><button disabled={reason.trim().length<10} onClick={()=>void decide("REJECT")}>Từ chối</button></div>
      </fieldset>}
    </section>}
    {message&&<p className={success?"time-success":"time-empty"} role="status">{message}</p>}
  </>;
}
