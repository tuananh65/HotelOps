import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function ReplenishmentWaitingRequests({ record }: { record: Values }) {
  const rows = Array.isArray(record.waitingRequests) ? record.waitingRequests.filter(isObject) : [];
  return <section aria-label="Material requests waiting for replenishment items">
    <h3>Material requests waiting for these items</h3>
    {record.waitingRequestsVisible !== true
      ? <p>Independent material request viewing permission is required.</p>
      : <>
        <p>Approved quantities still awaiting issue in this warehouse, within your viewing scope. Receipt does not reserve stock or complete these requests. Quantities use each request line's unit.</p>
        {rows.length === 0 ? <p>No waiting material requests in your viewing scope.</p>
          : <table><thead><tr><th>Request</th><th>Status</th><th>Item</th><th>Unit</th><th>Approved</th><th>Issued</th><th>Still waiting</th></tr></thead>
            <tbody>{rows.map(r => <tr key={String(r.requestLineId)}>
              <td><Link to={`/operations/material-requests/${encodeURIComponent(String(r.requestId))}`}>{String(r.requestNo)}</Link></td>
              <td>{String(r.status)}</td><td>{String(r.itemName)}</td><td>{String(r.unitName)}</td>
              <td>{String(r.approvedQty)}</td><td>{String(r.fulfilledQty)}</td><td>{String(r.remainingQty)}</td>
            </tr>)}</tbody></table>}
      </>}
  </section>;
}
