import { useState } from "react";
import { api, message, type Row } from "./api";
import { AlertPopup } from "./alert-popup";

export default function BatchAdjustment({document,plan,editable,changed}:{document:Row;plan:Row;editable:boolean;changed:()=>void}) {
  const [pending,setPending]=useState(false),[error,setError]=useState("");
  if(!plan?.lines?.length)return null;
  const selected=(line:number,batch:number)=>plan.selected?.find((s:Row)=>(s.LineId??s.lineId)===line&&(s.BatchId??s.batchId)===batch)?.Quantity
    ??plan.selected?.find((s:Row)=>s.lineId===line&&s.batchId===batch)?.quantity??0;
  return <section><h3>Lô điều chỉnh giảm</h3><p>Chọn đúng lô đã kiểm đếm hoặc cần hủy. Hàng hết hạn vẫn giữ trên sổ kho cho đến khi phiếu được duyệt độc lập và ghi sổ.</p>
    <form key={document.rowVersion} onSubmit={async e=>{e.preventDefault();if(pending)return;const data=new FormData(e.currentTarget);setPending(true);setError("");
      try {const lines=plan.lines.flatMap((l:Row)=>plan.batches.filter((b:Row)=>b.itemId===l.itemId).map((b:Row)=>({lineId:l.lineId,batchId:b.batchId,quantity:Number(data.get(`qty-${l.lineId}-${b.batchId}`)||0)}))).filter((l:Row)=>l.quantity>0);
        await api(`documents/${document.documentId}/adjustment-batches`,{rowVersion:document.rowVersion,operationKey:crypto.randomUUID(),lines,reason:data.get("reason")},"PUT");changed();
      }catch(e){setError(message(e));}finally{setPending(false);}}}>
      {plan.lines.map((l:Row)=><fieldset disabled={!editable||pending} key={l.lineId}><legend>{l.name} — tổng cần giảm: {l.quantity}</legend>
        {plan.batches.filter((b:Row)=>b.itemId===l.itemId).map((b:Row)=><label key={b.batchId}>{b.lotNumber} · Hạn: {b.expiresOn||"Chưa xác minh"} · Còn: {b.available}
          <input name={`qty-${l.lineId}-${b.batchId}`} type="number" min="0" max={b.available} step="0.0001" defaultValue={selected(l.lineId,b.batchId)}/></label>)}
      </fieldset>)}
      {editable&&<><label>Lý do / kết quả kiểm đếm<textarea name="reason" required maxLength={2000}/></label><button disabled={pending}>Lưu các lô trước khi duyệt</button></>}
      {error&&<AlertPopup>{error}</AlertPopup>}
    </form>
  </section>;
}
