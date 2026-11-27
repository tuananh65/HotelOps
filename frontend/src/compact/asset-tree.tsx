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
export default function AssetTree({ rows, allRows, branchName, select, replacement, locations = [], rooms: roomCatalog = [], filtered = false }: { rows: Row[]; allRows: Row[]; branchName: string; select: (id: number) => void; replacement?: (asset: Row) => React.ReactNode; locations?: Row[]; rooms?: Row[]; filtered?: boolean }) {
  const floorName = (key: string) => locations.find(l => String(l.locationId) === key)?.name || allRows.find(a => floorKey(a) === key)?.floorName || "Khu vực chung / chưa gán tầng";
  const roomFloor = (room: Row) => {
    let id = room.locationId; const seen = new Set<number>();
    while (id != null && !seen.has(id)) { seen.add(id); const node = locations.find(l => l.locationId === id); if (!node) break; if (node.kind === "FLOOR") return String(id); id = node.parentLocationId; }
    return "unplaced";
  };
  const floors = [...new Set([...rows.map(floorKey), ...(!filtered ? locations.filter(l => l.kind === "FLOOR" && l.isActive !== false).map(l => String(l.locationId)) : []), ...(!filtered ? roomCatalog.filter(r => r.isActive !== false).map(roomFloor) : [])])].sort((a,b) => floorName(a).localeCompare(floorName(b), "vi", {numeric:true}));
  return <section aria-label="Tài sản theo tầng và phòng"><h2>{branchName}</h2>
    {!floors.length && <Empty>Không có tài sản phù hợp.</Empty>}
    {floors.map(floor => { const floorRows = rows.filter(a => floorKey(a) === floor); const fullFloor = allRows.filter(a => floorKey(a) === floor); const configuredRooms = roomCatalog.filter(r => r.isActive !== false && roomFloor(r) === floor); const roomCode = (key: string) => configuredRooms.find(r => String(r.roomId) === key)?.code || floorRows.find(a => roomKey(a) === key)?.roomCode || ""; const rooms = [...new Set([...floorRows.map(roomKey), ...(!filtered ? configuredRooms.map(r => String(r.roomId)) : [])])].sort((a,b) => roomCode(a).localeCompare(roomCode(b), "vi", {numeric:true}));
      return <details key={floor} className="card" style={{ marginBottom: 12, padding: 16 }}><summary><strong>{floorName(floor)}</strong> · {floorRows.length} tài sản<Warning rows={fullFloor}/></summary>
        {!rooms.length && <p className="muted">Chưa có phòng hoặc tài sản.</p>}
        {rooms.map(room => { const assets = floorRows.filter(a => roomKey(a) === room); return <details key={room} style={{ margin: "12px 0 0 16px" }}><summary><strong>{room === "common" ? "Khu vực chung / chưa gán phòng" : `Phòng ${roomCode(room)}`}</strong> · {assets.length} tài sản<Warning rows={fullFloor.filter(a => roomKey(a) === room)}/></summary>
          {!assets.length ? <p className="muted">Chưa có tài sản được ghi nhận trong phòng.</p> :
          <div className="table-scroll"><table><thead><tr><th>Tài sản</th><th>Mã</th><th>Tình trạng</th><th>Sử dụng / Vị trí</th><th>Thao tác</th></tr></thead><tbody>{assets.map(a => <tr key={a.assetId} style={damaged(a) ? { backgroundColor: "#fff0ed" } : undefined}><td>{a.name}</td><td>{a.assetCode}</td><td>{damaged(a) && <AlertTriangle size={16} aria-hidden="true"/>} {assetConditionLabel(a.conditionCode)}</td><td>{a.conditionCode === "PENDING_INSPECTION" ? "Chờ kiểm kê" : a.isActive ? "Đang sử dụng" : "Đã ngừng"}<small>{a.locationName}</small></td><td><button onClick={() => select(a.assetId)}>Chi tiết</button>{replacement?.(a)}</td></tr>)}</tbody></table></div>
          }
        </details>; })}
      </details>;
    })}
  </section>;
}
