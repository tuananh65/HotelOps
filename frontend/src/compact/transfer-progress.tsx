import { type Row } from "./api";

export function TransferProgress({ rows }: { rows: Row[] }) {
  if (!rows.length) return null;
  return <section aria-label="Đối soát điều chuyển">
    <h3>Đối soát lượng gửi và thực nhận</h3>
    <div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Đề nghị</th><th>Được duyệt</th><th>Đã gửi</th><th>Thực nhận</th><th>Chờ đối soát</th></tr></thead>
      <tbody>{rows.map(r => <tr key={r.lineId}><td data-label="Vật tư">{r.name}</td><td data-label="Đề nghị">{r.requestedQuantity}</td><td data-label="Được duyệt">{r.approvedQuantity ?? "—"}</td><td data-label="Đã gửi">{r.shippedQuantity}</td><td data-label="Thực nhận">{r.receivedQuantity}</td><td data-label="Chờ đối soát">{r.pendingReconciliationQuantity}</td></tr>)}</tbody>
    </table></div>
    {rows.some(r => r.pendingReconciliationQuantity > 0) && <p>Phần chưa nhận giữ chờ đối soát. Hệ thống không tự ghi mất hoặc hoàn về kho nguồn.</p>}
  </section>;
}
