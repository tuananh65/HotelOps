import { useState } from "react";
import { type Row } from "./api";
import { Empty, State, useQuery } from "./ui";

// Deliberately uses the restricted transfer catalog, never changes the global branch.
export default function CrossBranchStock({ branch }: { branch: number }) {
  const query = useQuery(`documents/transfer-catalog?branchId=${branch}`);
  const [warehouse, setWarehouse] = useState(0);
  const [search, setSearch] = useState("");
  const warehouses: Row[] = query.data?.warehouses.filter((w: Row) => w.branchId !== branch) || [];
  const selected = warehouse || warehouses[0]?.warehouseId;
  const rows: Row[] = query.data?.items.filter((i: Row) => `${i.name} ${i.code}`.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi"))) || [];
  return <State {...query}>
    <p>Chỉ xem tồn khả dụng để đề nghị điều chuyển. Quyền quản lý chi nhánh hiện tại không thay đổi.</p>
    <label>Kho cung cấp<select value={selected || ""} onChange={e => setWarehouse(Number(e.target.value))}>{warehouses.map(w => <option key={w.warehouseId} value={w.warehouseId}>{w.branchName} · {w.name}</option>)}</select></label>
    <label>Tìm vật tư<input type="search" value={search} onChange={e => setSearch(e.target.value)} placeholder="Tên hoặc mã vật tư" /></label>
    {selected ? <><div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Đơn vị</th><th>Tồn khả dụng</th></tr></thead><tbody>{rows.map(i => <tr key={i.itemId}><td>{i.name}<small>{i.code}</small></td><td>{query.data.units.find((u: Row) => u.referenceId === i.baseUnitId)?.name || "—"}</td><td>{query.data.balances.find((b: Row) => b.warehouseId === selected && b.itemId === i.itemId)?.availableBaseQty ?? 0}</td></tr>)}</tbody></table></div><a className="button primary" href={`#documents?transfer=1&sourceWarehouseId=${selected}`}>Yêu cầu điều chuyển</a></> : <Empty>Không có kho chi nhánh khác đang hoạt động.</Empty>}
  </State>;
}
