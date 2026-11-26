import { useEffect, useState } from "react";
import { api, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, Pager, State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";

const labels: Record<string, string> = { READY: "Sẵn sàng", STAFF_HOLDING: "Nhân viên giữ", IN_USE: "Tại phòng", DIRTY: "Cần vệ sinh", LAUNDRY: "Giặt là", DAMAGED: "Hư hỏng", IN_TRANSIT: "Đang điều chuyển", WRITTEN_OFF: "Đã ghi giảm", LEGACY_REVIEW: "Chờ đối soát thực tế" };
const next: Record<string, string[]> = { READY: ["STAFF_HOLDING", "IN_USE", "DAMAGED"], STAFF_HOLDING: ["IN_USE", "READY", "DAMAGED"], IN_USE: ["DIRTY", "DAMAGED"], DIRTY: ["LAUNDRY", "DAMAGED"], LAUNDRY: ["READY", "DAMAGED"], DAMAGED: ["READY"], LEGACY_REVIEW: ["IN_USE"] };

export default function ReusableInventory({ branch, work, initialTab = "positions" }: { branch: number; work?: Row; initialTab?: string }) {
  const { profile, can } = useAuth();
  const store = profile.roles.some(r => r.code === "STOREKEEPER" && r.branchId === branch) && can("STOCK_ISSUE_POST", false);
  const manager = profile.roles.some(r => r.code === "OPERATION_MANAGER" && r.branchId === branch) && can("ROOM_SUPPLY_CONFIG", false);
  const [revision, bump] = useState(0), [tab, setTab] = useState(manager && !work && initialTab === "write-offs" ? "positions" : initialTab);
  const [action, setAction] = useState<{ row: Row; type: string; key: string } | null>(null);
  const [status, setStatus] = useState(""), [place, setPlace] = useState("room");
  const [pending, setPending] = useState(false), [error, setError] = useState("");
  const query = useQuery(`reusable-inventory?branchId=${branch}`, revision);
  const destinations = useQuery(store ? `reusable-inventory/destinations?branchId=${branch}` : null);
  const extra = useQuery((store || manager) && tab !== "positions" ? `reusable-inventory/${tab}?branchId=${branch}` : null, revision);
  const [documentId, setDocumentId] = useState<number | null>(null);
  const [page, setPage] = useState(1);
  useEffect(() => { setPage(1); setAction(null); setDocumentId(null); setError(""); }, [branch, tab]);
  const extraRows: Row[] = extra.data || [];
  const currentPage = Math.min(page, Math.max(1, Math.ceil(extraRows.length / 25)));
  const visibleRows = extraRows.slice((currentPage - 1) * 25, currentPage * 25);
  const document = useQuery(documentId ? `documents/${documentId}` : null);
  const positions: Row[] = (query.data?.positions || []).filter((r: Row) => !work || r.roomId === work.roomId || r.userId === profile.user.fields.userId);
  const legacyPositions = work ? positions.filter(r => r.status === "LEGACY_REVIEW" && r.quantity > 0) : [];
  const transitions = (r: Row) => (next[r.status] || []).filter(s => store || !!work && ["IN_USE", "DIRTY", "DAMAGED"].includes(s) && r.status !== "LEGACY_REVIEW");
  const begin = (row: Row, type: string) => {
    const target = transitions(row)[0] || "";
    setStatus(target); setPlace(target === "STAFF_HOLDING" ? "staff" : ["READY", "LAUNDRY"].includes(target) ? "warehouse" : "room");
    setAction({ row, type, key: crypto.randomUUID() }); setError("");
  };
  const locationType = status === "STAFF_HOLDING" ? "staff" : ["READY", "LAUNDRY"].includes(status) ? "warehouse" : status === "IN_USE" ? "room" : place;
  const options: Row[] = destinations.data?.[locationType === "staff" ? "staff" : locationType === "room" ? "rooms" : "warehouses"] || [];
  return <section>
    <h2>Vật tư tái sử dụng</h2>
    {work?.roomCapacity && <p>Sức chứa tối đa của phòng: <strong>{work.roomCapacity} khách</strong>. Số vật tư cấp được tính theo sức chứa và định mức mỗi khách.</p>}
    <p>Cấp, thu hồi và giặt không làm giảm tổng sở hữu. Vật tư hỏng chờ OM xác nhận ghi giảm.</p>
    {(store || manager) && <div className="actions">{[["positions", manager && !work ? "Tổng hợp" : "Số dư và vị trí"], ...(!manager || work ? [["write-offs", "Đề nghị ghi giảm"]] : []), ["history", "Lịch sử luân chuyển"]].map(([value, label]) => <button key={value} aria-pressed={tab === value} onClick={() => setTab(value)}>{label}</button>)}</div>}
    {tab === "positions" ? <State {...query}>
      {!work && <div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Thuộc sở hữu</th><th>Sẵn sàng</th><th>Chờ đối soát</th><th>Đã ghi giảm</th></tr></thead><tbody>{(query.data?.totals || []).map((r: Row) => <tr key={r.itemId}><td>{r.name}</td><td>{r.owned}</td><td>{r.ready}</td><td>{r.unresolvedLegacy}</td><td>{r.writtenOff}</td></tr>)}</tbody></table></div>}
      {(!manager || work) && <div className="table-scroll"><table><thead><tr><th>Vật tư / Vị trí</th><th>Trạng thái</th><th>Số lượng</th><th>Thao tác</th></tr></thead><tbody>{positions.filter(r => r.quantity > 0 && (!work || r.status !== "LEGACY_REVIEW")).map(r => <tr key={r.positionId}>
        <td>{r.name}<small>{r.roomId ? "Phòng " : ""}{r.location}</small>{(query.data?.shelfPolicies || []).filter((p: Row) => p.itemId === r.itemId && p.deadline).map((p: Row) => <small key={p.itemId}>Hạn kiểm tra tồn kho nội bộ: {p.deadline}{p.overdue ? " · Quá hạn, chờ đối soát" : ""}</small>)}</td><td>{labels[r.status]}</td><td>{r.quantity}{r.pendingWriteOff > 0 && <small>Chờ ghi giảm: {r.pendingWriteOff}</small>}</td>
        <td>{(store || work?.status === "IN_PROGRESS") && transitions(r).length > 0 && <button onClick={() => begin(r, "move")}>Luân chuyển</button>}{store && r.status === "DAMAGED" && r.quantity > r.pendingWriteOff && <button onClick={() => begin(r, "write-off")}>Đề nghị ghi giảm</button>}</td>
      </tr>)}</tbody></table></div>}
      {legacyPositions.length > 0 && <details><summary>Số dư cũ chờ kiểm kê thực tế</summary><p>Các số lượng dưới đây chưa được xác minh, không phải định mức phòng và không được tính là vật tư có thể sử dụng.</p><ul>{legacyPositions.map(r => <li key={r.positionId}>{r.name}: {r.quantity} · Phòng {r.location}</li>)}</ul></details>}
    </State> : <State {...extra}><div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Số lượng</th><th>{tab === "history" ? "Thời điểm / Nội dung" : "Trạng thái / Lý do"}</th><th>Thao tác</th></tr></thead><tbody>{visibleRows.map((r: Row) => <tr key={r.writeOffId || r.reusableMovementId}>
      <td>{r.name}</td><td>{r.quantity}</td><td>{tab === "history" ? timestamp(r.occurredAt) : ({ PENDING: "Chờ OM xác nhận", APPROVED: "Đã ghi giảm", REJECTED: "Đã từ chối" } as Record<string,string>)[r.status]}{tab === "history" && <><small>{({MOVE:"Luân chuyển",RECEIPT:"Nhập mới",HANDOVER:"Bàn giao",REPLACEMENT:"Thay thế",WRITE_OFF:"Ghi giảm",LAUNDRY:"Giặt là",DAMAGE:"Ghi nhận hỏng",RECOVERY:"Phục hồi",LEGACY_OPENING:"Số dư đối soát"} as Record<string,string>)[r.kind]}</small><small>{r.source ? `${r.source.locationKey} · ${labels[r.source.status]}` : "Nhận mới"} → {r.destination?.locationKey} · {labels[r.destination?.status]}</small></>}<small>{r.reason}</small>{r.decisionReason && <small>{r.decisionReason}</small>}</td>
      <td>{r.documentId && <button onClick={() => setDocumentId(r.documentId)}>Chứng từ</button>}{manager && r.status === "PENDING" && r.createdBy !== profile.user.fields.userId && <><button className="primary" onClick={() => begin(r, "approve")}>Xác nhận</button><button className="danger" onClick={() => begin(r, "reject")}>Từ chối</button></>}</td>
    </tr>)}</tbody></table></div><Pager page={currentPage} total={extraRows.length} pageSize={25} onPage={setPage}/></State>}
    {documentId && <Dialog title="Chứng từ bàn giao" close={() => setDocumentId(null)}><State {...document}>{document.data && <><p>{document.data.document.fields.documentNo}</p><p>{document.data.document.fields.reason}</p><p>{timestamp(document.data.document.fields.postedAt)}</p><ul>{document.data.lines.map((line: Row) => <li key={line.fields.lineId}>Vật tư #{line.fields.itemId}: {line.fields.actualQty} đơn vị</li>)}</ul></>}</State></Dialog>}
    {action && <Dialog title={action.type === "move" ? `Luân chuyển ${action.row.name}` : action.type === "write-off" ? "Đề nghị ghi giảm" : action.type === "approve" ? "Xác nhận ghi giảm" : "Từ chối ghi giảm"} close={() => { if (!pending) setAction(null); }}>
      <form onSubmit={async e => {
        e.preventDefault(); if (pending) return; setPending(true); setError("");
        const f = new FormData(e.currentTarget), base = { rowVersion: action.row.rowVersion, operationKey: action.key, reason: String(f.get("reason") || "") };
        try {
          if (["approve", "reject"].includes(action.type)) await api(`reusable-inventory/write-offs/${action.row.writeOffId}/decision`, { ...base, approve: action.type === "approve" });
          else if (action.type === "write-off") await api(`reusable-inventory/positions/${action.row.positionId}/write-off`, { ...base, quantity: Number(f.get("quantity")) });
          else {
            const locationId = Number(f.get("location"));
            await api(`reusable-inventory/positions/${action.row.positionId}/move`, { ...base, quantity: Number(f.get("quantity")), status,
              warehouseId: !work && locationType === "warehouse" ? locationId : null, userId: !work && locationType === "staff" ? locationId : null,
              roomId: work ? work.roomId : locationType === "room" ? locationId : null, workId: work?.workId || null, replace: f.get("replace") === "on" });
          }
          setAction(null); bump(n => n + 1);
        } catch (err) { setError(message(err)); } finally { setPending(false); }
      }}>
        <p>{action.row.name}{action.type === "approve" && ` · ${action.row.quantity} đơn vị sẽ được ghi giảm khỏi tổng sở hữu.`}</p>
        {["move", "write-off"].includes(action.type) && <label>Số lượng<input name="quantity" type="number" min="0.0001" step="0.0001" max={action.row.quantity - action.row.pendingWriteOff} required defaultValue="1"/></label>}
        {action.type === "move" && <>
          <label>Chuyển sang<select value={status} onChange={e => setStatus(e.target.value)}>{transitions(action.row).map(s => <option key={s} value={s}>{labels[s]}</option>)}</select></label>
          {!work && <>{["DIRTY", "DAMAGED"].includes(status) && <label>Nơi nhận<select value={place} onChange={e => setPlace(e.target.value)}><option value="room">Phòng</option><option value="warehouse">Kho</option><option value="staff">Nhân viên</option></select></label>}
            <label>{locationType === "room" ? "Phòng nhận" : locationType === "staff" ? "Nhân viên nhận" : "Kho nhận"}<select name="location" required key={locationType} defaultValue={action.row.status === "LEGACY_REVIEW" ? action.row.roomId : ""}><option value="">Chọn nơi nhận</option>{options.filter(r => action.row.status !== "LEGACY_REVIEW" || r.id === action.row.roomId).map(r => <option key={r.id} value={r.id}>{r.name}</option>)}</select></label><State {...destinations}>{null}</State></>}
          {status === "IN_USE" && ["READY", "STAFF_HOLDING"].includes(action.row.status) && <label><input name="replace" type="checkbox"/>Thay đồ đang dùng: đồng thời thu hồi cùng số lượng sang cần vệ sinh</label>}
        </>}
        <label>{action.type === "approve" ? "Ghi chú (không bắt buộc)" : "Lý do / Kết quả kiểm tra"}<textarea name="reason" required={action.type !== "approve"} maxLength={2000}/></label>
        <div className="actions"><button className="primary" disabled={pending}>Xác nhận</button><button type="button" className="danger" disabled={pending} onClick={() => setAction(null)}>Hủy</button></div>
      </form>{error && <AlertPopup>{error}</AlertPopup>}
    </Dialog>}
  </section>;
}
