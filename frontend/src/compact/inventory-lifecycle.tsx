import { useEffect, useState } from "react";
import { api, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, Empty, Pager, State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";
const classes: Record<string,string> = { CONSUMABLE: "Tiêu hao", REUSABLE: "Tái sử dụng", ASSET: "Tài sản", UNCLASSIFIED: "Chưa phân loại" };
const statuses: Record<string,string> = { READY: "Đã xác minh", LEGACY_REVIEW: "Cần đối soát dữ liệu cũ", QUARANTINED: "Cách ly / chờ xác minh", EXPIRED: "Hết hạn" };
const expiry: Record<string,string> = { REQUIRED: "Bắt buộc hạn dùng (FEFO)", NONE: "Không có hạn dùng (FIFO)", UNKNOWN: "Chưa xác định — chưa được cấp" };
export default function InventoryLifecycle({ branch, configuration = false }: { branch: number; configuration?: boolean }) {
  const { can, profile } = useAuth();
  const [revision, bump] = useState(0), [edit, setEdit] = useState<Row | null>(null), [pending, setPending] = useState(false), [error, setError] = useState("");
  const [page, setPage] = useState(1), [search, setSearch] = useState("");
  const [history, setHistory] = useState<Row | null>(null), [historyPage, setHistoryPage] = useState(1);
  useEffect(() => { setPage(1); setSearch(""); setEdit(null); setHistory(null); setError(""); }, [branch, configuration]);
  const query = useQuery(`inventory-lifecycle/${configuration ? "profiles" : "batches"}?branchId=${branch}${configuration ? "" : `&page=${page}&pageSize=25&search=${encodeURIComponent(search)}`}`, revision);
  const historyQuery = useQuery(history ? `inventory-lifecycle/batches/${history.batchId}/history?page=${historyPage}&pageSize=25` : null, revision);
  const editable = configuration ? can("ROOM_SUPPLY_CONFIG", false) && profile.roles.some(r => r.code === "OPERATION_MANAGER" && r.branchId === branch) :
    can("INVENTORY_VIEW", false) && profile.roles.some(r => r.code === "STOREKEEPER" && r.branchId === branch);
  const profiles: Row[] = configuration ? (query.data || []).filter((r: Row) => `${r.name} ${r.code}`.toLocaleLowerCase("vi").includes(search.trim().toLocaleLowerCase("vi"))) : [];
  const total = configuration ? profiles.length : query.data?.total || 0;
  const currentPage = configuration ? Math.min(page, Math.max(1, Math.ceil(total / 25))) : page;
  const rows: Row[] = configuration ? profiles.slice((currentPage - 1) * 25, currentPage * 25) : query.data?.items || [];
  useEffect(() => { if (!query.loading && query.data && !configuration && page > Math.max(1, Math.ceil(total / 25))) setPage(Math.max(1, Math.ceil(total / 25))); }, [query.loading, query.data, configuration, page, total]);
  return <section><h2>{configuration ? "Phân loại và chính sách hạn dùng" : "Lô vật tư và hạn sử dụng"}</h2>
    <p>{configuration ? "Phân biệt tiêu hao, vật tư tái sử dụng và tài sản. Đổi loại hàng đã có giao dịch cần đối soát số dư." : "Chỉ lô đã xác minh, còn hạn và không bị cách ly mới được tính vào lượng có thể cấp. Lô cũ không tự được gán hạn dùng."}</p>
    <label>{configuration ? "Tìm vật tư" : "Tìm vật tư, mã lô hoặc kho"}<input value={search} onChange={e => { setSearch(e.target.value); setPage(1); }}/></label>
    <State {...query}><div className="table-scroll"><table><thead><tr>
      <th>Vật tư</th>{configuration ? <><th>Phân loại</th><th>Hạn sử dụng</th></> : <><th>Lô / Kho</th><th>Hạn dùng</th><th>Tại kho / Có thể cấp</th><th>Nhân viên đang giữ</th><th>Trạng thái</th></>}<th>Thao tác</th>
    </tr></thead><tbody>{rows.map(row => <tr key={row.batchId || row.itemId}>
      <td data-label="Vật tư">{row.name || row.item}</td>{configuration ? <><td data-label="Phân loại">{classes[row.classification]}</td><td data-label="Hạn sử dụng">{expiry[row.expiryPolicy]}</td></> : <>
        <td data-label="Lô / Kho">{row.lotNumber}<small>{row.warehouse}</small><small>Nhập: {timestamp(row.receivedAt)}</small></td>
        <td data-label="Hạn dùng">{row.expiresOn || "Chưa có"}{row.expiring && <small>Sắp hết hạn</small>}</td><td data-label="Tại kho / Có thể cấp">{row.warehouseQuantity} / {row.available}</td><td data-label="Nhân viên đang giữ">{row.staffQuantity}</td>
        <td data-label="Trạng thái">{row.expired ? "Hết hạn" : statuses[row.status]}</td></>}
      <td data-label="Thao tác">{!configuration && <button onClick={() => { setHistory(row); setHistoryPage(1); }}>Lịch sử lô</button>}{editable && <button onClick={() => { setEdit(row); setError(""); }}>{configuration ? "Cấu hình" : "Xác minh lô"}</button>}</td>
    </tr>)}</tbody></table></div>{!rows.length && <Empty>Không tìm thấy vật tư phù hợp.</Empty>}<Pager page={currentPage} total={total} pageSize={25} onPage={setPage}/></State>
    {history && <Dialog title={`Lịch sử lô ${history.lotNumber}`} close={() => setHistory(null)}>
      <p>{history.item} · {history.warehouse}</p><p>Biến động số lượng giữ nguyên theo lô gốc; số âm là lượng ra, số dương là lượng vào.</p>
      <State {...historyQuery}><div className="table-scroll"><table><thead><tr><th>Thời điểm</th><th>Nơi ghi nhận</th><th>Số lượng</th><th>Tham chiếu</th></tr></thead><tbody>
        {(historyQuery.data?.items || []).map((r: Row) => <tr key={r.entryId}><td data-label="Thời điểm">{timestamp(r.occurredAt)}</td><td data-label="Nơi ghi nhận">{({ WAREHOUSE: "Kho", STAFF: "Nhân viên" } as Record<string,string>)[r.kind] || r.kind}</td><td data-label="Số lượng">{r.quantity}</td><td data-label="Tham chiếu">{r.movementId ? `Giao dịch kho #${r.movementId}` : r.ledgerId ? `Sổ vật tư #${r.ledgerId}` : "—"}</td></tr>)}
      </tbody></table></div>{historyQuery.data?.total === 0 && <Empty>Chưa có biến động của lô.</Empty>}<Pager page={historyPage} total={historyQuery.data?.total || 0} pageSize={25} onPage={setHistoryPage}/></State>
    </Dialog>}
    {edit && <Dialog title={configuration ? `Cấu hình ${edit.name}` : `Xác minh ${edit.lotNumber}`} close={() => { if (!pending) setEdit(null); }}>
      <form onSubmit={async e => { e.preventDefault(); if (pending) return; setPending(true); setError(""); const f = new FormData(e.currentTarget);
        try { const body = configuration ? { branchId: branch, itemId: edit.itemId, rowVersion: edit.rowVersion, classification: f.get("classification"), expiryPolicy: f.get("expiryPolicy"), reason: f.get("reason") } :
          { rowVersion: edit.rowVersion, lotNumber: f.get("lotNumber"), receivedAt: new Date(String(f.get("receivedAt"))).toISOString(), expiresOn: f.get("expiresOn") || null, status: f.get("status"), reason: f.get("reason") };
          await api(configuration ? "inventory-lifecycle/profiles" : `inventory-lifecycle/batches/${edit.batchId}`, body, "PUT"); setEdit(null); bump(x => x + 1);
        } catch (error) { setError(message(error)); } finally { setPending(false); }
      }}>
        {configuration ? <><label>Phân loại<select name="classification" defaultValue={edit.classification}>{Object.entries(classes).map(([code,label]) => <option key={code} value={code}>{label}</option>)}</select></label>
          <label>Chính sách hạn dùng<select name="expiryPolicy" defaultValue={edit.expiryPolicy}>{Object.entries(expiry).map(([code,label]) => <option key={code} value={code}>{label}</option>)}</select></label></> : <>
          <label>Mã lô thực tế<input name="lotNumber" required maxLength={100} defaultValue={edit.lotNumber}/></label>
          <label>Thời điểm nhập thực tế<input name="receivedAt" type="datetime-local" required defaultValue={edit.receivedAt ? new Date(new Date(edit.receivedAt).getTime() - new Date().getTimezoneOffset()*60000).toISOString().slice(0,16) : ""}/></label>
          <label>Hạn dùng<input name="expiresOn" type="date" required={edit.expiryPolicy === "REQUIRED"} defaultValue={edit.expiresOn || ""}/></label>
          <label>Trạng thái<select name="status" defaultValue={edit.status === "LEGACY_REVIEW" ? "QUARANTINED" : edit.status}><option value="READY">Đã xác minh, được cấp</option><option value="QUARANTINED">Cách ly</option><option value="EXPIRED">Hết hạn</option></select></label></>}
        <label>Căn cứ xác minh / lý do<textarea name="reason" required maxLength={2000}/></label>
        <button className="primary" disabled={pending}>Lưu</button>
      </form>{error && <AlertPopup>{error}</AlertPopup>}
    </Dialog>}
  </section>;
}
