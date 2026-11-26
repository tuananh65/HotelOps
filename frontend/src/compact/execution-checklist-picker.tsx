import { useState } from "react";
import { api, message, type Row } from "./api";
import { State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";

export default function ExecutionChecklistPicker({work,onSaved}:{work:Row;onSaved:()=>void}){
  const options=useQuery<Row[]>(`work/${work.workId}/execution-checklists`);
  const [pending,setPending]=useState(false),[error,setError]=useState("");
  const optional=["CLEANING","REWORK"].includes(work.kind);
  const [expanded,setExpanded]=useState(false);
  return <section className="panel"><h3>{optional ? "Checklist thực hiện" : "Checklist thực hiện bắt buộc"}</h3>
    {optional && <><p>Công việc chưa có checklist. Bạn có thể bỏ qua checklist và gửi hoàn thành sau khi dọn phòng.</p><button type="button" onClick={()=>setExpanded(value=>!value)}>{expanded ? "Đóng lựa chọn checklist" : "Thêm checklist (tùy chọn)"}</button></>}
    {error&&<AlertPopup>{error}</AlertPopup>}
    {(!optional||expanded)&&<State {...options}><form onSubmit={async event=>{
      event.preventDefault();if(pending)return;const data=new FormData(event.currentTarget);setPending(true);setError("");
      try{await api(`work/${work.workId}/execution-checklist`,{rowVersion:work.rowVersion,checklistTemplateId:Number(data.get("template"))});onSaved();}
      catch(error){setError(message(error));}finally{setPending(false);}
    }}>
      <label>Mẫu đã phát hành<select name="template" required disabled={pending} defaultValue=""><option value="">Chọn checklist</option>{options.data?.map(t=><option key={t.checklistTemplateId} value={t.checklistTemplateId}>{t.name} · phiên bản {t.versionNo}</option>)}</select></label>
      {!options.data?.length&&<p>{optional ? "Chưa có checklist phù hợp được phát hành. Bạn vẫn có thể hoàn thành công việc mà không gắn checklist." : "Chưa có checklist phù hợp được phát hành cho chi nhánh. Người có quyền cấu hình checklist cần phát hành mẫu trước khi hoàn thành công việc."}</p>}
      <button disabled={pending||!options.data?.length}>Gắn checklist vào công việc</button>
    </form></State>}
  </section>;
}
