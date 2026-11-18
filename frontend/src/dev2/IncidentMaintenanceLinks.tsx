import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function IncidentMaintenanceLinks({ data }: { data: Values }) {
  if (!Array.isArray(data.maintenanceWorks) || data.maintenanceWorks.length === 0) return null;
  const rows = data.maintenanceWorks;
  if (!rows.every(r => isObject(r) && typeof r.workId === "number" && Number.isSafeInteger(r.workId) && r.workId > 0
    && typeof r.workNo === "string" && typeof r.status === "string"))
    return <p role="alert">Không đọc được bảo trì liên quan. Tải lại sự cố.</p>;
  return <section aria-label="Lịch sử bảo trì sự cố"><h3>Tiến độ và lịch sử xử lý bảo trì</h3>
    <p>Mở công việc để xem tiến độ, nhật ký và chi tiết xử lý trong phạm vi quyền. Các chu kỳ xử lý trước được tách riêng.</p>
    <ul>{(rows as Values[]).map(row => <li key={String(row.workId)}>
      <Link to={"/operations/maintenance/" + row.workId + "?action=A004A-036"}>{String(row.workNo)}</Link>
      {" — " + String(row.status)}
    </li>)}</ul>
  </section>;
}
