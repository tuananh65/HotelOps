// Existing OperationalHistoryDtos.cs contract. Counts are not interchangeable:
// DEFECT_COUNT_SEMANTICS / REINSPECTION_COUNT_SEMANTICS / DEPARTMENT_AUTHORIZATION
// remain REQUIRES_CONFIRMATION. No inferred count or department grant here.
export const MAX_EVIDENCE_BYTES = 10 * 1024 * 1024;
export interface Filters {
  roomId?: number | null;
  inspectorId?: number | null;
  housekeeperId?: number | null;
  from?: string | null;
  to?: string | null;
  result?: string | null;
  search?: string | null;
  page: number;
  pageSize: number;
}
export interface Inspection {
  id: number;
  number: string;
  roomId: number;
  roomNumber: string;
  branchId: number;
  cleaningTaskId: number | null;
  versionId: number;
  versionNumber: number;
  inspectorId: number;
  inspectorName: string;
  status: string;
  result: string | null;
  at: string;
  summary: string | null;
  reworkCount: number;
}
export interface Assignment {
  id: number;
  userId: number;
  name: string;
  status: string;
  assignedAt: string;
  endedAt: string | null;
  reason: string | null;
}
export interface Criterion {
  id: number | null;
  itemId: number;
  criterion: string;
  description: string | null;
  mandatory: boolean;
  passCondition: string | null;
  outcome: string | null;
  note: string | null;
  at: string | null;
  group?: string | null;
  evidenceIds?: number[] | null;
}
export interface Defect {
  id: number;
  itemId: number | null;
  description: string;
  status: string;
  severityId: number | null;
  correctiveAction: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
  location?: string | null;
  evidenceIds?: number[] | null;
}
export interface Rework {
  id: number;
  cleaningTaskId: number;
  inspectionId: number;
  assigneeId: number | null;
  reason: string;
  status: string;
  requestedAt: string;
  completedAt: string | null;
  note: string | null;
}
export interface Evidence {
  id: number;
  entityType: string;
  entityId: number;
  name: string;
  mimeType: string | null;
  bytes: number | null;
  uploadedAt: string;
  downloadPath: string;
}
export interface Detail {
  inspection: Inspection;
  housekeepers: Assignment[];
  criteria: Criterion[];
  defects: Defect[];
  reworks: Rework[];
  cycleInspections: Inspection[];
  evidence: Evidence[];
}
export interface HistoryPage {
  items: Inspection[];
  total: number;
  filters: Filters & { branchId: number };
}

export function positiveId(value: string | number): number {
  if (
    !/^\d+$/.test(String(value)) ||
    !Number.isSafeInteger(Number(value)) ||
    Number(value) <= 0
  )
    throw new Error("ID phải là số nguyên dương trong phạm vi an toàn.");
  return Number(value);
}
export function utc(value: string): string {
  if (
    !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$/.test(value) ||
    !Number.isFinite(Date.parse(value)) ||
    new Date(value).toISOString().slice(0, 19) !== value.slice(0, 19)
  )
    throw new Error("Ngày giờ phải hợp lệ, ở UTC và kết thúc bằng Z.");
  return value;
}
export function filters(input: Filters): Filters {
  const result = input.result?.trim().toUpperCase() || null;
  const search = input.search?.trim() || null;
  if (
    !Number.isSafeInteger(input.page) ||
    input.page < 1 ||
    !Number.isSafeInteger(input.pageSize) ||
    input.pageSize < 1 ||
    input.pageSize > 100 ||
    input.page > Math.floor(2147483647 / input.pageSize) ||
    (result !== null && !["PASS", "FAIL", "CONDITIONAL"].includes(result)) ||
    (search !== null &&
      (search.length > 150 || /[\x00-\x1f\x7f-\x9f]/.test(search)))
  )
    throw new Error("Bộ lọc hoặc phân trang không hợp lệ.");
  const from = input.from ? utc(input.from) : null,
    to = input.to ? utc(input.to) : null;
  if (from && to && Date.parse(from) > Date.parse(to))
    throw new Error("Thời điểm bắt đầu phải trước kết thúc.");
  return {
    page: input.page,
    pageSize: input.pageSize,
    result,
    search,
    from,
    to,
    roomId: input.roomId == null ? null : positiveId(input.roomId),
    inspectorId:
      input.inspectorId == null ? null : positiveId(input.inspectorId),
    housekeeperId:
      input.housekeeperId == null ? null : positiveId(input.housekeeperId),
  };
}
export function historyQuery(branch: number, input: Filters): string {
  const query = new URLSearchParams({ branchId: String(positiveId(branch)) });
  for (const [key, value] of Object.entries(filters(input)))
    if (value !== null) query.set(key, String(value));
  return query.toString();
}
type Row = Record<string, any>;
const obj = (v: unknown): v is Row =>
  typeof v === "object" && v !== null && !Array.isArray(v);
const id = (v: unknown) =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const count = (v: unknown) =>
  typeof v === "number" && Number.isSafeInteger(v) && v >= 0;
const str = (v: unknown) => typeof v === "string";
const text = (v: unknown) => v === null || str(v);
// SQL DateTime can serialize without a zone. Preserve the recorded value rather
// than relabeling unspecified timestamps as UTC.
const date = (v: unknown) =>
  str(v) &&
  /^\d{4}-\d{2}-\d{2}T/.test(v as string) &&
  Number.isFinite(Date.parse(v as string));
const nullable = (check: (v: unknown) => boolean, v: unknown) =>
  v === null || check(v);
const list = (v: unknown, check: (v: any) => boolean) =>
  Array.isArray(v) && v.every(check);
const evidenceIds = (v: unknown) => v == null || list(v, id);
function inspection(v: unknown, branch: number): v is Inspection {
  return (
    obj(v) &&
    id(v.id) &&
    v.branchId === branch &&
    id(v.roomId) &&
    nullable(id, v.cleaningTaskId) &&
    id(v.versionId) &&
    id(v.versionNumber) &&
    id(v.inspectorId) &&
    count(v.reworkCount) &&
    [v.number, v.roomNumber, v.inspectorName, v.status].every(str) &&
    text(v.result) &&
    text(v.summary) &&
    date(v.at)
  );
}
export function decodePage(
  v: unknown,
  branch: number,
  requested: Filters,
): HistoryPage {
  const expected = filters(requested);
  if (
    !obj(v) ||
    !count(v.total) ||
    !list(v.items, (row) => inspection(row, branch)) ||
    v.items.length > expected.pageSize ||
    v.items.length > v.total ||
    !obj(v.filters) ||
    v.filters.branchId !== branch
  )
    throw new Error("Phản hồi lịch sử không hợp lệ hoặc sai chi nhánh.");
  const got = filters(v.filters as Filters);
  for (const key of Object.keys(expected) as (keyof Filters)[]) {
    const a = expected[key],
      b = got[key];
    if (key === "from" || key === "to") {
      if (
        a !== b &&
        (!a || !b || Date.parse(String(a)) !== Date.parse(String(b)))
      )
        throw new Error("Bộ lọc phản hồi không khớp.");
    } else if (a !== b) throw new Error("Bộ lọc phản hồi không khớp.");
  }
  if (new Set(v.items.map((i: Inspection) => i.id)).size !== v.items.length)
    throw new Error("Lịch sử bị lặp.");
  return v as unknown as HistoryPage;
}
export function decodeDetail(
  v: unknown,
  branch: number,
  inspectionId: number,
): Detail {
  if (
    !obj(v) ||
    !inspection(v.inspection, branch) ||
    v.inspection.id !== inspectionId
  )
    throw new Error("Chi tiết sai phạm vi hoặc danh tính.");
  const task = v.inspection.cleaningTaskId,
    room = v.inspection.roomId;
  if (
    !list(
      v.housekeepers,
      (a) =>
        obj(a) &&
        id(a.id) &&
        id(a.userId) &&
        str(a.name) &&
        str(a.status) &&
        date(a.assignedAt) &&
        nullable(date, a.endedAt) &&
        text(a.reason),
    ) ||
    !list(
      v.criteria,
      (c) =>
        obj(c) &&
        nullable(id, c.id) &&
        id(c.itemId) &&
        str(c.criterion) &&
        text(c.description) &&
        typeof c.mandatory === "boolean" &&
        text(c.passCondition) &&
        text(c.outcome) &&
        text(c.note) &&
        nullable(date, c.at) &&
        (c.group === undefined || text(c.group)) &&
        evidenceIds(c.evidenceIds),
    ) ||
    !list(
      v.defects,
      (d) =>
        obj(d) &&
        id(d.id) &&
        nullable(id, d.itemId) &&
        str(d.description) &&
        str(d.status) &&
        nullable(id, d.severityId) &&
        text(d.correctiveAction) &&
        nullable(date, d.resolvedAt) &&
        text(d.resolutionNote) &&
        (d.location === undefined || text(d.location)) &&
        evidenceIds(d.evidenceIds),
    ) ||
    !list(
      v.reworks,
      (r) =>
        obj(r) &&
        id(r.id) &&
        id(r.inspectionId) &&
        id(r.cleaningTaskId) &&
        r.cleaningTaskId === task &&
        nullable(id, r.assigneeId) &&
        str(r.reason) &&
        str(r.status) &&
        date(r.requestedAt) &&
        nullable(date, r.completedAt) &&
        text(r.note),
    ) ||
    !list(
      v.cycleInspections,
      (i) =>
        inspection(i, branch) && i.roomId === room && i.cleaningTaskId === task,
    ) ||
    !list(
      v.evidence,
      (e) =>
        obj(e) &&
        id(e.id) &&
        id(e.entityId) &&
        str(e.entityType) &&
        str(e.name) &&
        text(e.mimeType) &&
        nullable(count, e.bytes) &&
        date(e.uploadedAt) &&
        str(e.downloadPath) &&
        ((e.entityType === "INSPECTION" && e.entityId === inspectionId) ||
          (e.entityType === "CLEANING_TASK" && e.entityId === task)),
    )
  )
    throw new Error("Dữ liệu chi tiết lịch sử không hợp lệ.");
  return v as unknown as Detail;
}
export function evidencePath(
  branch: number,
  inspectionId: number,
  attachmentId: number,
) {
  return `/api/inspection-history/${positiveId(inspectionId)}/evidence/${positiveId(attachmentId)}?branchId=${positiveId(branch)}`;
}
