import { useState } from "react";
import { api, message, type Row } from "./api";
import { ConfirmationActions, Dialog } from "./ui";
import { AlertPopup } from "./alert-popup";
import RoomIssues from "./room-issues";

export default function CleaningIncidents({work,issues,canConfirm,saved}:{work:Row;issues:Row[];canConfirm:boolean;saved:()=>void}) {
  const [selected,setSelected]=useState<Row|null>(null),[pending,setPending]=useState(false),[error,setError]=useState("");
  const [key,setKey]=useState("");
  const [inspection,setInspection]=useState<number|null>(null);
  if(!issues.length)return null;
  return <section className="cleaning-incident-panel"><h3>Ghi nhận sự cố phòng</h3>
    {issues.map(issue=><div key={issue.workId}><strong>{issue.title}</strong><p>{issue.legacy?"Sự cố theo hồ sơ gốc":issue.stage==="ROOM_ISSUE_REPORTED"?"Chờ giám sát kiểm tra và xác nhận":(issue.stage==="ROOM_ISSUE_INSPECTION_REQUESTED"||issue.readyForInspection)?"Đã khắc phục — chờ kiểm tra thực tế":issue.status==="CLOSED"?"Đã khắc phục và kiểm tra đạt":issue.status==="CANCELLED"?"Báo cáo đã bị từ chối":issue.kind==="MISSING"?"Đã chuyển kho xử lý":"Đã chuyển kỹ thuật xử lý"}</p>
      {issue.legacy&&<a className="button-link" href={`#room-issues?id=${issue.workId}`}>Mở hồ sơ xử lý sự cố</a>}
      {canConfirm&&issue.stage==="ROOM_ISSUE_REPORTED"&&<button className="incident-action" onClick={()=>{setSelected(issue);setKey(crypto.randomUUID());setError("");}}>Xác nhận sự cố</button>}
      {canConfirm&&(issue.stage==="ROOM_ISSUE_INSPECTION_REQUESTED"||issue.readyForInspection)&&<button className="primary" onClick={()=>setInspection(issue.workId)}>Kiểm tra sau khắc phục</button>}
    </div>)}
    {inspection&&<RoomIssues detailOnly selectedId={inspection} onClose={()=>{setInspection(null);saved();}} onChanged={saved}/>}
    {selected&&<Dialog className="confirmation-popup" title="Xác nhận sự cố sau kiểm tra phòng" close={()=>!pending&&setSelected(null)}><form onSubmit={async e=>{e.preventDefault();if(pending)return;const reason=new FormData(e.currentTarget).get("reason");setPending(true);setError("");try{await api(`work/${work.workId}/confirm-room-issue`,{rowVersion:work.rowVersion,operationKey:key,relatedId:selected.workId,reason});setSelected(null);saved();}catch(error){setError(message(error));}finally{setPending(false);}}}>
      <p>{selected.title}</p><p>Xác nhận đã kiểm tra thực tế. Báo cáo này sẽ chuyển đến {selected.kind==="MISSING"?"Thủ kho để bổ sung vật dụng":"Trưởng đội bảo trì để xử lý thiết bị hỏng"}.</p>
      <label>Kết quả kiểm tra<textarea name="reason" required minLength={10} maxLength={2000}/></label>
      {error&&<AlertPopup>{error}</AlertPopup>}<ConfirmationActions pending={pending} cancel={()=>setSelected(null)}/>
    </form></Dialog>}
  </section>;
}
