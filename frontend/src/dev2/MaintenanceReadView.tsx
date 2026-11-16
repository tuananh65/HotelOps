import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

const show = (v: unknown) => typeof v === "string" || typeof v === "number" ? String(v) : "Unavailable";
const target = (row: Values) => [row.roomNumber && `Room ${row.roomNumber}`, row.areaName,
  row.assetCode && `${row.assetCode}${row.assetName ? " ? " + row.assetName : ""}`]
  .filter(v => typeof v === "string" && v.length > 0).join(" / ") || "Target unavailable";
const time = (v: unknown) => {
  if (typeof v !== "string") return "Unavailable";
  const date = new Date(/(?:Z|[+-]\d{2}:\d{2})$/.test(v) ? v : v + "Z");
  return Number.isNaN(date.getTime()) ? "Unavailable" : date.toLocaleString();
};

export function MaintenanceReadView({ data, detail, canDetail }: { data: Values; detail: boolean; canDetail: boolean }) {
  if (!detail) {
    const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
    return <section aria-label="Maintenance work results">
      <p>{show(data.totalCount)} matching records; page {show(data.page)} of {show(data.totalPages)}.</p>
      {rows.length === 0 ? <p>No maintenance work matches your filters and access scope.</p> :
        <table><thead><tr><th>Reference</th><th>Title</th><th>Source</th><th>Location / asset</th><th>Priority</th><th>Assignee</th><th>Status</th><th>Planned end</th></tr></thead>
          <tbody>{rows.map((row, index) => <tr key={show(row.maintenanceWorkId) + ":" + index}>
            <td>{canDetail && typeof row.maintenanceWorkId === "number" && Number.isSafeInteger(row.maintenanceWorkId) && row.maintenanceWorkId > 0
              ? <Link to={`/operations/maintenance/${row.maintenanceWorkId}?action=A004A-036`}>{show(row.workNo)}</Link> : show(row.workNo)}</td>
            <td>{show(row.title)}</td><td>{show(row.source)}</td><td>{target(row)}</td><td>{show(row.priorityName)}</td><td>{show(row.assigneeName ?? row.assigneeId)}</td><td>{show(row.status)}</td><td>{time(row.plannedEndAt)}</td>
          </tr>)}</tbody></table>}
    </section>;
  }
  const logs = Array.isArray(data.maintenanceWorkLogs) ? data.maintenanceWorkLogs.filter(isObject) : [];
  const usages = logs.flatMap(log => Array.isArray(log.materialUsages) ? log.materialUsages.filter(isObject) : []);
  return <section aria-label="Maintenance work detail">
    <h3>{show(data.workNo)} — {show(data.title)}</h3>
    <dl><dt>Status</dt><dd>{show(data.status)}</dd><dt>Assignee</dt><dd>{show(data.assigneeName ?? data.assigneeId)}</dd>
      <dt>Source</dt><dd>{show(data.source)}</dd><dt>Location / asset</dt><dd>{target(data)}</dd>
      <dt>Priority</dt><dd>{show(data.priorityName)}</dd>
      <dt>Instruction</dt><dd>{show(data.instruction)}</dd><dt>Planned start</dt><dd>{time(data.plannedStartAt)}</dd>
      <dt>Planned end</dt><dd>{time(data.plannedEndAt)}</dd><dt>Actual start</dt><dd>{time(data.actualStartAt)}</dd>
      <dt>Resolution</dt><dd>{show(data.resolutionNote)}</dd><dt>Resolved at</dt><dd>{time(data.resolvedAt)}</dd></dl>
    <h4>Work history</h4>
    {logs.length === 0 ? <p>No work logs recorded.</p> : <ol>{logs.map((log, index) => <li key={show(log.logId) + ":" + index}>
      {time(log.loggedAt)} — {show(log.status)} — {show(log.workNote)} ({show(log.spentMinutes)} minutes)
    </li>)}</ol>}
    <h4>Material usage</h4>
    {data.materialUsageAvailable !== true ? <p>Material usage is unavailable: current material/inventory access and source document integrity must both be verified.</p>
      : usages.length === 0 ? <p>No material usage recorded.</p>
      : <table><thead><tr><th>Item ID</th><th>Used quantity</th><th>Unit ID</th><th>Base quantity</th><th>Base unit ID</th><th>Issue document</th><th>Material request</th></tr></thead>
        <tbody>{usages.map((usage, index) => <tr key={show(usage.usageId) + ":" + index}>
          <td>{show(usage.itemId)}</td><td>{show(usage.usedQty)}</td><td>{show(usage.uomId)}</td>
          <td>{show(usage.usedBaseQty)}</td><td>{show(usage.baseUomId)}</td><td>{show(usage.documentNo)}</td>
          <td>{typeof usage.requestId === "number" && Number.isSafeInteger(usage.requestId) && usage.requestId > 0
            ? <Link to={`/operations/material-requests/${usage.requestId}?action=A004A-038`}>{usage.requestId}</Link> : "Unavailable"}</td>
        </tr>)}</tbody></table>}
  </section>;
}
