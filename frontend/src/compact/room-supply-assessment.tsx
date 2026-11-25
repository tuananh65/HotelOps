import { useState } from "react";
import { api, message } from "./api";
import { State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";

type Line = { reservationId: number; itemId: number; name: string; requiredQuantity: number; usableQuantity: number | null; missingQuantity: number; assessedAt: string | null; rowVersion: string; currentQuantity: number | null; unusableQuantity: number | null; expiredQuantity: number | null; replenishedQuantity: number | null; note: string | null };

export default function RoomSupplyAssessment({ workId, editable }: { workId: number; editable: boolean }) {
  const [revision, setRevision] = useState(0);
  const query = useQuery<Line[]>(`supplies/room-assessments/${workId}`, revision);
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID());
  const [draft, setDraft] = useState<Record<number, string>>({});
  return <section className="form-panel">
    <h3>Kiểm tra vật tư trong phòng</h3>
    <p>Vật tư được cấp khi nhận công việc theo sức chứa tối đa của phòng. Kiểm đếm đồ hiện có; tách đồ còn dùng được, không đạt và hết hạn. Lưu kiểm tra để giải phóng phần giữ chỗ dư. Chỉ ghi nhận tiêu hao theo số thực tế đã bổ sung trước khi hoàn thành.</p>
    {error && <AlertPopup>{error}</AlertPopup>}
    <State {...query} retry={() => setRevision(value => value + 1)}>
      {query.data?.length ? <form onSubmit={async event => {
        event.preventDefault(); if (pending) return;
        const data = new FormData(event.currentTarget);
        setPending(true); setError("");
        try {
          await api(`supplies/room-assessments/${workId}`, { operationKey, lines: query.data!.map(line => ({ reservationId: line.reservationId, usableQuantity: Number(data.get(`usable-${line.reservationId}`)), rowVersion: line.rowVersion,
            currentQuantity: Number(data.get(`current-${line.reservationId}`)), unusableQuantity: Number(data.get(`unusable-${line.reservationId}`)), expiredQuantity: Number(data.get(`expired-${line.reservationId}`)),
            replenishedQuantity: Number(data.get(`replenished-${line.reservationId}`)), note: data.get(`note-${line.reservationId}`) || null })) });
          setOperationKey(crypto.randomUUID()); setRevision(value => value + 1); setDraft({});
        } catch (error) { setError(message(error)); } finally { setPending(false); }
      }}>
        {query.data.map(line => <fieldset key={`${line.reservationId}-${line.rowVersion}`} disabled={!editable || pending}>
          <legend>{line.name} · Định mức {line.requiredQuantity}</legend>
          <label>Tổng số hiện có<input name={`current-${line.reservationId}`} type="number" min="0" max="999999999" step="0.0001" required defaultValue={line.currentQuantity ?? ""}/></label>
          <label>Còn sử dụng được
          <input name={`usable-${line.reservationId}`} type="number" min="0" max="999999999" step="0.0001" required disabled={!editable || pending}
            aria-label={`${line.name}: số lượng còn sử dụng được`}
            defaultValue={line.usableQuantity ?? ""}
            onChange={event => setDraft(value => ({ ...value, [line.reservationId]: event.target.value }))} /></label>
          <label>Không đạt (không gồm hết hạn)<input name={`unusable-${line.reservationId}`} type="number" min="0" max="999999999" step="0.0001" required defaultValue={line.unusableQuantity ?? ""}/></label>
          <label>Hết hạn<input name={`expired-${line.reservationId}`} type="number" min="0" max="999999999" step="0.0001" required defaultValue={line.expiredQuantity ?? ""}/></label>
          <small>Cần bổ sung: {Math.max(0, line.requiredQuantity - Number(draft[line.reservationId] ?? line.usableQuantity ?? 0))}{line.assessedAt ? " · Đã kiểm tra" : " · Chưa kiểm tra"}</small>
          <label>Đã bổ sung thực tế<input name={`replenished-${line.reservationId}`} type="number" min="0" max={Math.max(0, line.requiredQuantity - Number(draft[line.reservationId] ?? line.usableQuantity ?? 0))} step="0.0001" required defaultValue={line.replenishedQuantity ?? 0}/></label>
          <label>Ghi chú<textarea name={`note-${line.reservationId}`} maxLength={2000} defaultValue={line.note ?? ""}/></label>
        </fieldset>)}
        {editable && <button type="submit" className="primary" disabled={pending}>{pending ? "Đang lưu…" : "Lưu kiểm tra vật tư"}</button>}
      </form> : <p>Chưa có dữ liệu giữ chỗ vật tư cho công việc này.</p>}
    </State>
  </section>;
}
