export const reportTypes = [
  "rooms",
  "inspections",
  "incidents",
  "assets",
  "items",
  "tasks",
  "requests",
] as const;
export type ReportType = (typeof reportTypes)[number];
export interface ReportOptions {
  branches: { id: number; code: string; name: string }[];
  departments: { id: number; branchId: number; name: string }[];
  calendar: "UTC";
  defaultPeriodDays: number;
  exportRowLimit: number;
}
export interface ReportQuery {
  floorId?: number;
  roomTypeId?: number;
  roomId?: number;
  areaId?: number;
  categoryId?: number;
  severityId?: number;
  warehouseId?: number;
  itemId?: number;
  condition?: string;
  branchId?: number;
  departmentId?: number;
  housekeeperId?: number;
  inspectorId?: number;
  employeeId?: number;
  type?: string;
  from: string;
  to: string;
}
type Cell = string | number | boolean | null;
export interface ReportSection {
  key: string;
  columns: string[];
  rows: Record<string, Cell>[];
}
export interface ReportSnapshot {
  snapshotId: string;
  key: ReportType;
  snapshot: string;
  filters: {
    branchIds: number[];
    departmentId: number | null;
    from: string;
    to: string;
    calendar: "UTC";
  };
  sections: ReportSection[];
  totalCount: number;
  missingSources: string[];
  exportFormats: string[];
  appliedFilters?: Record<string, string | null> | null;
}
export class ReportFailure extends Error {
  constructor(readonly status: number) {
    super(`Yêu cầu báo cáo thất bại (${status}).`);
  }
}
const object = (v: unknown): v is Record<string, unknown> =>
  !!v && typeof v === "object" && !Array.isArray(v);
const id = (v: unknown): v is number =>
  Number.isSafeInteger(v) && Number(v) > 0;
const strings = (v: unknown): v is string[] =>
  Array.isArray(v) && v.every((x) => typeof x === "string");
const date = (v: unknown): v is string =>
  typeof v === "string" &&
  /^\d{4}-\d{2}-\d{2}$/.test(v) &&
  Number.isFinite(Date.parse(v)) &&
  new Date(v).toISOString().slice(0, 10) === v;
const scalar = (v: unknown) =>
  v === null ||
  typeof v === "string" ||
  typeof v === "boolean" ||
  (typeof v === "number" && Number.isFinite(v));
export function decodeOptions(v: unknown): ReportOptions {
  if (
    !object(v) ||
    v.calendar !== "UTC" ||
    !id(v.defaultPeriodDays) ||
    !id(v.exportRowLimit) ||
    !Array.isArray(v.branches) ||
    !v.branches.every(
      (b) =>
        object(b) &&
        id(b.id) &&
        typeof b.name === "string" &&
        typeof b.code === "string",
    ) ||
    !Array.isArray(v.departments) ||
    !v.departments.every(
      (d) =>
        object(d) && id(d.id) && id(d.branchId) && typeof d.name === "string",
    )
  )
    throw new Error("Lựa chọn báo cáo không hợp lệ");
  const result = v as unknown as ReportOptions;
  const branches = new Set(result.branches.map((b) => b.id));
  if (
    branches.size !== result.branches.length ||
    new Set(result.departments.map((d) => d.id)).size !==
      result.departments.length ||
    result.departments.some((d) => !branches.has(d.branchId))
  )
    throw new Error("Phạm vi báo cáo không hợp lệ");
  return result;
}
export function decodeSnapshot(
  v: unknown,
  key: ReportType,
  query: ReportQuery,
): ReportSnapshot {
  if (
    !object(v) ||
    v.key !== key ||
    typeof v.snapshotId !== "string" ||
    !/^[a-f0-9]{32}$/.test(v.snapshotId) ||
    typeof v.snapshot !== "string" ||
    !Number.isFinite(Date.parse(v.snapshot)) ||
    !object(v.filters) ||
    !Array.isArray(v.filters.branchIds) ||
    !v.filters.branchIds.every(id) ||
    new Set(v.filters.branchIds).size !== v.filters.branchIds.length ||
    !(v.filters.departmentId === null || id(v.filters.departmentId)) ||
    v.filters.departmentId !== (query.departmentId ?? null) ||
    v.filters.calendar !== "UTC" ||
    !date(v.filters.from) ||
    !date(v.filters.to) ||
    v.filters.from !== query.from ||
    v.filters.to !== query.to ||
    (query.branchId !== undefined &&
      (v.filters.branchIds.length !== 1 ||
        v.filters.branchIds[0] !== query.branchId)) ||
    !Number.isSafeInteger(v.totalCount) ||
    Number(v.totalCount) < 0 ||
    Number(v.totalCount) > 50000 ||
    !strings(v.missingSources) ||
    !strings(v.exportFormats) ||
    v.exportFormats.some((f) => f !== "csv" && f !== "xlsx" && f !== "pdf") ||
    !Array.isArray(v.sections) ||
    !v.sections.every(
      (s) =>
        object(s) &&
        typeof s.key === "string" &&
        strings(s.columns) &&
        new Set(s.columns).size === s.columns.length &&
        Array.isArray(s.rows) &&
        s.rows.every(
          (r) =>
            object(r) &&
            (s.columns as string[]).every(
              (c) => Object.hasOwn(r, c) && scalar(r[c]),
            ),
        ),
    )
  )
    throw new Error("Bản chụp dữ liệu báo cáo không hợp lệ");
  const result = v as unknown as ReportSnapshot;
  if (
    result.sections.reduce((n, s) => n + s.rows.length, 0) !==
      result.totalCount ||
    new Set(result.sections.map((s) => s.key)).size !==
      result.sections.length ||
    (result.appliedFilters != null &&
      (!object(result.appliedFilters) ||
        !Object.values(result.appliedFilters).every(
          (x) => x === null || typeof x === "string",
        )))
  )
    throw new Error("Dòng báo cáo không hợp lệ");
  for (const field of ["housekeeperId", "inspectorId", "employeeId", "type", "floorId", "roomTypeId", "roomId", "areaId", "categoryId", "severityId", "warehouseId", "itemId", "condition"] as const) {
    if (query[field] !== undefined && result.appliedFilters?.[field] !== String(query[field]))
      throw new Error("Bộ lọc báo cáo chưa được xác nhận");
  }
  return result;
}
export function createReportApi(
  signal: AbortSignal,
  send: typeof fetch = (...args) => fetch(...args),
) {
  async function request(path: string, body?: unknown) {
    signal.throwIfAborted();
    const headers: Record<string, string> = { Accept: "application/json" };
    if (body !== undefined) {
      const csrf = await request("/api/auth/csrf");
      const token: unknown = await csrf.json();
      signal.throwIfAborted();
      if (
        !object(token) ||
        typeof token.csrfToken !== "string" ||
        !token.csrfToken
      )
        throw new Error("Chưa có bảo vệ phiên (CSRF)");
      headers["X-CSRF-TOKEN"] = token.csrfToken;
      headers["Content-Type"] = "application/json";
    }
    const response = await send(path, {
      method: body === undefined ? "GET" : "POST",
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
      signal,
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    signal.throwIfAborted();
    if (!response.ok) throw new ReportFailure(response.status);
    return response;
  }
  return {
    async options() {
      return decodeOptions(
        await (await request("/api/reports/options")).json(),
      );
    },
    async preview(key: ReportType, query: ReportQuery) {
      if (
        !reportTypes.includes(key) ||
        !date(query.from) ||
        !date(query.to) ||
        query.from > query.to ||
        (query.branchId !== undefined && !id(query.branchId)) ||
        (query.departmentId !== undefined && !id(query.departmentId)) ||
        (query.employeeId !== undefined && (!id(query.employeeId) || key !== "tasks")) ||
        (query.type !== undefined && (key !== "requests" || !query.type.trim() || query.type.length > 40 || /[\u0000-\u001f\u007f]/.test(query.type))) ||
        (query.housekeeperId !== undefined && (!id(query.housekeeperId) || key !== "inspections")) ||
        (query.inspectorId !== undefined && (!id(query.inspectorId) || key !== "inspections"))
      )
        throw new Error("Bộ lọc báo cáo không hợp lệ");
      return decodeSnapshot(
        await (await request(`/api/reports/${key}/preview`, query)).json(),
        key,
        query,
      );
    },
    async export(snapshot: ReportSnapshot, format: "csv" | "xlsx" | "pdf" = "csv") {
      if (
        !/^[a-f0-9]{32}$/.test(snapshot.snapshotId) ||
        !reportTypes.includes(snapshot.key) ||
        !["csv", "xlsx", "pdf"].includes(format) ||
        !snapshot.exportFormats.includes(format)
      )
        throw new Error("Định danh bản xuất không hợp lệ");
      const response = await request(
        `/api/reports/snapshots/${snapshot.snapshotId}/export`,
        { format },
      );
      if (
        response.headers
          .get("Content-Type")
          ?.split(";")[0]
          .trim()
          .toLowerCase() !==
        (format === "csv"
          ? "text/csv"
          : format === "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
      )
        throw new Error(`Không hợp lệ ${format.toUpperCase()} Phản hồi`);
      const blob = await response.blob();
      signal.throwIfAborted();
      if (blob.size > 100 * 1024 * 1024) throw new Error("Dữ liệu xuất vượt giới hạn");
      return { blob, name: `${snapshot.key}-${snapshot.snapshotId}.${format}` };
    },
  };
}
