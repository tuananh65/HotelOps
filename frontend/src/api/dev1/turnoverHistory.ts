import { ApiError, getJson } from "./client";
import { positiveId } from "./rooms";
export interface Coverage {
  cleaning: string;
  inspection: string;
  rework: string;
  readiness: string;
  roomStatusHistory: string;
}
export interface Cycle {
  id: number;
  roomId: number;
  branchId: number;
  roomNumber: string;
  number: string;
  cleaningType: string;
  recordedStatus: string;
  workflowState: string | null;
  createdAtUtc: string;
  startUtc: string | null;
  endUtc: string | null;
  isCurrentCycle: boolean | null;
  result: null;
  reworkCount: number | null;
  cancellationReason: string | null;
  assignees: {
    id: number;
    userId: number;
    name: string;
    status: string;
    assignedAtUtc: string;
    endedAtUtc: string | null;
  }[];
}
export interface TurnoverPage {
  roomId: number;
  branchId: number;
  items: Cycle[];
  total: number;
  filters: { page: number; pageSize: number };
  coverage: Coverage;
}
export interface TimelineEvent {
  sourceType: string;
  sourceId: number;
  event: string;
  atUtc: string;
  actorId: number | null;
  state: string | null;
  note: string | null;
}
export interface TurnoverDetail {
  cycle: Cycle;
  timeline: TimelineEvent[];
  timelineTotal: number;
  timelinePage: number;
  timelinePageSize: number;
  coverage: Coverage;
  inspections: {
    id: number;
    cleaningTaskId: number;
    roomId: number;
    versionId: number;
    versionNumber: number;
    inspectorId: number;
    recordedStatus: string;
    atUtc: string;
    result: null;
    coverage: string;
  }[];
  reworks: {
    id: number;
    inspectionId: number;
    cleaningTaskId: number;
    assigneeId: number | null;
    recordedStatus: string;
    workflowState: string | null;
    requestedAtUtc: string;
    completedAtUtc: string | null;
    note: string | null;
  }[];
  evidence: {
    id: number;
    entityType: string;
    entityId: number;
    name: string;
    mimeType: string | null;
    bytes: number | null;
    uploadedAt: string;
    downloadPath: string;
  }[];
}
const obj = (v: unknown): v is Record<string, any> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
const num = (v: unknown, min = 1): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v >= min;
const str = (v: unknown): v is string => typeof v === "string";
const nullable = (v: unknown) => v === null || str(v);
const stamp = (v: unknown): v is string =>
  str(v) && /Z$/.test(v) && Number.isFinite(Date.parse(v));
const time = (v: unknown) => v === null || stamp(v);
function invalid(): never {
  throw new ApiError(502, "INVALID_TURNOVER_RESPONSE");
}
function coverage(v: unknown): asserts v is Coverage {
  if (
    !obj(v) ||
    v.cleaning !== "PERSISTED_SOURCE" ||
    !["NOT_ACCEPTED", "SOURCE_READ_DENIED"].includes(v.inspection) ||
    !["PERSISTED_SOURCE_NOT_SHARED_ACCEPTED", "SOURCE_READ_DENIED"].includes(
      v.rework,
    ) ||
    !["PERSISTED_EVENTS_NOT_SHARED_ACCEPTED", "UNAVAILABLE"].includes(
      v.readiness,
    ) ||
    v.roomStatusHistory !== "UNAVAILABLE_CYCLE_LINK_CONTRACT"
  )
    invalid();
}
function cycle(v: unknown, room: number, branch: number): asserts v is Cycle {
  if (
    !obj(v) ||
    !num(v.id) ||
    v.roomId !== room ||
    v.branchId !== branch ||
    ![v.roomNumber, v.number, v.cleaningType, v.recordedStatus].every(str) ||
    !nullable(v.workflowState) ||
    !stamp(v.createdAtUtc) ||
    !time(v.startUtc) ||
    !time(v.endUtc) ||
    !(v.isCurrentCycle === null || typeof v.isCurrentCycle === "boolean") ||
    v.result !== null ||
    !(v.reworkCount === null || num(v.reworkCount, 0)) ||
    !nullable(v.cancellationReason) ||
    !Array.isArray(v.assignees) ||
    v.assignees.some(
      (a: unknown) =>
        !obj(a) ||
        !num(a.id) ||
        !num(a.userId) ||
        !str(a.name) ||
        !str(a.status) ||
        !stamp(a.assignedAtUtc) ||
        !time(a.endedAtUtc),
    )
  )
    invalid();
}
export function turnoverQuery(params: URLSearchParams) {
  const q = new URLSearchParams();
  for (const key of [
    "branchId",
    "from",
    "to",
    "result",
    "page",
    "pageSize",
    "timelinePage",
    "timelinePageSize",
  ]) {
    const value = params.get(key);
    if (value) q.set(key, value);
  }
  if (q.has("branchId")) positiveId(q.get("branchId")!);
  for (const [key, defaultValue, max] of [
    ["page", "1", Number.MAX_SAFE_INTEGER],
    ["pageSize", "20", 100],
    ["timelinePage", "1", Number.MAX_SAFE_INTEGER],
    ["timelinePageSize", "50", 100],
  ] as const) {
    const n = positiveId(q.get(key) || defaultValue);
    if (n > max) throw Error("INVALID_PAGINATION");
    q.set(key, String(n));
  }
  for (const key of ["from", "to"]) {
    const value = q.get(key);
    if (value && !stamp(value)) throw Error("INVALID_UTC_DATE");
  }
  if (
    q.has("from") &&
    q.has("to") &&
    Date.parse(q.get("from")!) > Date.parse(q.get("to")!)
  )
    throw Error("INVALID_DATE_RANGE");
  if (q.has("result") && !["PASS", "FAIL"].includes(q.get("result")!))
    throw Error("INVALID_RESULT");
  return q;
}
export async function listTurnovers(
  room: string,
  params: URLSearchParams,
  signal?: AbortSignal,
): Promise<TurnoverPage> {
  const id = positiveId(room),
    q = turnoverQuery(params);
  const d = await getJson(`/rooms/${id}/turnover-history?${q}`, signal);
  if (
    !obj(d) ||
    d.roomId !== id ||
    !num(d.branchId) ||
    (q.has("branchId") && d.branchId !== Number(q.get("branchId"))) ||
    !num(d.total, 0) ||
    !Array.isArray(d.items) ||
    !obj(d.filters) ||
    d.filters.page !== Number(q.get("page")) ||
    d.filters.pageSize !== Number(q.get("pageSize")) ||
    d.items.length > d.filters.pageSize ||
    d.items.length > d.total
  )
    invalid();
  coverage(d.coverage);
  for (const item of d.items) cycle(item, id, d.branchId);
  return d as unknown as TurnoverPage;
}
export async function readTurnover(
  room: string,
  id: string,
  params: URLSearchParams,
  signal?: AbortSignal,
): Promise<TurnoverDetail> {
  const r = positiveId(room),
    c = positiveId(id),
    q = turnoverQuery(params);
  const d = await getJson(`/rooms/${r}/turnover-history/${c}?${q}`, signal);
  if (!obj(d) || !obj(d.cycle) || !num(d.cycle.branchId)) invalid();
  cycle(d.cycle, r, d.cycle.branchId);
  if (
    d.cycle.id !== c ||
    (q.has("branchId") && d.cycle.branchId !== Number(q.get("branchId"))) ||
    d.timelinePage !== Number(q.get("timelinePage")) ||
    d.timelinePageSize !== Number(q.get("timelinePageSize")) ||
    !num(d.timelineTotal, 0) ||
    !Array.isArray(d.timeline) ||
    d.timeline.length > d.timelinePageSize ||
    d.timeline.length > d.timelineTotal
  )
    invalid();
  coverage(d.coverage);
  for (const e of d.timeline)
    if (
      !obj(e) ||
      !str(e.sourceType) ||
      !num(e.sourceId) ||
      !str(e.event) ||
      !stamp(e.atUtc) ||
      !(e.actorId === null || num(e.actorId)) ||
      !nullable(e.state) ||
      !nullable(e.note)
    )
      invalid();
  if (
    !Array.isArray(d.inspections) ||
    !Array.isArray(d.reworks) ||
    !Array.isArray(d.evidence)
  )
    invalid();
  for (const i of d.inspections)
    if (
      !obj(i) ||
      ![i.id, i.versionId, i.versionNumber, i.inspectorId].every((x) =>
        num(x),
      ) ||
      i.cleaningTaskId !== c ||
      i.roomId !== r ||
      !str(i.recordedStatus) ||
      !stamp(i.atUtc) ||
      i.result !== null ||
      i.coverage !== "NOT_ACCEPTED"
    )
      invalid();
  const inspections = new Set(d.inspections.map((i: { id: number }) => i.id));
  for (const w of d.reworks)
    if (
      !obj(w) ||
      !num(w.id) ||
      !inspections.has(w.inspectionId) ||
      w.cleaningTaskId !== c ||
      !(w.assigneeId === null || num(w.assigneeId)) ||
      !str(w.recordedStatus) ||
      !nullable(w.workflowState) ||
      !stamp(w.requestedAtUtc) ||
      !time(w.completedAtUtc) ||
      !nullable(w.note)
    )
      invalid();
  for (const e of d.evidence)
    if (
      !obj(e) ||
      !num(e.id) ||
      !str(e.name) ||
      !nullable(e.mimeType) ||
      !(e.bytes === null || num(e.bytes, 0)) ||
      !stamp(e.uploadedAt) ||
      !(
        (e.entityType === "CLEANING_TASK" && e.entityId === c) ||
        (e.entityType === "INSPECTION" && inspections.has(e.entityId))
      ) ||
      e.downloadPath !==
        `/api/rooms/${r}/turnover-history/${c}/evidence/${e.id}?branchId=${d.cycle.branchId}`
    )
      invalid();
  if (
    d.coverage.inspection === "SOURCE_READ_DENIED" &&
    (d.inspections.length ||
      d.reworks.length ||
      d.evidence.some(
        (e: { entityType: string }) => e.entityType === "INSPECTION",
      ))
  )
    invalid();
  return d as unknown as TurnoverDetail;
}
export function turnoverError(e: unknown): string {
  if (e instanceof ApiError) {
    if (e.status === 401) return "Phiên máy chủ không hợp lệ hoặc đã hết hạn.";
    if (e.status === 403)
      return "Bạn không có quyền xem nguồn tại chi nhánh/phòng này.";
    if (e.status === 404)
      return "Không tìm thấy phòng, cycle hoặc evidence trong phạm vi được cấp quyền.";
    if (e.status === 503)
      return "Nguồn tạm chưa khả dụng; bộ lọc PASS/FAIL cần Inspection contract accepted.";
    if (e.status === 0) return "Không kết nối được API. Thử tải lại.";
  }
  return "Dữ liệu hoặc bộ lọc turnover không hợp lệ; không dùng dữ liệu prototype thay thế.";
}
