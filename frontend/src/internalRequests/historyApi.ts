export interface HistoryRow {
  id: number;
  number: string;
  branchId: number;
  receivingDepartmentId: number | null;
  subject: string;
  status: string;
  deadline: string | null;
  overdue: boolean;
  createdAt?: string;
  timingStatus?: "ON_TIME" | "AT_RISK" | "OVERDUE" | "TERMINAL" | "INVALID_WINDOW";
  currentAssigneeId: number | null;
}
export interface HistoryPage {
  items: HistoryRow[];
  totalCount: number;
  page: number;
  pageSize: number;
  snapshotUtc: string;
}
export interface Timeline {
  requestId: number;
  eventTotalCount: number;
  assignmentTotalCount: number;
  events: {
    id: number;
    type: string;
    oldStatus: string | null;
    newStatus: string | null;
    note: string | null;
    actorId: number;
    occurredAt: string;
  }[];
  assignments: {
    id: number;
    assigneeId: number;
    assignedBy: number;
    assignedAt: string;
    endedAt: string | null;
    status: string;
    note: string | null;
  }[];
  feedback: {
    id: number;
    rating: number;
    comment: string | null;
    isSatisfied: boolean | null;
    submittedBy: number;
    submittedAt: string;
  } | null;
}
export class HistoryFailure extends Error {
  constructor(readonly status: number) {
    super(`History request failed (${status}).`);
  }
}
export interface HistoryOptions {
  statuses: string[];
  types: { id: number; name: string }[];
  priorities: { id: number; name: string }[];
  departments: { id: number; name: string }[];
}
export function decodeHistoryOptions(value: unknown): HistoryOptions {
  if (
    !object(value) ||
    !Array.isArray(value.statuses) ||
    !value.statuses.every(text) ||
    ![value.types, value.priorities, value.departments].every(
      (list) =>
        Array.isArray(list) &&
        list.every((item) => object(item) && id(item.id) && text(item.name)),
    )
  )
    throw new Error("Invalid history options.");
  return value as unknown as HistoryOptions;
}
const object = (v: unknown): v is Record<string, unknown> =>
  !!v && typeof v === "object" && !Array.isArray(v);
const id = (v: unknown): v is number =>
  Number.isSafeInteger(v) && Number(v) > 0;
const count = (v: unknown): v is number =>
  Number.isSafeInteger(v) && Number(v) >= 0;
const text = (v: unknown) => typeof v === "string";
const nullableText = (v: unknown) => v === null || text(v);
const date = (v: unknown) =>
  typeof v === "string" && /Z$/.test(v) && Number.isFinite(Date.parse(v));
const nullableDate = (v: unknown) => v === null || date(v);
export function decodeHistory(v: unknown): HistoryPage {
  if (
    !object(v) ||
    !Array.isArray(v.items) ||
    !count(v.totalCount) ||
    !id(v.page) ||
    !id(v.pageSize) ||
    v.pageSize > 100 ||
    !date(v.snapshotUtc) ||
    v.items.length > v.pageSize ||
    v.items.length > v.totalCount ||
    !v.items.every(
      (r) =>
        object(r) &&
        id(r.id) &&
        id(r.branchId) &&
        (r.receivingDepartmentId === null || id(r.receivingDepartmentId)) &&
        text(r.number) &&
        text(r.subject) &&
        text(r.status) &&
        nullableDate(r.deadline) &&
        typeof r.overdue === "boolean" &&
        ((r.timingStatus === undefined && r.createdAt === undefined) ||
          (date(r.createdAt) && typeof r.timingStatus === "string" &&
            ["ON_TIME", "AT_RISK", "OVERDUE", "TERMINAL", "INVALID_WINDOW"].includes(r.timingStatus) &&
            r.overdue === (r.timingStatus === "OVERDUE"))) &&
        (r.currentAssigneeId === null || id(r.currentAssigneeId)),
    )
  )
    throw new Error("Invalid history response.");
  return v as unknown as HistoryPage;
}
export function decodeTimeline(v: unknown, requestId: number): Timeline {
  if (
    !object(v) ||
    v.requestId !== requestId ||
    !count(v.eventTotalCount) ||
    !count(v.assignmentTotalCount) ||
    !Array.isArray(v.events) ||
    !Array.isArray(v.assignments) ||
    v.events.length > v.eventTotalCount ||
    v.assignments.length > v.assignmentTotalCount ||
    !v.events.every(
      (e) =>
        object(e) &&
        id(e.id) &&
        text(e.type) &&
        nullableText(e.oldStatus) &&
        nullableText(e.newStatus) &&
        nullableText(e.note) &&
        id(e.actorId) &&
        date(e.occurredAt),
    ) ||
    !v.assignments.every(
      (a) =>
        object(a) &&
        id(a.id) &&
        id(a.assigneeId) &&
        id(a.assignedBy) &&
        date(a.assignedAt) &&
        nullableDate(a.endedAt) &&
        text(a.status) &&
        nullableText(a.note),
    ) ||
    !(
      v.feedback === null ||
      (object(v.feedback) &&
        id(v.feedback.id) &&
        count(v.feedback.rating) &&
        nullableText(v.feedback.comment) &&
        (v.feedback.isSatisfied === null ||
          typeof v.feedback.isSatisfied === "boolean") &&
        id(v.feedback.submittedBy) &&
        date(v.feedback.submittedAt))
    )
  )
    throw new Error("Invalid timeline response.");
  return v as unknown as Timeline;
}
export async function readHistory(
  path: string,
  signal: AbortSignal,
): Promise<unknown> {
  const response = await fetch(`/api/internal-requests${path}`, {
    credentials: "same-origin",
    cache: "no-store",
    signal,
    headers: { Accept: "application/json" },
  });
  if (!response.ok) throw new HistoryFailure(response.status);
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Invalid history content type.");
  return response.json();
}
