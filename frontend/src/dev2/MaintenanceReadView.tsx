import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

const show = (v: unknown) => typeof v === "string" || typeof v === "number" ? String(v) : "Unavailable";
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
        <table><thead><tr><th>Reference</th><th>Title</th><th>Source</th><th>Assignee</th><th>Status</th><th>Planned end</th></tr></thead>
          <tbody>{rows.map((row, index) => <tr key={show(row.maintenanceWorkId) + ":" + index}>
            <td>{canDetail && typeof row.maintenanceWorkId === "number" && Number.isSafeInteger(row.maintenanceWorkId) && row.maintenanceWorkId > 0
              ? <Link to={`/operations/maintenance/${row.maintenanceWorkId}?action=A004A-036`}>{show(row.workNo)}</Link> : show(row.workNo)}</td>
            <td>{show(row.title)}</td><td>{show(row.source)}</td><td>{show(row.assigneeId)}</td><td>{show(row.status)}</td><td>{time(row.plannedEndAt)}</td>
          </tr>)}</tbody></table>}
    </section>;
  }
  const logs = Array.isArray(data.maintenanceWorkLogs) ? data.maintenanceWorkLogs.filter(isObject) : [];
  return <section aria-label="Maintenance work detail">
    <h3>{show(data.workNo)} — {show(data.title)}</h3>
    <dl><dt>Status</dt><dd>{show(data.status)}</dd><dt>Assignee</dt><dd>{show(data.assigneeId)}</dd>
      <dt>Instruction</dt><dd>{show(data.instruction)}</dd><dt>Planned start</dt><dd>{time(data.plannedStartAt)}</dd>
      <dt>Planned end</dt><dd>{time(data.plannedEndAt)}</dd><dt>Actual start</dt><dd>{time(data.actualStartAt)}</dd>
      <dt>Resolution</dt><dd>{show(data.resolutionNote)}</dd><dt>Resolved at</dt><dd>{time(data.resolvedAt)}</dd></dl>
    <h4>Work history</h4>
    {logs.length === 0 ? <p>No work logs recorded.</p> : <ol>{logs.map((log, index) => <li key={show(log.logId) + ":" + index}>
      {time(log.loggedAt)} — {show(log.status)} — {show(log.workNote)} ({show(log.spentMinutes)} minutes)
    </li>)}</ol>}
    {data.materialUsageAvailable !== true && <p>Material usage and stock references are unavailable in this view. They require separate material and inventory authorization.</p>}
  </section>;
}
