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
  }[];
  missingSources: string[];
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
): Promise<DashboardOptions> {
  const response = await fetch("/api/dashboards/options", {
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
    throw new Error("Invalid dashboard options");
  const options = v as unknown as DashboardOptions;
  const branches = new Set(options.branches.map((b) => b.id));
  if (
    branches.size !== options.branches.length ||
    new Set(options.departments.map((d) => d.id)).size !==
      options.departments.length ||
    options.departments.some((d) => !branches.has(d.branchId))
  )
    throw new Error("Invalid dashboard scope");
  return options;
}
export class DashboardFailure extends Error {
  constructor(readonly status: number) {
    super("Dashboard request failed");
  }
}
export async function readDashboard(
  query: string,
  signal: AbortSignal,
): Promise<Dashboard> {
  const response = await fetch(`/api/dashboards/operations?${query}`, {
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
    throw new Error("Invalid dashboard response");
  return v as unknown as Dashboard;
}
