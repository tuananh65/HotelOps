import type { Dashboard } from "./dashboardApi";
import type { WorkloadFilters } from "../operationalTasks/workload";

const keys = new Set(["openTasks", "overdueTasks", "closedTasksInPeriod", "averageTaskCompletionHours", "taskEffortHours", "taskProvisionalHours", "taskOpenIntervals"]);
const id = (value: number) => Number.isSafeInteger(value) && value > 0;
const date = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value)
  && Number.isFinite(Date.parse(value)) && new Date(value).toISOString().slice(0, 10) === value;

export function dashboardWorkloadLink(filters: Dashboard["filters"], key: string): string | null {
  if (!keys.has(key) || filters.branchIds.length === 0 || filters.branchIds.some(branch => !id(branch))
    || new Set(filters.branchIds).size !== filters.branchIds.length
    || filters.calendar !== "UTC" || !date(filters.from) || !date(filters.to)
    || filters.to < filters.from || filters.from < "0001-01-01" || filters.to === "9999-12-31"
    || filters.departmentId !== null && !id(filters.departmentId)) return null;
  const query = new URLSearchParams({ view: "workload", calendar: "UTC", from: filters.from, to: filters.to });
  if (filters.branchIds.length === 1) query.set("branchId", String(filters.branchIds[0]));
  else query.set("workloadBranches", filters.branchIds.join(","));
  if (filters.departmentId !== null) query.set("departmentId", String(filters.departmentId));
  if (key === "overdueTasks") query.set("overdueOnly", "true");
  if (key === "closedTasksInPeriod" || key === "averageTaskCompletionHours") query.set("status", "CLOSED");
  if (key === "openTasks") query.set("taskSet", "OPEN");
  if (key === "overdueTasks") query.set("taskSet", "OVERDUE");
  if (key === "closedTasksInPeriod" || key === "averageTaskCompletionHours") query.set("taskSet", "CLOSED_IN_PERIOD");
  return `/operational-tasks?${query}`;
}

export function dashboardWorkloadFilters(params: URLSearchParams): WorkloadFilters | undefined {
  if (params.get("view") !== "workload" || params.get("calendar") !== "UTC") return undefined;
  const from = params.get("from") ?? "", to = params.get("to") ?? "";
  const department = params.get("departmentId"), status = params.get("status"), overdue = params.get("overdueOnly");
  const taskSet = params.get("taskSet");
  if (!date(from) || !date(to) || from > to || from < "0001-01-01" || to === "9999-12-31"
    || department !== null && (!/^[1-9]\d*$/.test(department) || !id(Number(department)))
    || status !== null && status !== "CLOSED" || overdue !== null && overdue !== "true"
    || taskSet !== null && !["OPEN", "OVERDUE", "CLOSED_IN_PERIOD"].includes(taskSet)) return undefined;
  return { from, to, calendar: "UTC", ...(department === null ? {} : { departmentId: Number(department) }),
    ...(status === null ? {} : { status }), ...(overdue === null ? {} : { overdueOnly: true }),
    ...(taskSet === null ? {} : { taskSet: taskSet as WorkloadFilters["taskSet"] }) };
}

export function dashboardWorkloadBranches(params: URLSearchParams): number[] | undefined {
  if (!params.has("workloadBranches") || params.has("branchId") || params.has("taskId")
    || !dashboardWorkloadFilters(params)) return undefined;
  const values = params.get("workloadBranches")!.split(",");
  if (values.length < 2 || values.some(value => !/^[1-9]\d*$/.test(value) || !id(Number(value)))) return undefined;
  const branches = values.map(Number);
  return new Set(branches).size === branches.length ? branches : undefined;
}
