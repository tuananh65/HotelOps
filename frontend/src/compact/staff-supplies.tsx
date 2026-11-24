import { useState } from "react";
import { useAuth } from "./auth";
import { timestamp } from "./api";
import { Dialog, Empty, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { supplyMessage, supplyQuantity as qty, type SupplyAllocation, type SupplyLedger } from "./supply-api";
import "./supplies.css";

export type StaffSuppliesProps = {
  /** Parent supplies the real unified-request navigation after its route is wired. */
  onRequestRefill?: (allocation: SupplyAllocation) => void;
  staffNames?: Readonly<Record<number, string>>;
  unitNames?: Readonly<Record<number, string>>;
};
export default function StaffSupplies(props: StaffSuppliesProps) {
  const { branch } = useAuth();
  return <BranchSupplies key={branch} {...props} />;
}
function BranchSupplies({ onRequestRefill, staffNames = {}, unitNames = {} }: StaffSuppliesProps) {
  const { branch, can, profile } = useAuth();
  const ownId = Number(profile.user.fields.userId);
  const branchRead = can("STAFF_SUPPLY_VIEW", false), read = can("STAFF_SUPPLY_VIEW", true);
  const [revision, bump] = useState(0), [user, setUser] = useState(""), [status, setStatus] = useState("");
  const [selected, setSelected] = useState<SupplyAllocation | null>(null);
  const [filters, setFilters] = useState(false);
  const query = useQuery<SupplyAllocation[]>(read ? `supplies/allocations?branchId=${branch}${branchRead && user ? `&userId=${Number(user)}` : ""}` : null, revision);
  const rows = (query.data || []).filter(a => !status || a.status === status);
  const name = (id: number) => id === ownId ? profile.user.fields.fullName || "Tôi" : staffNames[id] || `Nhân viên ${id}`;
  return <div className="supply-page">
    <PageTitle><h1>{branchRead ? "Vật tư nhân viên" : "Vật tư của tôi"}</h1><p>Số lượng cấp, giữ cho công việc, đã dùng và hoàn trả theo từng ca</p></PageTitle>
    {!read ? <Empty>Bạn chưa được cấp quyền xem vật tư nhân viên.</Empty> : <>
      <PageToolbar>
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>Bộ lọc</button>
        <button onClick={() => bump(n => n + 1)}>Tải lại</button>
      </PageToolbar>
      {filters && <PageToolbar>
        {branchRead && <label>Nhân viên<select value={user} onChange={e => { setUser(e.target.value); setSelected(null); }}><option value="">Tất cả trong phạm vi</option>{Array.from(new Set((query.data || []).map(a => a.userId).concat(user ? [Number(user)] : []))).map(id => <option key={id} value={id}>{name(id)}</option>)}</select></label>}
        <label>Trạng thái<select value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả</option><option value="OPEN">Đang giữ vật tư</option><option value="SHORT">Thiếu vật tư</option><option value="CLOSED">Đã hoàn trả / đóng</option></select></label>
        <button onClick={() => { setUser(""); setStatus(""); setSelected(null); }}>Đặt lại</button>
      </PageToolbar>}
      <State {...query} retry={() => bump(n => n + 1)}>
        <p className="muted">Hiển thị tối đa 100 lượt cấp gần nhất. Số dư khác đơn vị được trình bày riêng, không cộng gộp.</p>
        {rows.map(a => <section className="supply-allocation" key={a.allocationId}>
          <div className="supply-section-heading"><div><h2>{name(a.userId)}</h2><small>Cấp lúc {timestamp(a.createdAt)}{a.closedAt && ` · Đóng lúc ${timestamp(a.closedAt)}`}</small></div><span className={`badge ${a.status === "CLOSED" ? "neutral" : a.status === "SHORT" ? "bad" : "good"}`}>{a.status === "CLOSED" ? "Đã hoàn trả / đóng" : a.status === "SHORT" ? "Thiếu vật tư" : "Đang giữ vật tư"}</span></div>
          <table className="supply-table supply-balances"><thead><tr><th>Vật tư</th><th>Cấp đầu ca</th><th>Cấp bổ sung</th><th>Đã dùng</th><th>Đã trả</th><th>Còn giữ</th><th>Đã giữ chỗ</th><th>Có thể dùng</th></tr></thead><tbody>{a.items.map(i => <tr key={i.balanceId}>
            <td data-label="Vật tư"><strong>{i.name}</strong><small>{unitNames[i.unitId] || `Đơn vị ${i.unitId}`}</small></td>
            <td data-label="Cấp đầu ca">{qty(i.initialIssued)}<small>Định mức: {qty(i.initialTarget)}</small></td><td data-label="Cấp bổ sung">{qty(i.additionalIssued)}</td><td data-label="Đã dùng">{qty(i.used)}</td><td data-label="Đã trả">{qty(i.returned)}</td><td data-label="Còn giữ">{qty(i.remaining)}</td><td data-label="Đã giữ chỗ"><span className="supply-reserved">{qty(i.reserved)}</span></td><td data-label="Có thể dùng"><strong className={i.available > 0 ? "supply-available" : "supply-none"}>{qty(i.available)}</strong></td>
          </tr>)}</tbody></table>
          {!a.items.length && <Empty>Chưa có vật tư được cấu hình cho lượt cấp này.</Empty>}
          <div className="supply-actions"><button onClick={() => setSelected(a)}>Lịch sử vật tư</button>{a.userId === ownId && a.status !== "CLOSED" && onRequestRefill && <button onClick={() => onRequestRefill(a)}>Yêu cầu bổ sung</button>}</div>
          {a.shortageReason && <p className="supply-shortage">{supplyMessage(a.shortageReason)}</p>}
        </section>)}
        {!rows.length && <Empty>Chưa có lượt cấp vật tư trong phạm vi đã chọn.</Empty>}
      </State>
      {!branchRead && <p className="muted">Vật tư được giữ cho công việc khi phân công, trừ khi báo hoàn thành và hoàn trả qua thao tác kết thúc ca trong Lịch làm việc.</p>}
    </>}
    {selected && <Dialog title={`Lịch sử vật tư · ${name(selected.userId)}`} close={() => setSelected(null)}><AllocationHistory allocation={selected} /></Dialog>}
  </div>;
}
function AllocationHistory({ allocation }: { allocation: SupplyAllocation }) {
  const [revision, bump] = useState(0);
  const query = useQuery<SupplyLedger[]>(`supplies/allocations/${allocation.allocationId}/history`, revision);
  const kinds: Record<string, string> = { INITIAL: "Cấp đầu ca", ADDITIONAL: "Cấp bổ sung", RESERVE: "Giữ cho công việc", RESERVED: "Giữ cho công việc", RELEASE: "Giải phóng giữ chỗ", USE: "Sử dụng", CONSUME: "Sử dụng", RETURN: "Hoàn trả" };
  return <div className="supply-page"><State {...query} retry={() => bump(n => n + 1)}>
    <p className="muted">Tối đa 1.000 giao dịch gần nhất của lượt cấp này.</p>
    <ol className="history">{(query.data || []).map(entry => <li key={entry.ledgerId}><strong>{kinds[entry.kind] || "Biến động vật tư"}</strong><span>{timestamp(entry.occurredAt)}</span><p>{allocation.items.find(i => i.itemId === entry.itemId)?.name || `Vật tư ${entry.itemId}`} · {qty(entry.quantity)}</p>{entry.workId && <a href={`#work?id=${entry.workId}`}>Xem công việc liên quan</a>}</li>)}</ol>
    {!query.data?.length && <Empty>Chưa có giao dịch vật tư.</Empty>}
  </State></div>;
}
