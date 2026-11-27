import { useEffect, useState } from "react";
import type { Row } from "./api";
import { State, useQuery } from "./ui";
import { supplyQuantity } from "./supply-api";
import "./room-material-verification.css";

export type MaterialLine = { itemId: number; itemName: string; standardQuantity: number; requiredQuantity: number | null; availableQuantity: number; shortageQuantity: number | null };
export type MaterialAssessment = { confirmed: boolean; readOnly: boolean; historical?: boolean; lines: MaterialLine[] };

export function materialQuantity(line: MaterialLine, confirmed: boolean, historical = false): number | null {
  if (!confirmed) return line.requiredQuantity ?? line.standardQuantity;
  if (historical) return line.requiredQuantity;
  return line.requiredQuantity === null ? null : Math.max(0, line.standardQuantity - line.requiredQuantity);
}

export default function RoomMaterialVerification({ work, revision = 0 }: { work: Row; revision?: number }) {
  const [refresh, setRefresh] = useState(0);
  const query = useQuery<MaterialAssessment>(`supplies/room-assessments/${work.workId}`, revision + refresh, false);
  useEffect(() => {
    const reload = () => setRefresh(value => value + 1);
    window.addEventListener("focus", reload);
    window.addEventListener("compact:notification-change", reload);
    return () => { window.removeEventListener("focus", reload); window.removeEventListener("compact:notification-change", reload); };
  }, []);
  const data = query.data;
  const shortage = !data?.confirmed && data?.lines.some(line => (line.shortageQuantity ?? Math.max(0, (line.requiredQuantity ?? line.standardQuantity) - line.availableQuantity)) > 0);
  return <section className="room-material-verification" aria-label="Vật tư dọn phòng">
    <h3>Vật tư dọn phòng</h3>
    <p>Tiêu hao tự động khi nhận việc hoặc bắt đầu đồng thời nhận việc, theo định mức mỗi khách × sức chứa tối đa của phòng. Hoàn thành không trừ thêm vật tư.</p>
    <State {...query} retry={() => setRefresh(value => value + 1)}>
      {data && <>
        {data.confirmed && <p role="status">Đã ghi nhận tiêu hao. Lịch sử được giữ nguyên; làm lại không trừ vật tư lần nữa.</p>}
        <div className="material-table-scroll"><table>
          <thead><tr><th>Vật tư</th><th>Định mức phòng</th><th>{data.confirmed ? "Đã ghi nhận" : "Cần dùng khi nhận việc"}</th>{!data.confirmed && <><th>Khả dụng</th><th>Còn thiếu</th></>}</tr></thead>
          <tbody>{data.lines.map(line => <tr key={line.itemId}>
            <td data-label="Vật tư">{line.itemName}</td>
            <td data-label="Định mức phòng">{supplyQuantity(line.standardQuantity)}</td>
            <td data-label={data.confirmed ? "Đã ghi nhận" : "Cần dùng khi nhận việc"}>{materialQuantity(line, data.confirmed, data.historical) === null ? "—" : supplyQuantity(materialQuantity(line, data.confirmed, data.historical)!)}</td>
            {!data.confirmed && <><td data-label="Khả dụng">{supplyQuantity(line.availableQuantity)}</td><td data-label="Còn thiếu">{supplyQuantity(line.shortageQuantity ?? Math.max(0, (line.requiredQuantity ?? line.standardQuantity) - line.availableQuantity))}</td></>}
          </tr>)}</tbody>
        </table></div>
        {!data.lines.length && <p>Không có vật tư tiêu hao trong định mức phòng này.</p>}
        {shortage && <p role="status">Vật tư đang giữ chưa đủ để nhận phòng. <a href="#requests?create=CONSUMABLE_REFILL_REQUEST">Yêu cầu bổ sung vật tư</a>; sau khi kho cấp, tải lại số dư.</p>}
        {!data.confirmed && <div className="material-actions"><button type="button" disabled={query.loading} onClick={() => setRefresh(value => value + 1)}>Tải lại số dư</button></div>}
      </>}
    </State>
  </section>;
}
