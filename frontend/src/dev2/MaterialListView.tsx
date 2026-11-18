import { displayCode } from "../i18n/vi";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaterialListView({ data }: { data: Values }) {
  const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
  return <section aria-label="Theo dõi yêu cầu vật tư">
    <p>Trang {String(data.page)}; {String(data.total)} yêu cầu phù hợp, mới nhất trước.</p>
    {rows.length === 0 && <p>Không có yêu cầu vật tư phù hợp.</p>}
    {rows.map(request => <section key={String(request.requestId)}>
      <h3><Link to={`/operations/material-requests/${encodeURIComponent(String(request.requestId))}`}>{String(request.requestNo)}</Link></h3>
      <p>Trạng thái: {displayCode(request.status)}; Người yêu cầu: {String(request.requesterId)}; người duyệt được chỉ định: {String(request.assignedApproverUserId ?? "Chưa ghi nhận")}</p>
      <p>Đã tạo: {String(request.createdAt)}; cần lúc: {String(request.neededAt ?? "Chưa ghi nhận")}</p>
      <table><caption>Số lượng yêu cầu, đã duyệt và đã cấp theo vật tư và đơn vị</caption>
        <thead><tr><th>Vật tư</th><th>Đơn vị</th><th>Đã yêu cầu</th><th>Đã duyệt</th><th>Đã cấp</th></tr></thead>
        <tbody>{(Array.isArray(request.quantities) ? request.quantities.filter(isObject) : []).map(line => <tr key={String(line.requestLineId)}>
          <td>{String(line.itemName)}</td><td>{String(line.unitName)}</td><td>{String(line.requestedQty)}</td>
          <td>{String(line.approvedQty ?? "Chưa duyệt")}</td><td>{String(line.fulfilledQty)}</td>
        </tr>)}</tbody>
      </table>
    </section>)}
  </section>;
}
