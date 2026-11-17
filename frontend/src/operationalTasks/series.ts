import {
  planningMutation,
  planningRequest,
  validateTaskCreation,
  type CreateTaskInput,
} from "./planning";

export interface RecurrenceInput {
  rule: "DAILY" | "WEEKLY" | "MONTHLY";
  weekdays: number[];
  monthDay: number | null;
  defaultAssigneeId: number;
  endDate: string | null;
}
export interface TaskSeries {
  seriesId: number;
  branchId: number;
  title: string;
  rule: RecurrenceInput["rule"];
  weekdays: number[];
  monthDay: number | null;
  defaultAssigneeId: number;
  defaultAssigneeName: string;
  firstDueAt: string;
  nextDueAt: string | null;
  endDate: string | null;
  isActive: boolean;
  occurrenceCount: number;
  eTag: string;
}
export interface SeriesPage {
  items: TaskSeries[];
  totalCount: number;
  page: number;
  pageSize: number;
}
const positive = (n: number) => Number.isSafeInteger(n) && n > 0;
const strongTag = (s: string) =>
  typeof s === "string" && /^"[0-9A-F]{16}"$/.test(s);
const day = (s: string | null) =>
  s === null ||
  (typeof s === "string" &&
    /^\d{4}-\d{2}-\d{2}$/.test(s) &&
    Number.isFinite(Date.parse(s)) &&
    new Date(s).toISOString().slice(0, 10) === s);
const time = (s: string | null) =>
  s === null || (typeof s === "string" && Number.isFinite(Date.parse(s)));
function base(branch: number) {
  if (!positive(branch)) throw new Error("Invalid series branch");
  return `/api/branches/${branch}/operational-task-series`;
}
export function validateRecurrence(
  template: CreateTaskInput,
  r: RecurrenceInput,
) {
  validateTaskCreation(template);
  if (
    !template.dueAt ||
    !positive(r.defaultAssigneeId) ||
    !["DAILY", "WEEKLY", "MONTHLY"].includes(r.rule) ||
    !Array.isArray(r.weekdays) ||
    r.weekdays.some((d) => !Number.isInteger(d) || d < 0 || d > 6) ||
    new Set(r.weekdays).size !== r.weekdays.length ||
    !day(r.endDate) ||
    (r.rule === "WEEKLY"
      ? !r.weekdays.length || r.monthDay !== null
      : r.weekdays.length !== 0) ||
    (r.rule === "MONTHLY"
      ? !Number.isInteger(r.monthDay) || r.monthDay! < 1 || r.monthDay! > 31
      : r.monthDay !== null)
  )
    throw new Error("Invalid recurrence rule");
  const local = new Date(Date.parse(template.dueAt) + 7 * 3600000);
  if (
    (r.rule === "WEEKLY" && !r.weekdays.includes(local.getUTCDay())) ||
    (r.rule === "MONTHLY" && r.monthDay !== local.getUTCDate()) ||
    (r.endDate !== null && r.endDate < local.toISOString().slice(0, 10))
  )
    throw new Error("First deadline must match recurrence and end date");
}
function validateSeries(s: TaskSeries, branch: number) {
  if (
    !s ||
    !positive(s.seriesId) ||
    s.branchId !== branch ||
    typeof s.title !== "string" ||
    !["DAILY", "WEEKLY", "MONTHLY"].includes(s.rule) ||
    !Array.isArray(s.weekdays) ||
    s.weekdays.some((d) => !Number.isInteger(d) || d < 0 || d > 6) ||
    !positive(s.defaultAssigneeId) ||
    typeof s.defaultAssigneeName !== "string" ||
    typeof s.isActive !== "boolean" ||
    !Number.isSafeInteger(s.occurrenceCount) ||
    s.occurrenceCount < 0 ||
    !strongTag(s.eTag) ||
    typeof s.firstDueAt !== "string" ||
    !time(s.firstDueAt) ||
    !time(s.nextDueAt) ||
    !day(s.endDate)
  )
    throw new Error("Invalid recurring task response");
  return s;
}
export async function createSeries(
  branch: number,
  template: CreateTaskInput,
  recurrence: RecurrenceInput,
  signal: AbortSignal,
) {
  validateRecurrence(template, recurrence);
  const r = await planningMutation(
    base(branch),
    "POST",
    { template, recurrence },
    signal,
    template.requestLink ? `"${template.requestLink.version}"` : undefined,
  );
  const body = await r.json();
  const series = validateSeries(body?.series, branch);
  if (body.messageCode !== "MSG08" || r.headers.get("ETag") !== series.eTag)
    throw new Error("Invalid creation receipt");
  return series;
}
export async function readSeries(
  branch: number,
  page: number,
  signal: AbortSignal,
  seriesId?: number,
): Promise<SeriesPage> {
  if (!positive(page) || page > Math.floor(2147483647 / 25))
    throw new Error("Invalid series page");
  if (seriesId !== undefined && !positive(seriesId))
    throw new Error("Invalid series identity");
  const body = await (
    await planningRequest(
      base(branch) + `?page=${page}` + (seriesId ? `&series=${seriesId}` : ""),
      signal,
    )
  ).json();
  if (
    !body ||
    !Array.isArray(body.items) ||
    body.items.length > 25 ||
    body.page !== page ||
    body.pageSize !== 25 ||
    !Number.isSafeInteger(body.totalCount) ||
    body.totalCount < 0
  )
    throw new Error("Invalid series list");
  body.items.forEach((s: TaskSeries) => validateSeries(s, branch));
  if (
    seriesId !== undefined &&
    body.items.some((s: TaskSeries) => s.seriesId !== seriesId)
  )
    throw new Error("Unexpected series identity");
  return body;
}
export async function stopSeries(
  branch: number,
  series: TaskSeries,
  reason: string | null,
  signal: AbortSignal,
  taskId?: number,
) {
  validateSeries(series, branch);
  if (taskId !== undefined && !positive(taskId))
    throw new Error("Invalid source task");
  if (
    !series.isActive ||
    (reason !== null &&
      (reason.length > 4000 ||
        /[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(reason)))
  )
    throw new Error("Invalid stop series request");
  const r = await planningMutation(
    base(branch) + `/${series.seriesId}/stop`,
    "POST",
    { reason, ...(taskId ? { taskId } : {}) },
    signal,
    series.eTag,
  );
  const body = await r.json();
  const result = validateSeries(body?.series, branch);
  if (
    body.messageCode !== "MSG08" ||
    result.seriesId !== series.seriesId ||
    result.isActive ||
    r.headers.get("ETag") !== result.eTag
  )
    throw new Error("Invalid stop series receipt");
  return result;
}

export async function reactivateSeries(branch: number, series: TaskSeries, reason: string | null, signal: AbortSignal) {
  validateSeries(series, branch);
  if(series.isActive || !reason?.trim() || reason.length > 4000) throw new Error("Reactivation reason required");
  const response = await planningMutation(base(branch) + `/${series.seriesId}/reactivate`, "POST", {reason}, signal, series.eTag);
  const body = await response.json();
  const result = validateSeries(body?.series, branch);
  if(body.messageCode !== "MSG08" || result.seriesId !== series.seriesId || !result.isActive || response.headers.get("ETag") !== result.eTag)
    throw new Error("Invalid reactivation receipt");
  return result;
}
