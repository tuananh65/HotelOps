import { ApiError, getJson } from "./client";
import { positiveId } from "./rooms";
export interface MonitorItem {
  kind: "cleaning" | "rework";
  id: number;
  cleaningTaskId: number;
  taskNumber: string;
  roomId: number;
  roomNumber: string;
  floorId: number;
  floorName: string | null;
  recordedStatus: string;
  workflowState: string | null;
  priorityId: number | null;
  priorityCode: string | null;
  assigneeId: number | null;
  assigneeName: string | null;
  dueAtUtc: string | null;
  isOverdue: boolean | null;
  progressPercent: number | null;
  blockerCode: string | null;
  blockerNote: string | null;
  description: string | null;
  isCurrentCycle?: boolean | null;
  hasUnresolvedBlocker?: boolean | null;
}
export interface MonitorPage {
  items: MonitorItem[];
  total: number;
  page: number;
  pageSize: number;
  fromDate: string;
  toDate: string;
  timezone: string;
  asOfUtc: string;
}
export interface MonitorDetail {
  item: MonitorItem;
  assignments: {
    id: number;
    assigneeId: number;
    status: string;
    assignedAtUtc: string;
    endedAtUtc: string | null;
    reason: string | null;
  }[];
  assignmentCount: number;
  progress: {
    id: number;
    percent: number;
    status: string | null;
    blockerCode: string | null;
    note: string | null;
    loggedBy: number;
    loggedAtUtc: string;
  }[];
  progressCount: number;
  audit: {
    id: number;
    entityType: string;
    action: string;
    actorId: number | null;
    occurredAtUtc: string;
  }[];
  auditCount: number;
  timelineLimit: number;
  asOfUtc: string;
  timelinePage: number;
}
const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
const integer = (v: unknown, min = 1): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v >= min;
const text = (v: unknown): v is string => typeof v === "string";
const nullableText = (v: unknown) => v === null || text(v);
const nullableId = (v: unknown) => v === null || integer(v);
const stamp = (v: unknown) =>
  text(v) && /Z$/.test(v) && Number.isFinite(Date.parse(v));
const nullableStamp = (v: unknown) => v === null || stamp(v);
function invalid(): never {
  throw new ApiError(502, "INVALID_MONITOR_RESPONSE");
}
function item(v: unknown): asserts v is MonitorItem {
  if (
    !object(v) ||
    !["cleaning", "rework"].includes(String(v.kind)) ||
    ![v.id, v.cleaningTaskId, v.roomId, v.floorId].every((n) => integer(n)) ||
    ![v.taskNumber, v.roomNumber, v.recordedStatus].every(text) ||
    ![
      v.floorName,
      v.workflowState,
      v.priorityCode,
      v.assigneeName,
      v.blockerCode,
      v.blockerNote,
      v.description,
    ].every(nullableText) ||
    ![v.priorityId, v.assigneeId].every(nullableId) ||
    !nullableStamp(v.dueAtUtc) ||
    ![v.isCurrentCycle, v.hasUnresolvedBlocker].every(
      (value) => value == null || typeof value === "boolean",
    ) ||
    !(v.isOverdue === null || typeof v.isOverdue === "boolean") ||
    !(
      v.progressPercent === null ||
      (integer(v.progressPercent, 0) && v.progressPercent <= 100)
    )
  )
    invalid();
}
export async function listCleaning(
  branch: string,
  params: URLSearchParams,
  signal?: AbortSignal,
): Promise<MonitorPage> {
  const b = positiveId(branch);
  const query = new URLSearchParams();
  for (const key of [
    "fromDate",
    "toDate",
    "floorId",
    "status",
    "assigneeId",
    "priorityId",
    "overdue",
    "search",
    "kind",
    "page",
    "pageSize",
  ]) {
    const value = params.get(key);
    if (value) query.set(key, value);
  }
  for (const key of ["floorId", "assigneeId", "priorityId"])
    if (query.has(key)) positiveId(query.get(key)!);
  const page = positiveId(query.get("page") || "1"),
    pageSize = positiveId(query.get("pageSize") || "20");
  if (pageSize > 100 || page > Math.floor(2147483647 / pageSize))
    throw new Error("Invalid pagination");
  query.set("page", String(page));
  query.set("pageSize", String(pageSize));
  const v = await getJson(`/branches/${b}/cleaning-monitor?${query}`, signal);
  if (
    !object(v) ||
    !Array.isArray(v.items) ||
    !integer(v.total, 0) ||
    v.page !== page ||
    v.pageSize !== pageSize ||
    !text(v.fromDate) ||
    !text(v.toDate) ||
    !text(v.timezone) ||
    !stamp(v.asOfUtc) ||
    v.items.length > pageSize ||
    v.items.length > v.total
  )
    invalid();
  v.items.forEach(item);
  return v as unknown as MonitorPage;
}
export async function readCleaning(
  branch: string,
  kind: string,
  id: string,
  page = 1,
  signal?: AbortSignal,
): Promise<MonitorDetail> {
  if (
    !["cleaning", "rework"].includes(kind) ||
    !integer(page) ||
    page > Math.floor(2147483647 / 20)
  )
    throw new Error("Invalid detail");
  const v = await getJson(
    `/branches/${positiveId(branch)}/cleaning-monitor/${kind}/${positiveId(id)}?timelinePage=${page}&timelinePageSize=20`,
    signal,
  );
  if (!object(v)) invalid();
  item(v.item);
  if (
    v.item.kind !== kind ||
    v.item.id !== Number(id) ||
    v.timelinePage !== page ||
    v.timelineLimit !== 20 ||
    !stamp(v.asOfUtc)
  )
    invalid();
  for (const [key, count] of [
    ["assignments", "assignmentCount"],
    ["progress", "progressCount"],
    ["audit", "auditCount"],
  ]) {
    const rows = v[key];
    if (
      !Array.isArray(rows) ||
      !integer(v[count], 0) ||
      rows.length > 20 ||
      rows.length > (v[count] as number)
    )
      invalid();
    for (const r of rows) {
      if (!object(r) || !integer(r.id)) invalid();
      if (
        key === "assignments" &&
        (!integer(r.assigneeId) ||
          !text(r.status) ||
          !stamp(r.assignedAtUtc) ||
          !nullableStamp(r.endedAtUtc) ||
          !nullableText(r.reason))
      )
        invalid();
      if (
        key === "progress" &&
        (!integer(r.percent, 0) ||
          r.percent > 100 ||
          !integer(r.loggedBy) ||
          !stamp(r.loggedAtUtc) ||
          ![r.status, r.blockerCode, r.note].every(nullableText))
      )
        invalid();
      if (
        key === "audit" &&
        (!text(r.entityType) ||
          !text(r.action) ||
          !nullableId(r.actorId) ||
          !stamp(r.occurredAtUtc))
      )
        invalid();
    }
  }
  return v as unknown as MonitorDetail;
}
export function monitorError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401)
      return "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.";
    if (error.status === 403)
      return "Không có quyền CLEANING_VIEW tại chi nhánh này; OWN không cấp quyền xem monitor.";
    if (error.status === 404)
      return "Không tìm thấy công việc trong chi nhánh được phép.";
    if (error.status === 400)
      return "Bộ lọc không hợp lệ hoặc yêu cầu chưa dùng HTTPS.";
    if (error.status === 503)
      return "Dịch vụ hoặc cơ sở dữ liệu tạm thời không khả dụng.";
    if (error.status === 502) return "Phản hồi API không hợp lệ.";
  }
  return "Không tải được công việc. Kiểm tra kết nối và bộ lọc rồi thử lại.";
}
