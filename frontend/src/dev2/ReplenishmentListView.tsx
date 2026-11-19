import { referenceLabel } from "../i18n/referenceLabel";
import { displayCode } from "../i18n/vi";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function ReplenishmentListView({ data }: { data: Values }) {
  const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
  return <section aria-label="Theo dõi bổ sung vật tư">
    <p>Trang {String(data.page)} — {String(data.total)} yêu cầu bổ sung phù hợp. Số lượng hiển thị theo đơn vị cơ sở của từng vật tư.</p>
    {rows.length === 0 && <p>Không có yêu cầu bổ sung phù hợp.</p>}
    {rows.map(r => <section key={String(r.replenishmentId)} aria-label={String(r.replenishmentNo)}>
      <h3><Link to={`/operations/replenishments/${encodeURIComponent(String(r.replenishmentId))}`}>{String(r.replenishmentNo)}</Link></h3>
      <p>Trạng thái: {displayCode(r.status)}; Kho: {referenceLabel(r, "warehouseId")}; Nhà cung cấp: {String(r.supplierName ?? "Chưa chỉ định")}; dự kiến: {String(r.expectedOn ?? "Chưa chỉ định")}</p>
      <table><caption>Số lượng yêu cầu, đã nhận và còn lại</caption>
        <thead><tr><th>Vật tư</th><th>Đơn vị cơ sở</th><th>Đã yêu cầu</th><th>Thực nhận</th><th>Còn lại</th></tr></thead>
        <tbody>{(Array.isArray(r.lines) ? r.lines.filter(isObject) : []).map(l => <tr key={String(l.lineNo)}>
          <td>{String(l.itemName)}</td><td>{String(l.unitName)}</td><td>{String(l.requestedBaseQty)}</td>
          <td>{String(l.netReceivedBaseQty)}</td><td>{String(l.remainingBaseQty)}</td>
        </tr>)}</tbody>
      </table>
      {r.status === "CLOSED" && <p>Phần còn lại đã đóng và sẽ không được nhận theo yêu cầu bổ sung này.</p>}
    </section>)}
  </section>;
}
