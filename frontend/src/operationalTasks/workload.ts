import { taskWorkflowStates } from "./api";
import { planningRequest } from "./planning";

export interface WorkloadFilters {
  taskSet?: "OPEN" | "OVERDUE" | "CLOSED_IN_PERIOD";
  calendar?: "UTC" | "Asia/Ho_Chi_Minh";
  from?: string;
  to?: string;
  departmentId?: number;
  employeeId?: number;
  priorityId?: number;
  status?: string;
  deadlineFrom?: string;
  deadlineTo?: string;
  overdueOnly?: boolean;
  search?: string;
}
export interface WorkloadEmployee {
  userId: number;
  name: string;
  employeeCode: string;
  openTasks: number;
  overdueTasks: number;
  closedTasks: number;
  closedEffortSeconds: number;
  provisionalSeconds: number;
}
interface Snapshot {
  calendar?: "UTC" | "Asia/Ho_Chi_Minh";
  branchId: number;
  from: string;
  to: string;
  asOf: string;
  page: number;
  pageSize: number;
}
export interface WorkloadPage extends Snapshot {
  items: WorkloadEmployee[];
  totalCount: number;
  messageCode: "MSG10" | null;
}
export interface WorkloadTask {
  taskId: number;
  code: string;
  title: string;
  departmentId: number;
  department: string;
  state: string;
  priorityId: number | null;
  priority: string | null;
  deadline: string | null;
  currentAssigneeId: number | null;
  completedBy: number | null;
  completedAt: string | null;
  overdue: boolean;
}
interface TimedRow {
  start: string;
  end: string | null;
  closedSeconds: number;
  provisionalSeconds: number;
}
export interface WorkloadInterval extends TimedRow {
  intervalId: number;
  taskId: number;
}
export interface WorkloadShift extends TimedRow {
  workdayId: number;
  workDate: string;
  departmentId: number;
}
export interface WorkloadDetail extends Snapshot {
  userId: number;
  name: string;
  tasks: WorkloadTask[];
  taskCount: number;
  intervals: WorkloadInterval[];
  intervalCount: number;
  shifts: WorkloadShift[] | null;
  shiftCount: number | null;
}
export interface WorkloadTaskPage extends Snapshot {
  tasks: WorkloadTask[];
  totalCount: number;
  messageCode: "MSG10" | null;
  taskSet: WorkloadFilters["taskSet"];
}
const count = (n: number) => Number.isSafeInteger(n) && n >= 0;
const id = (n: number) => count(n) && n > 0;
const date = (s: string) =>
  typeof s === "string" &&
  /^\d{4}-\d{2}-\d{2}$/.test(s) &&
  Number.isFinite(Date.parse(s)) &&
  new Date(s).toISOString().slice(0, 10) === s;
const time = (s: string) =>
  typeof s === "string" &&
  /(Z|\+00:00)$/.test(s) &&
  Number.isFinite(Date.parse(s));
const nullableId = (n: number | null) => n === null || id(n);
const nullableTime = (s: string | null) => s === null || time(s);
function timed(row: TimedRow) {
  return (
    row &&
    time(row.start) &&
    nullableTime(row.end) &&
    (row.end === null || Date.parse(row.end) >= Date.parse(row.start)) &&
    count(row.closedSeconds) &&
    count(row.provisionalSeconds) &&
    (row.end === null ? row.closedSeconds === 0 : row.provisionalSeconds === 0)
  );
}
export async function readWorkload(
  branch: number,
  filters: WorkloadFilters,
  page: number,
  employee: number | null,
  signal: AbortSignal,
): Promise<WorkloadPage | WorkloadDetail | WorkloadTaskPage> {
  if (
    !id(branch) ||
    !id(page) ||
    page > Math.floor(2147483647 / 20) ||
    (employee !== null && !id(employee))
  )
    throw Error("Invalid workload identity");
  const params = new URLSearchParams({ page: String(page), pageSize: "20" });
  for (const key of ["from", "to", "deadlineFrom", "deadlineTo"] as const) {
    const value = filters[key];
    if (value !== undefined) {
      if (!date(value)) throw Error("Invalid period");
      params.set(key, value);
    }
  }
  if (
    (filters.from && filters.to && filters.to < filters.from) ||
    (filters.deadlineFrom &&
      filters.deadlineTo &&
      filters.deadlineTo < filters.deadlineFrom)
  )
    throw Error("Reversed period");
  for (const key of ["departmentId", "employeeId", "priorityId"] as const) {
    const value = filters[key];
    if (value !== undefined) {
      if (!id(value)) throw Error("Invalid reference");
      params.set(key, String(value));
    }
  }
  if (
    employee !== null &&
    filters.employeeId !== undefined &&
    employee !== filters.employeeId
  )
    throw Error("Employee filter mismatch");
  if (filters.status !== undefined) {
    if (!(taskWorkflowStates as readonly string[]).includes(filters.status))
      throw Error("Invalid workflow");
    params.set("status", filters.status);
  }
  if (filters.search !== undefined) {
    if (
      filters.search.length > 150 ||
      /[\u0000-\u001f\u007f-\u009f]/.test(filters.search)
    )
      throw Error("Invalid search");
    params.set("search", filters.search);
  }
  if (filters.overdueOnly) params.set("overdueOnly", "true");
  if (filters.taskSet !== undefined) {
    if (!["OPEN", "OVERDUE", "CLOSED_IN_PERIOD"].includes(filters.taskSet) || employee !== null || filters.employeeId !== undefined)
      throw Error("Invalid task population");
    params.set("taskSet", filters.taskSet);
  }
  if (filters.calendar !== undefined) {
    if (filters.calendar !== "UTC" && filters.calendar !== "Asia/Ho_Chi_Minh") throw Error("Invalid calendar");
    params.set("calendar", filters.calendar);
  }
  const response = await planningRequest(
    `/api/branches/${branch}/task-workload${filters.taskSet ? "/tasks" : employee === null ? "" : `/${employee}`}?${params}`,
    signal,
  );
  const data = (await response.json()) as WorkloadPage | WorkloadDetail | WorkloadTaskPage;
  if (
    !data ||
    (data.calendar ?? "Asia/Ho_Chi_Minh") !== (filters.calendar ?? "Asia/Ho_Chi_Minh") ||
    data.branchId !== branch ||
    data.page !== page ||
    data.pageSize !== 20 ||
    !date(data.from) ||
    !date(data.to) ||
    data.to < data.from ||
    !time(data.asOf) ||
    (filters.from && data.from !== filters.from) ||
    (filters.to && data.to !== filters.to)
  )
    throw Error("Invalid workload snapshot");
  if (filters.taskSet) {
    if (!("taskSet" in data) || data.taskSet !== filters.taskSet || !Array.isArray(data.tasks)
      || data.tasks.length > 20 || !count(data.totalCount) || data.totalCount < data.tasks.length
      || data.messageCode !== (data.totalCount === 0 ? "MSG10" : null)
      || new Set(data.tasks.map(t => t.taskId)).size !== data.tasks.length
      || data.tasks.some(t => !t || !id(t.taskId) || !id(t.departmentId) || typeof t.code !== "string" || typeof t.title !== "string"
        || typeof t.department !== "string" || !(taskWorkflowStates as readonly string[]).includes(t.state)
        || !nullableId(t.priorityId) || t.priority !== null && typeof t.priority !== "string"
        || !nullableTime(t.deadline) || !nullableTime(t.completedAt) || !nullableId(t.currentAssigneeId)
        || !nullableId(t.completedBy) || typeof t.overdue !== "boolean"
        || filters.departmentId !== undefined && t.departmentId !== filters.departmentId
        || filters.taskSet === "OPEN" && ["CLOSED", "CANCELLED"].includes(t.state)
        || filters.taskSet === "OVERDUE" && !t.overdue
        || filters.taskSet === "CLOSED_IN_PERIOD" && (t.state !== "CLOSED" || t.completedAt === null)))
      throw Error("Invalid workload task population");
  } else if (employee === null) {
    if (
      !("items" in data) ||
      !Array.isArray(data.items) ||
      data.items.length > 20 ||
      !count(data.totalCount) ||
      data.totalCount < data.items.length ||
      data.messageCode !== (data.totalCount === 0 ? "MSG10" : null) ||
      new Set(data.items.map((x) => x.userId)).size !== data.items.length ||
      data.items.some(
        (x) =>
          !id(x.userId) ||
          typeof x.name !== "string" ||
          typeof x.employeeCode !== "string" ||
          !count(x.openTasks) ||
          !count(x.overdueTasks) ||
          x.overdueTasks > x.openTasks ||
          !count(x.closedTasks) ||
          !count(x.closedEffortSeconds) ||
          !count(x.provisionalSeconds),
      )
    )
      throw Error("Invalid employee workload");
  } else {
    if (
      !("userId" in data) ||
      data.userId !== employee ||
      typeof data.name !== "string" ||
      !Array.isArray(data.tasks) ||
      !Array.isArray(data.intervals) ||
      !count(data.taskCount) ||
      !count(data.intervalCount) ||
      data.tasks.length > 20 ||
      data.intervals.length > 20 ||
      data.taskCount < data.tasks.length ||
      data.intervalCount < data.intervals.length ||
      new Set(data.tasks.map((t) => t.taskId)).size !== data.tasks.length ||
      new Set(data.intervals.map((i) => i.intervalId)).size !==
        data.intervals.length ||
      data.tasks.some(
        (t) =>
          !id(t.taskId) ||
          !id(t.departmentId) ||
          typeof t.code !== "string" ||
          typeof t.title !== "string" ||
          typeof t.department !== "string" ||
          !(taskWorkflowStates as readonly string[]).includes(t.state) ||
          !nullableId(t.priorityId) ||
          (t.priority !== null && typeof t.priority !== "string") ||
          !nullableTime(t.deadline) ||
          !nullableTime(t.completedAt) ||
          !nullableId(t.currentAssigneeId) ||
          !nullableId(t.completedBy) ||
          typeof t.overdue !== "boolean",
      ) ||
      data.intervals.some(
        (i) => !id(i.intervalId) || !id(i.taskId) || !timed(i),
      ) ||
      (data.shifts === null
        ? data.shiftCount !== null
        : !Array.isArray(data.shifts) ||
          !count(data.shiftCount!) ||
          data.shifts.length > 20 ||
          data.shiftCount! < data.shifts.length ||
          new Set(data.shifts.map((d) => d.workdayId)).size !==
            data.shifts.length ||
          data.shifts.some(
            (d) =>
              !id(d.workdayId) ||
              !id(d.departmentId) ||
              !date(d.workDate) ||
              !timed(d),
          ))
    )
      throw Error("Invalid time-log detail");
  }
  return data;
}
