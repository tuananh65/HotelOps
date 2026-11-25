import { useState } from "react";
import { api, message, type Row } from "./api";
import { State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";

export default function ExecutionChecklistPicker({work,onSaved}:{work:Row;onSaved:()=>void}){
  const options=useQuery<Row[]>(`work/${work.workId}/execution-checklists`);
  const [pending,setPending]=useState(false),[error,setError]=useState("");
  return <section className="panel"><h3>Checklist thực hiện bắt buộc</h3>
    {error&&<AlertPopup>{error}</AlertPopup>}
    <State {...options}><form onSubmit={async event=>{
      event.preventDefault();if(pending)return;const data=new FormData(event.currentTarget);setPending(true);setError("");
      try{await api(`work/${work.workId}/execution-checklist`,{rowVersion:work.rowVersion,checklistTemplateId:Number(data.get("template"))});onSaved();}
      catch(error){setError(message(error));}finally{setPending(false);}
    }}>
      <label>Mẫu đã phát hành<select name="template" required disabled={pending} defaultValue=""><option value="">Chọn checklist</option>{options.data?.map(t=><option key={t.checklistTemplateId} value={t.checklistTemplateId}>{t.name} · phiên bản {t.versionNo}</option>)}</select></label>
      {!options.data?.length&&<p>Chưa có checklist phù hợp được phát hành cho chi nhánh. Người có quyền cấu hình checklist cần phát hành mẫu trước khi hoàn thành công việc.</p>}
      <button disabled={pending||!options.data?.length}>Gắn checklist vào công việc</button>
    </form></State>
  </section>;
}
