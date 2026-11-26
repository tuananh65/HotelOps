import { AlertTriangle } from "lucide-react";
import type { Row } from "./api";
import { assetConditionLabel } from "./asset-presentation";
import { Empty } from "./ui";

const damaged = (asset: Row) => asset.isActive && ["FAULTY", "UNDER_REPAIR"].includes(asset.conditionCode);
const floorKey = (a: Row) => String(a.floorId ?? "unplaced");
const roomKey = (a: Row) => String(a.roomId ?? "common");
function Warning({ rows }: { rows: Row[] }) {
  const count = rows.filter(damaged).length;
  return count ? <span style={{ color: "#a61b1b", marginLeft: 12 }}><AlertTriangle size={16} aria-hidden="true"/> {count} tài sản hỏng / đang sửa</span> : null;
}
export default function AssetTree({ rows, allRows, branchName, select }: { rows: Row[]; allRows: Row[]; branchName: string; select: (id: number) => void }) {
  const floors = [...new Set(rows.map(floorKey))].sort((a, b) => String(rows.find(r => floorKey(r) === a)?.floorName || "\uffff").localeCompare(String(rows.find(r => floorKey(r) === b)?.floorName || "\uffff"), "vi", { numeric: true }));
  return <section aria-label="Tài sản theo tầng và phòng"><h2>{branchName}</h2>
    {!rows.length && <Empty>Không có tài sản phù hợp.</Empty>}
    {floors.map(floor => { const floorRows = rows.filter(a => floorKey(a) === floor); const fullFloor = allRows.filter(a => floorKey(a) === floor); const rooms = [...new Set(floorRows.map(roomKey))];
      return <details key={floor} className="card" style={{ marginBottom: 12, padding: 16 }}><summary><strong>{floorRows[0].floorName || "Khu vực chung / chưa gán tầng"}</strong> · {floorRows.length} tài sản<Warning rows={fullFloor}/></summary>
        {rooms.map(room => { const assets = floorRows.filter(a => roomKey(a) === room); return <details key={room} style={{ margin: "12px 0 0 16px" }}><summary><strong>{room === "common" ? "Khu vực chung / chưa gán phòng" : `Phòng ${assets[0].roomCode}`}</strong> · {assets.length} tài sản<Warning rows={fullFloor.filter(a => roomKey(a) === room)}/></summary>
          <div className="table-scroll"><table><thead><tr><th>Tài sản</th><th>Mã</th><th>Tình trạng</th><th>Sử dụng / Vị trí</th><th>Thao tác</th></tr></thead><tbody>{assets.map(a => <tr key={a.assetId} style={damaged(a) ? { backgroundColor: "#fff0ed" } : undefined}><td>{a.name}</td><td>{a.assetCode}</td><td>{damaged(a) && <AlertTriangle size={16} aria-hidden="true"/>} {assetConditionLabel(a.conditionCode)}</td><td>{a.conditionCode === "PENDING_INSPECTION" ? "Chờ kiểm kê" : a.isActive ? "Đang sử dụng" : "Đã ngừng"}<small>{a.locationName}</small></td><td><button onClick={() => select(a.assetId)}>Chi tiết</button></td></tr>)}</tbody></table></div>
        </details>; })}
      </details>;
    })}
  </section>;
}
