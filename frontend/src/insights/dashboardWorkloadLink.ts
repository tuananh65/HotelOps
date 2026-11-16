import type { Dashboard } from "./dashboardApi";
import type { WorkloadFilters } from "../operationalTasks/workload";

const keys = new Set(["openTasks", "overdueTasks", "closedTasksInPeriod", "taskEffortHours", "taskProvisionalHours", "taskOpenIntervals"]);
const id = (value: number) => Number.isSafeInteger(value) && value > 0;
const date = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value)
  && Number.isFinite(Date.parse(value)) && new Date(value).toISOString().slice(0, 10) === value;

export function dashboardWorkloadLink(filters: Dashboard["filters"], key: string): string | null {
  if (!keys.has(key) || filters.branchIds.length !== 1 || !id(filters.branchIds[0])
    || filters.calendar !== "UTC" || !date(filters.from) || !date(filters.to)
    || filters.to < filters.from || filters.from < "1900-01-01"
    || Date.parse(filters.to) - Date.parse(filters.from) > 365 * 86400000
    || filters.departmentId !== null && !id(filters.departmentId)) return null;
  const query = new URLSearchParams({ branchId: String(filters.branchIds[0]), view: "workload", calendar: "UTC", from: filters.from, to: filters.to });
  if (filters.departmentId !== null) query.set("departmentId", String(filters.departmentId));
  if (key === "overdueTasks") query.set("overdueOnly", "true");
  if (key === "closedTasksInPeriod") query.set("status", "CLOSED");
  return `/operational-tasks?${query}`;
}

export function dashboardWorkloadFilters(params: URLSearchParams): WorkloadFilters | undefined {
  if (params.get("view") !== "workload" || params.get("calendar") !== "UTC") return undefined;
  const from = params.get("from") ?? "", to = params.get("to") ?? "";
  const department = params.get("departmentId"), status = params.get("status"), overdue = params.get("overdueOnly");
  if (!date(from) || !date(to) || from > to || department !== null && (!/^[1-9]\d*$/.test(department) || !id(Number(department)))
    || status !== null && status !== "CLOSED" || overdue !== null && overdue !== "true") return undefined;
  return { from, to, calendar: "UTC", ...(department === null ? {} : { departmentId: Number(department) }),
    ...(status === null ? {} : { status }), ...(overdue === null ? {} : { overdueOnly: true }) };
}
