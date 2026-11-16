import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function ReplenishmentListView({ data }: { data: Values }) {
  const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
  return <section aria-label="Replenishment tracking">
    <p>Page {String(data.page)} — {String(data.total)} matching replenishments. Quantities are shown per item in its base unit.</p>
    {rows.length === 0 && <p>No matching replenishments.</p>}
    {rows.map(r => <section key={String(r.replenishmentId)} aria-label={String(r.replenishmentNo)}>
      <h3><Link to={`/operations/replenishments/${encodeURIComponent(String(r.replenishmentId))}`}>{String(r.replenishmentNo)}</Link></h3>
      <p>Status: {String(r.status)}; warehouse: {String(r.warehouseId)}; supplier: {String(r.supplierName ?? "Not specified")}; expected: {String(r.expectedOn ?? "Not specified")}</p>
      <table><caption>Requested, received and remaining quantities</caption>
        <thead><tr><th>Item</th><th>Base unit</th><th>Requested</th><th>Net received</th><th>Remaining</th></tr></thead>
        <tbody>{(Array.isArray(r.lines) ? r.lines.filter(isObject) : []).map(l => <tr key={String(l.lineNo)}>
          <td>{String(l.itemName)}</td><td>{String(l.unitName)}</td><td>{String(l.requestedBaseQty)}</td>
          <td>{String(l.netReceivedBaseQty)}</td><td>{String(l.remainingBaseQty)}</td>
        </tr>)}</tbody>
      </table>
      {r.status === "CLOSED" && <p>The remaining amount is closed and will not be received under this replenishment.</p>}
    </section>)}
  </section>;
}
