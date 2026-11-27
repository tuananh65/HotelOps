import { useState } from "react";
import { timestamp } from "./api";
import { State, useQuery } from "./ui";
import { supplyQuantity, type SupplyAllocation, type SupplyLedger } from "./supply-api";

export default function ShiftSupplyBalances({ branch, userId, assignmentIds, activeId, pendingReturnIds = [], revision }: {
  branch: number; userId: number; assignmentIds: number[]; activeId?: number; pendingReturnIds?: number[]; revision: number;
}) {
  const [refresh, setRefresh] = useState(0);
  const query = useQuery<SupplyAllocation[]>(`supplies/allocations?branchId=${branch}&userId=${userId}`, revision + refresh);
  const rows = (query.data || []).filter(row => assignmentIds.includes(row.assignmentId) || row.assignmentId === activeId || pendingReturnIds.includes(row.assignmentId));
  return <details className="panel"><summary>Vật tư theo ca · Đã cấp, đã dùng, đã trả</summary>
    <State {...query} retry={() => setRefresh(value => value + 1)}>
      {rows.map(row => <Allocation key={row.allocationId} allocation={row} pendingReturn={pendingReturnIds.includes(row.assignmentId)} active={row.assignmentId === activeId && row.status !== "CLOSED"} revision={revision + refresh} />)}
      {!rows.length && <p>Chưa có lượt cấp vật tư trong các ca đang xem.</p>}
    </State>
  </details>;
}
function Allocation({ allocation, active, pendingReturn, revision }: { allocation: SupplyAllocation; active: boolean; pendingReturn: boolean; revision: number }) {
  const [showHistory, setShowHistory] = useState(false);
  const history = useQuery<SupplyLedger[]>(showHistory ? `supplies/allocations/${allocation.allocationId}/history` : null, revision);
  const kinds: Record<string, string> = { INITIAL: "Cấp đầu ca", ADDITIONAL: "Cấp bổ sung", RESERVE: "Giữ chỗ cũ", RELEASE: "Giải phóng giữ chỗ", USE: "Tiêu hao", RETURN: "Hoàn trả" };
  return <section>
    <h3>Vật tư ca · {timestamp(allocation.createdAt)}</h3>
    {allocation.status !== "CLOSED" && allocation.returnDueAt && <p>Dự kiến hoàn trả: {timestamp(allocation.returnDueAt)}. Bộ ca tiếp theo được cấp sau khi hoàn trả xong.</p>}
    <p>{allocation.status === "CLOSED" ? `Đã đóng lượt cấp · ${timestamp(allocation.closedAt ?? undefined)}` : pendingReturn ? "Ca đã kết thúc · Chờ tự động hoàn trả vật tư" : active ? "Ca đang hoạt động" : "Lượt cấp chưa đóng"}</p>
    <div className="table-scroll"><table><thead><tr><th>Vật tư</th><th>Đã cấp</th><th>Đã dùng</th><th>Đã trả</th><th>Còn giữ</th></tr></thead>
      <tbody>{allocation.items.map(item => <tr key={item.balanceId}>
        <td data-label="Vật tư">{item.name}</td><td data-label="Đã cấp">{supplyQuantity(item.initialIssued + item.additionalIssued)}</td>
        <td data-label="Đã dùng">{supplyQuantity(item.used)}</td><td data-label="Đã trả">{supplyQuantity(item.returned)}</td><td data-label="Còn giữ">{supplyQuantity(item.remaining)}</td>
      </tr>)}</tbody></table></div>
    {!!allocation.reusableItems?.length && <ul>{allocation.reusableItems.map(item => <li key={item.positionId}>{item.name}: {supplyQuantity(item.quantity)} · {({STAFF_HOLDING:"Đồ sạch đang giữ",DIRTY:"Đồ đã dùng, chưa khả dụng",DAMAGED:"Đồ hỏng, chưa khả dụng"} as Record<string,string>)[item.status] || item.status}</li>)}</ul>}
    {active && <p><a href="#requests?create=CONSUMABLE_REFILL_REQUEST">Yêu cầu bổ sung vật tư trong giờ ca</a></p>}
    <button type="button" aria-expanded={showHistory} onClick={() => setShowHistory(value => !value)}>Lịch sử vật tư</button>
    {showHistory && <State {...history}><ol>{(history.data || []).map(entry => <li key={entry.ledgerId}>
      {timestamp(entry.occurredAt)} · {kinds[entry.kind] || entry.kind} · {allocation.items.find(item => item.itemId === entry.itemId)?.name || "Vật tư"}: {supplyQuantity(entry.quantity)}
    </li>)}</ol>{!history.data?.length && <p>Chưa có giao dịch.</p>}</State>}
  </section>;
}
