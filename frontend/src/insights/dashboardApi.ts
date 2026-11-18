export interface Dashboard {
  snapshot: string;
  filters: {
    branchIds: number[];
    departmentId: number | null;
    from: string;
    to: string;
    calendar: string;
  };
  metrics: { key: string; label: string; value: number | null; unit: string }[];
  distributions: {
    source: string;
    status: string;
    count: number;
    severity: string | null;
  }[];
  quantities: {
    kind: string;
    itemId: number;
    uomId: number;
    warehouseId: number | null;
    quantity: string;
    categoryId?: number | null;
  }[];
  missingSources: string[];
  categoryQuantities?: { categoryId: number; uomId: number; warehouseId: number; received: string; issued: string }[];
}
const object = (v: unknown): v is Record<string, unknown> =>
  !!v && typeof v === "object" && !Array.isArray(v);
const id = (v: unknown): v is number =>
  Number.isSafeInteger(v) && Number(v) > 0;
const text = (v: unknown): v is string => typeof v === "string";
export interface DashboardOptions {
  branches: { id: number; code: string; name: string }[];
  departments: { id: number; branchId: number; name: string }[];
}
export async function readDashboardOptions(
  signal: AbortSignal,
  roomsOnly = false,
): Promise<DashboardOptions> {
  const response = await fetch(roomsOnly ? "/api/dashboards/rooms/options" : "/api/dashboards/options", {
    method: "GET",
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
    headers: { Accept: "application/json" },
  });
  if (!response.ok) throw new DashboardFailure(response.status);
  const v: unknown = await response.json();
  if (
    !object(v) ||
    !Array.isArray(v.branches) ||
    !Array.isArray(v.departments) ||
    !v.branches.every(
      (b) => object(b) && id(b.id) && text(b.code) && text(b.name),
    ) ||
    !v.departments.every(
      (d) => object(d) && id(d.id) && id(d.branchId) && text(d.name),
    )
  )
    throw new Error("Lựa chọn tổng quan không hợp lệ");
  const options = v as unknown as DashboardOptions;
  const branches = new Set(options.branches.map((b) => b.id));
  if (
    branches.size !== options.branches.length ||
    new Set(options.departments.map((d) => d.id)).size !==
      options.departments.length ||
    options.departments.some((d) => !branches.has(d.branchId))
  )
    throw new Error("Phạm vi tổng quan không hợp lệ");
  return options;
}
export class DashboardFailure extends Error {
  constructor(readonly status: number) {
    super("Yêu cầu tổng quan thất bại");
  }
}
export async function readDashboard(
  query: string,
  signal: AbortSignal,
  roomsOnly = false,
): Promise<Dashboard> {
  const response = await fetch(`/api/dashboards/${roomsOnly ? "rooms" : "operations"}?${query}`, {
    method: "GET",
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
    headers: { Accept: "application/json" },
  });
  if (!response.ok) throw new DashboardFailure(response.status);
  const v: unknown = await response.json();
  if (
    !object(v) ||
    !text(v.snapshot) ||
    !Number.isFinite(Date.parse(v.snapshot)) ||
    !object(v.filters) ||
    !Array.isArray(v.filters.branchIds) ||
    !v.filters.branchIds.every(id) ||
    !(v.filters.departmentId === null || id(v.filters.departmentId)) ||
    !text(v.filters.from) ||
    !text(v.filters.to) ||
    v.filters.calendar !== "UTC" ||
    (v.categoryQuantities !== undefined && (!Array.isArray(v.categoryQuantities) || !v.categoryQuantities.every(q =>
      object(q) && id(q.categoryId) && id(q.uomId) && id(q.warehouseId)
        && typeof q.received === "string" && /^-?\d+(?:\.\d+)?$/.test(q.received)
        && typeof q.issued === "string" && /^-?\d+(?:\.\d+)?$/.test(q.issued)))) ||
    !Array.isArray(v.metrics) ||
    !v.metrics.every(
      (m) =>
        object(m) &&
        text(m.key) &&
        text(m.label) &&
        text(m.unit) &&
        (m.value === null ||
          (typeof m.value === "number" && Number.isFinite(m.value))),
    ) ||
    !Array.isArray(v.distributions) ||
    !v.distributions.every(
      (d) =>
        object(d) &&
        text(d.source) &&
        text(d.status) &&
        Number.isSafeInteger(d.count) &&
        Number(d.count) >= 0 &&
        (d.severity === null || text(d.severity)),
    ) ||
    !Array.isArray(v.quantities) ||
    !v.quantities.every(
      (q) =>
        object(q) &&
        text(q.kind) &&
        id(q.itemId) &&
        id(q.uomId) &&
        (q.warehouseId === null || id(q.warehouseId)) &&
        text(q.quantity) &&
        /^-?\d+(?:\.\d+)?$/.test(q.quantity),
    ) ||
    !Array.isArray(v.missingSources) ||
    !v.missingSources.every(text)
  )
    throw new Error("Phản hồi tổng quan không hợp lệ");
  return v as unknown as Dashboard;
}
