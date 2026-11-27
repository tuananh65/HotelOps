import { useState } from "react";
import type { Row } from "./api";

type Props = {
  objects: Row;
  types: Row[];
  reception: boolean;
  pending: boolean;
  cleaningWorkId: number | null;
  submit: (type: Row, lines: Row[], note: string) => void;
};

export default function RoomIssueReportForm({ objects, types, reception, pending, cleaningWorkId, submit }: Props) {
  const [typeId, setTypeId] = useState("");
  const [category, setCategory] = useState("reusable");
  const [selected, setSelected] = useState<Record<string, string>>({});
  const [note, setNote] = useState("");
  const type = types.find(t => String(t.referenceId) === typeId);
  const missing = type?.kind === "MISSING";
  const assets = (missing ? objects.missingAssets || [] : objects.assets || []).map((item: Row) => ({ ...item, key: `asset:${item.assetId}`, code: item.assetCode, quantity: 1 }));
  const reusable = (missing ? objects.missingReusableItems || [] : objects.reusableItems || []).map((item: Row) => ({ ...item, key: `item:${item.itemId}` }));
  const rows: Row[] = missing ? category === "asset" ? assets : reusable : assets;
  const chosen = rows.filter(row => selected[row.key] !== undefined);
  const valid = chosen.length > 0 && chosen.every(row => {
    const quantity = Number(selected[row.key]);
    return Number.isSafeInteger(quantity) && quantity > 0
      && (!row.assetId || quantity === 1)
      && (row.maxReportableQuantity == null || quantity <= Number(row.maxReportableQuantity));
  });
  function choose(key: string, checked: boolean) {
    setSelected(previous => {
      const next = { ...previous };
      if (checked) next[key] = "1";
      else delete next[key];
      return next;
    });
  }
  return <form className="room-report-form" data-cleaning-continuation={cleaningWorkId ? "true" : undefined} onSubmit={event => {
    event.preventDefault();
    if (!type || !valid || pending) return;
    submit(type, chosen.map(row => ({ assetId: row.assetId || null, itemId: row.assetId ? null : row.itemId, quantity: Number(selected[row.key]) })), note.trim());
  }}>
    <p><strong>Phòng {objects.code}</strong></p>
    <fieldset disabled={pending}>
      <label>Loại báo cáo<select required value={typeId} onChange={event => { setTypeId(event.target.value); setSelected({}); }}>
        <option value="">Chọn loại báo cáo</option>
        {types.filter(t => t.isActive && (!reception || t.kind === "BROKEN")).map(t => <option key={t.referenceId} value={t.referenceId}>{t.name}</option>)}
      </select></label>
      {!types.some(t => t.isActive) && <p>Quản lý vận hành cần bật loại báo cáo trước.</p>}
      {type && <>
        {!missing && <p className="room-report-guidance">Chỉ báo hỏng tài sản trong phòng. Vật tư thay thế như khăn, ga chỉ được báo ở loại mất/thiếu đồ.</p>}
        {missing && <div className="room-report-categories" role="group" aria-label="Nhóm đồ bị thiếu">
          <button type="button" aria-pressed={category === "reusable"} onClick={() => { setCategory("reusable"); setSelected({}); }}>Vật tư thay thế</button>
          <button type="button" aria-pressed={category === "asset"} onClick={() => { setCategory("asset"); setSelected({}); }}>Tài sản trong phòng</button>
        </div>}
        {missing && <p className="room-report-guidance">{category === "reusable"
          ? "Chọn khăn, ga hoặc đồ thay thế bị thiếu. Giám sát xác minh trước khi ghi giảm số đồ cũ không thu hồi được; báo cáo không trừ đồ sạch thêm lần nữa."
          : "Chọn tài sản bị thiếu trong phòng. Sau khi giám sát xác minh, báo cáo được chuyển quản lý vận hành để xử lý mua bổ sung."}</p>}
        <div className="room-report-item-list" role="group" aria-label="Đồ vật cần báo cáo">
          {rows.map(row => <div className={`room-report-item${selected[row.key] !== undefined ? " is-selected" : ""}`} key={row.key}>
            <label className="room-report-item-choice"><input type="checkbox" disabled={row.maxReportableQuantity === 0 || pending} checked={selected[row.key] !== undefined} onChange={event => choose(row.key, event.target.checked)} /><span><strong>{row.name}</strong>{row.code && <small>{row.code}</small>}{row.maxReportableQuantity != null && <small>Có thể báo thiếu: {row.maxReportableQuantity}</small>}</span></label>
            {row.assetId ? <span>1 tài sản</span> : <label className="room-report-quantity">{missing ? "Số lượng thiếu" : "Số lượng hỏng"}<input aria-label={`Số lượng ${row.name}`} type="number" min="1" step="1" max={row.maxReportableQuantity ?? undefined} disabled={selected[row.key] === undefined || pending} required={selected[row.key] !== undefined} value={selected[row.key] || ""} onChange={event => setSelected(previous => ({ ...previous, [row.key]: event.target.value }))} /></label>}
          </div>)}
          {!rows.length && <p className="room-report-empty">{missing && category === "reusable" ? "Chưa có vật tư thay thế đủ điều kiện báo thiếu cho công việc này. Liên hệ giám sát để kiểm tra dữ liệu cấp và thay đồ." : "Chưa có tài sản hoặc vật tư thuộc phòng để báo cáo."}</p>}
        </div>
        <label>Mô tả tình trạng{type.requireNote ? " (bắt buộc)" : ""}<textarea value={note} required={type.requireNote === true} maxLength={2000} onChange={event => setNote(event.target.value)} placeholder="Ghi rõ vị trí và tình trạng thực tế…" /></label>
        <div className="room-report-actions"><span>Đã chọn {chosen.length} loại đồ</span><button className="primary" disabled={pending || !valid}>Gửi báo cáo</button></div>
      </>}
    </fieldset>
  </form>;
}
