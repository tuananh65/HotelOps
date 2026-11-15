import { taskWorkflowStates } from "./api";
import { TaskProgressError } from "./progress";

export interface PlanFields {
  priorityId: number;
  plannedStartAt: string | null;
  dueAt: string | null;
  slaMinutes: number | null;
}
export interface CreateTaskInput extends PlanFields {
  requestKey: string;
  departmentId: number;
  categoryId: number;
  title: string;
  description: string | null;
  requestedResult: string;
  criteria: { label: string; required: boolean }[];
  roomId: number | null;
  areaId: number | null;
  internalRequestId: number | null;
}
export interface PlanningOptions {
  departments: { id: number; name: string }[];
  categories: { id: number; name: string }[];
  priorities: { id: number; name: string }[];
}
export interface PlanSnapshot {
  etag: string;
  data: Omit<PlanFields, "priorityId"> & {
    priorityId: number | null;
    taskId: number;
    branchId: number;
    state: string;
    requestedResult: string | null;
    canManage: boolean;
    canCancel: boolean;
    historyTotalCount: number;
    page: number;
    pageSize: number;
    history: {
      auditId: number;
      at: string;
      actorId: number;
      previousPriorityId: number | null;
      previousPlannedStartAt: string | null;
      previousDueAt: string | null;
      previousSlaMinutes: number | null;
      priorityId: number | null;
      plannedStartAt: string | null;
      dueAt: string | null;
      slaMinutes: number | null;
      reason: string | null;
    }[];
  };
}
export const localPlanTime = (utc: string | null) =>
  utc ? new Date(Date.parse(utc) + 7 * 3600000).toISOString().slice(0, 23) : "";
export function utcPlanTime(local: string, original: string | null = null) {
  if (!local) return null;
  if (original && local === localPlanTime(original)) return original;
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,3})?)?$/.test(local))
    throw new Error("Invalid local time");
  const value = new Date(local + "+07:00");
  if (!Number.isFinite(value.valueOf())) throw new Error("Invalid local time");
  return value.toISOString();
}
const positive = (n: number) => Number.isSafeInteger(n) && n > 0;
const tag = (s: string) => /^"[0-9A-F]{16}"$/.test(s);
const controls = /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/;
const validTime = (s: string | null) =>
  s === null || (typeof s === "string" && Number.isFinite(Date.parse(s)));
function base(branch: number) {
  if (!positive(branch)) throw new Error("Invalid branch");
  return `/api/branches/${branch}/operational-tasks`;
}
function route(branch: number, task: number) {
  if (!positive(task)) throw new Error("Invalid task");
  return base(branch) + `/${task}`;
}
async function request(
  url: string,
  signal: AbortSignal,
  init: RequestInit = {},
) {
  const r = await fetch(url, {
    ...init,
    signal,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
  });
  if (!r.ok) throw new TaskProgressError(r.status);
  return r;
}
async function mutate(
  url: string,
  method: string,
  body: unknown,
  signal: AbortSignal,
  etag?: string,
) {
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    csrf?.headerName !== "X-CSRF-TOKEN" ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken.trim() ||
    /[\r\n]/.test(csrf.csrfToken)
  )
    throw new Error("Invalid CSRF response");
  signal.throwIfAborted();
  return request(url, signal, {
    method,
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrf.csrfToken,
      ...(etag ? { "If-Match": etag } : {}),
    },
    body: JSON.stringify(body),
  });
}
function validatePlan(p: PlanFields) {
  if (
    !positive(p.priorityId) ||
    !validTime(p.plannedStartAt) ||
    !validTime(p.dueAt) ||
    (p.slaMinutes !== null &&
      (!positive(p.slaMinutes) || p.slaMinutes > 2147483647)) ||
    (p.plannedStartAt &&
      p.dueAt &&
      Date.parse(p.dueAt) < Date.parse(p.plannedStartAt))
  )
    throw new Error("Invalid priority, duration or deadline");
}
export async function creationOptions(
  branch: number,
  department: number | null,
  signal: AbortSignal,
): Promise<PlanningOptions> {
  if (department !== null && !positive(department))
    throw new Error("Invalid department");
  const body = await (
    await request(
      base(branch) +
        "/creation-options" +
        (department ? `?department=${department}` : ""),
      signal,
    )
  ).json();
  if (
    !body ||
    ["departments", "categories", "priorities"].some(
      (k) =>
        !Array.isArray(body[k]) ||
        body[k].some(
          (r: { id: number; name: string }) =>
            !r || !positive(r.id) || typeof r.name !== "string",
        ),
    )
  )
    throw new Error("Invalid planning options");
  return body;
}
export function validateTaskCreation(input: CreateTaskInput) {
  validatePlan(input);
  if (
    !/^[a-f0-9]{8}(-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i.test(input.requestKey) ||
    !positive(input.departmentId) ||
    !positive(input.categoryId) ||
    !input.title.trim() ||
    input.title.length > 200 ||
    !input.requestedResult.trim() ||
    input.requestedResult.length > 4000 ||
    (input.description?.length ?? 0) > 4000 ||
    [input.title, input.requestedResult, input.description ?? ""].some((s) =>
      controls.test(s),
    ) ||
    !Array.isArray(input.criteria) ||
    input.criteria.length < 1 ||
    input.criteria.length > 100 ||
    input.criteria.some(
      (c) =>
        !c ||
        !c.label.trim() ||
        c.label.length > 250 ||
        typeof c.required !== "boolean" ||
        controls.test(c.label),
    ) ||
    new Set(input.criteria.map((c) => c.label.trim().toUpperCase())).size !==
      input.criteria.length ||
    (input.roomId !== null && !positive(input.roomId)) ||
    (input.areaId !== null && !positive(input.areaId)) ||
    (input.roomId !== null && input.areaId !== null) ||
    input.internalRequestId !== null
  )
    throw new Error("Invalid task definition");
}
export async function createTask(
  branch: number,
  input: CreateTaskInput,
  signal: AbortSignal,
) {
  validateTaskCreation(input);
  const response = await mutate(base(branch), "POST", input, signal);
  const body = await response.json();
  const etag = response.headers.get("ETag") ?? "";
  if (
    !positive(body?.taskId) ||
    body.branchId !== branch ||
    typeof body.code !== "string" ||
    !taskWorkflowStates.includes(body.state) ||
    body.messageCode !== "MSG08" ||
    !tag(etag)
  )
    throw new Error("Task creation outcome unconfirmed");
  return {
    taskId: body.taskId as number,
    branchId: branch,
    state: body.state as string,
  };
}
export async function readTaskPlan(
  branch: number,
  task: number,
  page: number,
  signal: AbortSignal,
): Promise<PlanSnapshot> {
  if (!positive(page)) throw new Error("Invalid history page");
  const response = await request(
    route(branch, task) + `/plan?page=${page}`,
    signal,
  );
  const data = await response.json();
  const etag = response.headers.get("ETag") ?? "";
  if (
    !data ||
    data.taskId !== task ||
    data.branchId !== branch ||
    !tag(etag) ||
    !taskWorkflowStates.includes(data.state) ||
    typeof data.canManage !== "boolean" ||
    typeof data.canCancel !== "boolean" ||
    (data.requestedResult !== null &&
      typeof data.requestedResult !== "string") ||
    !validTime(data.plannedStartAt) ||
    !validTime(data.dueAt) ||
    (data.priorityId !== null && !positive(data.priorityId)) ||
    (data.slaMinutes !== null && !positive(data.slaMinutes)) ||
    data.page !== page ||
    data.pageSize !== 25 ||
    !Number.isSafeInteger(data.historyTotalCount) ||
    data.historyTotalCount < 0 ||
    !Array.isArray(data.history) ||
    data.history.length > 25 ||
    data.history.some(
      (h: PlanSnapshot["data"]["history"][number]) =>
        !h ||
        !positive(h.auditId) ||
        !positive(h.actorId) ||
        !validTime(h.at) ||
        !validTime(h.previousDueAt) ||
        !validTime(h.dueAt) ||
        (h.reason !== null && typeof h.reason !== "string"),
    )
  )
    throw new Error("Invalid plan snapshot");
  return { data, etag };
}
export async function changeTaskPlan(
  branch: number,
  task: number,
  snapshot: PlanSnapshot,
  input: PlanFields | null,
  reason: string,
  signal: AbortSignal,
) {
  const path = route(branch, task);
  if (
    snapshot.data.taskId !== task ||
    snapshot.data.branchId !== branch ||
    !snapshot.data.canManage ||
    !tag(snapshot.etag) ||
    controls.test(reason) ||
    reason.length > 4000 ||
    (input === null && (!snapshot.data.canCancel || reason.trim().length < 10))
  )
    throw new Error("Invalid plan operation");
  if (input) validatePlan(input);
  const response = await mutate(
    path + (input ? "/plan" : "/cancel"),
    input ? "PUT" : "POST",
    { ...input, reason: reason.trim() || null },
    signal,
    snapshot.etag,
  );
  const body = await response.json();
  const etag = response.headers.get("ETag") ?? "";
  if (
    body?.taskId !== task ||
    body.branchId !== branch ||
    body.state !== (input ? snapshot.data.state : "CANCELLED") ||
    body.messageCode !== "MSG08" ||
    !tag(etag) ||
    etag === snapshot.etag
  )
    throw new Error("Plan outcome unconfirmed");
  return body;
}
