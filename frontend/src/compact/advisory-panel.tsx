import { useState } from "react";
import { api, message, timestamp, type Row } from "./api";
import { AlertPopup } from "./alert-popup";
export default function AdvisoryPanel({ branch, incidentId, allowTransfer = false }: { branch: number; incidentId?: number; allowTransfer?: boolean }) {
  const [kind, setKind] = useState(incidentId ? "INCIDENT" : "DEMAND"), [days, setDays] = useState(14);
  const [result, setResult] = useState<Row | null>(null), [pending, setPending] = useState(false), [error, setError] = useState("");
  return <section className="panel"><h3>Hỗ trợ phân tích</h3>
    <p>Kết quả chỉ để tham khảo. Người có thẩm quyền vẫn kiểm tra, phê duyệt và thực hiện theo quy trình.</p>
    <form onSubmit={async e => { e.preventDefault(); if (pending) return; setPending(true); setError(""); setResult(null);
      try { setResult(await api("advisory", { branchId: branch, kind, days, incidentId })); }
      catch (e) { setError(message(e)); } finally { setPending(false); }
    }}>
      {!incidentId && <><label>Nội dung<select value={kind} onChange={e => { setKind(e.target.value); setResult(null); }}><option value="DEMAND">Dự báo nhu cầu vật tư</option>{allowTransfer && <option value="TRANSFER">Đề xuất điều chuyển</option>}</select></label>
        <label>Số ngày dự báo<input type="number" min={1} max={90} required value={days} onChange={e => { setDays(Number(e.target.value)); setResult(null); }}/></label></>}
      <button disabled={pending}>{pending ? "Đang phân tích…" : "Xem phân tích"}</button>
    </form>
    {error && <AlertPopup>{error}</AlertPopup>}
    {result && <section aria-live="polite"><h4>{result.method === "AI_ASSISTED" ? "Có hỗ trợ AI" : "Thống kê và quy tắc nghiệp vụ — không phải kết quả AI"}</h4>
      {result.externalStatus !== "AVAILABLE" && <p>AI bên ngoài chưa khả dụng: {result.externalStatus === "BLOCKED_EXTERNAL_CONFIGURATION" ? "Chưa bật hoặc chưa đủ cấu hình" : "Nhà cung cấp chưa trả kết quả hợp lệ"}.</p>}
      <p style={{ whiteSpace: "pre-wrap" }}>{result.explanation}</p><small>Tạo lúc {timestamp(result.generatedAt)}{result.provider && ` · ${result.provider} / ${result.model}`}</small>
      {kind === "DEMAND" && <div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Khả dụng</th><th>Dự kiến cần</th><th>Đề xuất bổ sung</th><th>Giới hạn dữ liệu</th></tr></thead><tbody>{result.evidence.map((r: Row) => <tr key={`${r.warehouseId}:${r.itemId}`}><td>{r.item}<small>{r.unit}</small></td><td>{r.available}</td><td>{r.expectedDemand}</td><td>{r.suggestedReplenishment}</td><td>{r.limitation}</td></tr>)}</tbody></table></div>}
      {kind === "TRANSFER" && (result.evidence.length ? <div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Kho nguồn → kho nhận</th><th>Số lượng đề xuất</th><th>Lý do</th></tr></thead><tbody>{result.evidence.map((r: Row) => <tr key={`${r.sourceWarehouseId}:${r.destinationWarehouseId}:${r.itemId}`}><td>{r.item}</td><td>#{r.sourceWarehouseId} → #{r.destinationWarehouseId}</td><td>{r.quantity}</td><td>{r.reason}</td></tr>)}</tbody></table></div> : <p>Không có phần dư phù hợp để đề xuất trong dữ liệu hiện tại.</p>)}
      {kind === "DEMAND" && <details><summary>Cơ sở dự báo và kịch bản phòng cần dọn</summary>{result.evidence.map((r:Row)=><p key={`${r.warehouseId}:${r.itemId}`}>{r.item}: cấp {r.issuedInLast30Days}, hoàn {r.returnedInLast30Days}, tiêu hao {r.usedInLast30Days} trong 30 ngày. Trung bình 7 ngày: {Number(r.recentDailyUse).toFixed(2)}; 7 ngày trước: {Number(r.previousDailyUse).toFixed(2)}. {r.roomsNeedingCleaning} phòng cần dọn, sức chứa {r.guestCapacity}, trần theo định mức {r.roomDemandUpperBound} (cần kiểm đếm thực tế).</p>)}</details>}
      {kind === "INCIDENT" && <p>Số báo cáo khác của thiết bị trong 90 ngày: {result.evidence.repeatedAssetReportsIn90Days}. Cùng nhóm sự cố: {result.evidence.similarReports.length} báo cáo gần nhất.</p>}
    </section>}
  </section>;
}
