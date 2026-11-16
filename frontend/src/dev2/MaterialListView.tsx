import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaterialListView({ data }: { data: Values }) {
  const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
  return <section aria-label="Material request tracking">
    <p>Page {String(data.page)}; {String(data.total)} matching requests, newest first.</p>
    {rows.length === 0 && <p>No matching material requests.</p>}
    {rows.map(request => <section key={String(request.requestId)}>
      <h3><Link to={`/operations/material-requests/${encodeURIComponent(String(request.requestId))}`}>{String(request.requestNo)}</Link></h3>
      <p>Status: {String(request.status)}; requester: {String(request.requesterId)}; assigned approver: {String(request.assignedApproverUserId ?? "Not recorded")}</p>
      <p>Created: {String(request.createdAt)}; needed: {String(request.neededAt ?? "Not recorded")}</p>
      <table><caption>Requested, approved and issued quantities per material and unit</caption>
        <thead><tr><th>Material</th><th>Unit</th><th>Requested</th><th>Approved</th><th>Issued</th></tr></thead>
        <tbody>{(Array.isArray(request.quantities) ? request.quantities.filter(isObject) : []).map(line => <tr key={String(line.requestLineId)}>
          <td>{String(line.itemName)}</td><td>{String(line.unitName)}</td><td>{String(line.requestedQty)}</td>
          <td>{String(line.approvedQty ?? "Not approved")}</td><td>{String(line.fulfilledQty)}</td>
        </tr>)}</tbody>
      </table>
    </section>)}
  </section>;
}
