import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const show = (v: unknown) => typeof v === "string" && v.trim() ? v : "N/A";
const time = (v: unknown) => {
  if (typeof v !== "string" || !/(?:Z|[+-]\d{2}:\d{2})$/.test(v)) return "N/A";
  const date = new Date(v);
  return Number.isFinite(date.getTime()) ? new Intl.DateTimeFormat("en-GB", {
    timeZone: "Asia/Ho_Chi_Minh", dateStyle: "short", timeStyle: "medium"
  }).format(date) : "N/A";
};

export function IncidentListView({ data, canDetail }: { data: Values; canDetail: boolean }) {
  if (!Array.isArray(data.items) || !positive(data.page) || !positive(data.pageSize)
    || typeof data.totalCount !== "number" || !Number.isSafeInteger(data.totalCount) || data.totalCount < 0
    || !data.items.every(r => isObject(r) && positive(r.incidentId) && positive(r.branchId)
      && typeof r.incidentNo === "string" && typeof r.incidentTitle === "string" && typeof r.status === "string"
      && (r.isOverdue === null || typeof r.isOverdue === "boolean")))
    return <p role="alert">Incident results could not be read. Reload the list.</p>;
  const rows = data.items as Values[];
  const labels: Record<string, string> = { branchId: "Branch", departmentId: "Work department", roomId: "Room", areaId: "Area", assetId: "Asset", categoryId: "Category", severityId: "Severity", priorityId: "Priority", status: "Status", assigneeId: "Assignee", overdue: "Overdue", detectedFrom: "Detected from", detectedTo: "Detected to", keyword: "Keyword" };
  const filters = isObject(data.appliedFilters) ? Object.entries(data.appliedFilters).filter(([key, value]) => key in labels && value !== null && value !== undefined && value !== "") : [];
  return <section aria-label="Incident results">
    <p>Applied filters: {filters.length ? filters.map(([key, value]) => labels[key] + ": " + String(value)).join("; ") : "None"}.</p>
    <p>{data.totalCount} matching incidents; page {data.page}. Times shown in UTC+7.</p>
    <p>{data.sortOrder === "RESOLUTION_DEADLINE_ASC" ? "Ordered by resolution deadline, earliest first; missing deadlines last."
      : data.sortOrder === "REPORTED_AT_DESC" ? "SLA ordering unavailable. Most recently reported incidents first." : "Ordering unavailable."}
      {" "}Status evaluated at {time(data.evaluatedAtUtc)}.</p>
    {rows.length === 0 ? <p>No incidents match the submitted filters and your access scope.</p> :
      <div style={{ overflowX: "auto" }}><table><thead><tr>
        <th>Reference / title</th><th>Branch / location / asset</th><th>Category</th><th>Severity / priority</th>
        <th>Status</th><th>Work department</th><th>Current assignee</th><th>Response deadline</th><th>Resolution deadline</th><th>Overdue</th>
      </tr></thead><tbody>{rows.map(r => <tr key={String(r.incidentId)}>
        <td>{canDetail ? <Link to={"/operations/incidents/" + r.incidentId + "?branchId=" + r.branchId + "&action=A004A-023"}>{show(r.incidentNo)}</Link> : show(r.incidentNo)}<br />{show(r.incidentTitle)}</td>
        <td>{[r.branchName, r.roomNumber, r.areaName, r.assetName].filter(v => typeof v === "string" && v.trim()).join(" / ") || "N/A"}</td>
        <td>{show(r.categoryName)}</td><td>{show(r.severityName)} / {show(r.priorityName)}</td><td>{show(r.status)}</td>
        <td>{show(r.departmentName)}</td><td>{r.assignmentUnavailableReason === "UNASSIGNED" ? "Unassigned" : show(r.assigneeName)}</td>
        <td>{time(r.responseDeadlineAt)}</td><td>{time(r.resolutionDeadlineAt)}{r.slaUnavailableReason != null && <small> SLA data unavailable or incomplete.</small>}</td>
        <td>{r.isOverdue === true ? "Overdue" : r.isOverdue === false ? "No" : "N/A"}</td>
      </tr>)}</tbody></table></div>}
  </section>;
}
