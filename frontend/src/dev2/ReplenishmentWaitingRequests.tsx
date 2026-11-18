import { displayCode } from "../i18n/vi";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function ReplenishmentWaitingRequests({ record }: { record: Values }) {
  const rows = Array.isArray(record.waitingRequests) ? record.waitingRequests.filter(isObject) : [];
  return <section aria-label="Yêu cầu vật tư đang chờ hàng bổ sung">
    <h3>Yêu cầu đang chờ các vật tư này</h3>
    {record.waitingRequestsVisible !== true
      ? <p>Cần quyền xem yêu cầu vật tư riêng.</p>
      : <>
        <p>Số lượng đã duyệt còn chờ cấp tại kho này trong phạm vi bạn được xem. Nhập kho không giữ hàng hoặc hoàn tất yêu cầu. Số lượng theo đơn vị của từng dòng.</p>
        {rows.length === 0 ? <p>Không có yêu cầu vật tư đang chờ trong phạm vi xem của bạn.</p>
          : <table><thead><tr><th>Yêu cầu</th><th>Trạng thái</th><th>Vật tư</th><th>Đơn vị</th><th>Đã duyệt</th><th>Đã cấp</th><th>Vẫn đang chờ</th></tr></thead>
            <tbody>{rows.map(r => <tr key={String(r.requestLineId)}>
              <td><Link to={`/operations/material-requests/${encodeURIComponent(String(r.requestId))}`}>{String(r.requestNo)}</Link></td>
              <td>{displayCode(r.status)}</td><td>{String(r.itemName)}</td><td>{String(r.unitName)}</td>
              <td>{String(r.approvedQty)}</td><td>{String(r.fulfilledQty)}</td><td>{String(r.remainingQty)}</td>
            </tr>)}</tbody></table>}
      </>}
  </section>;
}
