import { useState } from "react";
import { api, message } from "./api";
import { State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";

type Line = { reservationId: number; itemId: number; name: string; requiredQuantity: number; usableQuantity: number | null; missingQuantity: number; assessedAt: string | null; rowVersion: string; currentQuantity: number | null; unusableQuantity: number | null; expiredQuantity: number | null; replenishedQuantity: number | null; note: string | null };
type Values = { currentQuantity: string; usableQuantity: string; unusableQuantity: string; expiredQuantity: string; replenishedQuantity: string; note: string };
const fields = {currentQuantity:"Tổng số hiện có",usableQuantity:"Còn sử dụng được",unusableQuantity:"Không đạt (không gồm hết hạn)",expiredQuantity:"Hết hạn",replenishedQuantity:"Đã bổ sung thực tế"} as const;
function initial(line: Line): Values {
  return {currentQuantity:String(line.currentQuantity??""),usableQuantity:String(line.usableQuantity??""),unusableQuantity:String(line.unusableQuantity??0),expiredQuantity:String(line.expiredQuantity??0),replenishedQuantity:String(line.replenishedQuantity??0),note:line.note??""};
}

export default function RoomSupplyAssessment({ workId, editable }: { workId: number; editable: boolean }) {
  const [revision, setRevision] = useState(0);
  const query = useQuery<Line[]>(`supplies/room-assessments/${workId}`, revision, false);
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID());
  const [draft, setDraft] = useState<Record<number, Values>>({});
  function change(line: Line, field: keyof Values, value: string) {
    setDraft(previous=>({...previous,[line.reservationId]:{...(previous[line.reservationId]??initial(line)),[field]:value}}));
  }
  return <section className="form-panel room-supply-assessment">
    <h3>Kiểm tra vật tư trong phòng</h3>
    <p>Mở từng vật tư để kiểm đếm. Định mức theo sức chứa tối đa; chỉ ghi nhận tiêu hao theo số thực tế bổ sung.</p>
    {error && <AlertPopup>{error}</AlertPopup>}
    <State {...query} retry={() => setRevision(value => value + 1)}>
      {query.data?.length ? <form noValidate onSubmit={async event => {
        event.preventDefault(); if (pending) return;
        const lines=query.data!.map(line=> {
          const value=draft[line.reservationId]??initial(line);
          return {reservationId:line.reservationId,rowVersion:line.rowVersion,
            ...Object.fromEntries(Object.keys(fields).map(key=>[key,value[key as keyof Values]===""?null:Number(value[key as keyof Values])])),note:value.note||null};
        });
        if(lines.some(line=>Object.keys(fields).some(key=>(line as any)[key]===null))){setError("Nhập đầy đủ số lượng cho từng vật tư.");return;}
        for(let index=0;index<lines.length;index++){
          const line=lines[index] as typeof lines[number]&Record<keyof typeof fields,number>;
          if(Object.keys(fields).some(key=>{const quantity=line[key as keyof typeof fields];return !Number.isFinite(quantity)||quantity<0||quantity>999999999||Math.abs(quantity*10000-Math.round(quantity*10000))>0.001;})){
            setError("Số lượng phải từ 0 đến 999999999 và tối đa 4 chữ số thập phân.");return;
          }
          if(Math.abs(line.currentQuantity-line.usableQuantity-line.unusableQuantity-line.expiredQuantity)>0.00001){setError("Tổng số hiện có phải bằng số còn dùng được, không đạt và hết hạn.");return;}
          if(line.replenishedQuantity>Math.max(0,query.data![index].requiredQuantity-line.usableQuantity)){setError("Số bổ sung thực tế không được vượt quá số còn thiếu.");return;}
        }
        setPending(true); setError("");
        try {
          await api(`supplies/room-assessments/${workId}`, { operationKey, lines });
          setOperationKey(crypto.randomUUID()); setRevision(value => value + 1); setDraft({});
        } catch (error) { setError(message(error)); } finally { setPending(false); }
      }}>
        {query.data.map(line=>{
          const value=draft[line.reservationId]??initial(line);
          const missing=Math.max(0,line.requiredQuantity-Number(value.usableQuantity||0));
          return <details key={line.reservationId} className="supply-assessment-group">
            <summary>{line.name} · Định mức {line.requiredQuantity}<small>Cần bổ sung: {missing}{line.assessedAt?" · Đã lưu":" · Chưa kiểm tra"}</small></summary>
            <fieldset disabled={!editable||pending}>
              <legend className="sr-only">{line.name}</legend>
              {Object.entries(fields).map(([key,title])=><label key={key}>{title}<input
                name={`${key}-${line.reservationId}`} aria-label={`${line.name}: ${title.toLowerCase()}`} type="number" min="0"
                max={key==="replenishedQuantity"?missing:999999999} step="0.0001"
                value={value[key as keyof Values]} onChange={event=>change(line,key as keyof Values,event.target.value)}/></label>)}
              <label>Ghi chú<textarea maxLength={2000} value={value.note} onChange={event=>change(line,"note",event.target.value)}/></label>
            </fieldset>
          </details>;
        })}
        {editable && <button type="submit" className="primary" disabled={pending}>{pending ? "Đang lưu…" : "Lưu kiểm tra vật tư"}</button>}
      </form> : <p>Chưa có dữ liệu giữ chỗ vật tư cho công việc này.</p>}
    </State>
  </section>;
}
