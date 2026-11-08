// FE05 adapter: existing cookie/CSRF transport, raw rowversion ETags, no replay.
export interface Criterion {
  itemId: number;
  criterion: string;
  description: string | null;
  mandatory: boolean;
  passCondition: string | null;
  outcome: string | null;
  note: string | null;
  group?: string | null;
  evidenceIds?: number[] | null;
}
export interface Defect {
  id: number;
  itemId: number | null;
  description: string;
  status: string;
  severityId: number | null;
  location?: string | null;
  evidenceIds?: number[] | null;
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
export interface Execution {
  id: number;
  branchId: number;
  roomId: number;
  cleaningTaskId: number;
  inspectorId: number;
  versionId: number;
  state: string;
  dueAt: string | null;
  criteria: Criterion[];
  defects: Defect[];
  evidence: Evidence[];
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
export interface Detail {
  inspection: Inspection;
  housekeepers: Assignment[];
  criteria: Criterion[];
  defects: Defect[];
  reworks: Rework[];
  cycleInspections: Inspection[];
  evidence: Evidence[];
}
export interface Filters {
  branchId?: number;
  roomId?: number;
  inspectorId?: number;
  housekeeperId?: number;
  from?: string;
  to?: string;
  result?: string;
  search?: string;
  page: number;
  pageSize: number;
}
export interface HistoryPage {
  items: Inspection[];
  total: number;
  filters: Filters;
}
export interface Upload {
  name: string;
  mimeType: string;
  base64: string;
}
export interface ResultInput {
  itemId: number;
  outcome: string;
  note: string | null;
  photos?: Upload[];
}
export class InspectionError extends Error {
  constructor(
    public status: number,
    public code: string,
  ) {
    super(
      status === 401
        ? "Session expired. Sign in again."
        : status === 403
          ? "Access denied for this branch or assigned inspector."
          : status === 412
            ? "Inspection changed. Reload before continuing."
            : status === 409
              ? "Operation unavailable in the current phase."
              : status === 400
                ? "Invalid input. Check mandatory results, failed criteria defects and evidence."
                : status === 0
                  ? "Connection lost. Outcome is unconfirmed; reload before any further write."
                  : `Request failed (${status}): ${code}`,
    );
  }
}
const obj = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
export const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
export function parseId(v: string): number {
  const n = Number(v);
  if (!/^\d+$/.test(v) || !positive(n))
    throw new InspectionError(400, "INVALID_ID");
  return n;
}
export const validTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const str = (v: unknown): v is string => typeof v === "string";
const nullableStr = (v: unknown) => v === null || str(v);
const criterion = (v: unknown): boolean =>
  obj(v) &&
  positive(v.itemId) &&
  str(v.criterion) &&
  typeof v.mandatory === "boolean" &&
  nullableStr(v.description) &&
  nullableStr(v.passCondition) &&
  nullableStr(v.outcome) &&
  nullableStr(v.note) &&
  (!v.evidenceIds ||
    (Array.isArray(v.evidenceIds) && v.evidenceIds.every(positive)));
const defect = (v: unknown): boolean =>
  obj(v) &&
  positive(v.id) &&
  (v.itemId === null || positive(v.itemId)) &&
  str(v.description) &&
  str(v.status) &&
  (v.severityId === null || positive(v.severityId)) &&
  (v.location === undefined || nullableStr(v.location));
const evidence = (v: unknown): boolean =>
  obj(v) &&
  positive(v.id) &&
  positive(v.entityId) &&
  str(v.entityType) &&
  str(v.name) &&
  str(v.downloadPath) &&
  nullableStr(v.mimeType) &&
  (v.bytes === null || (typeof v.bytes === "number" && v.bytes >= 0)) &&
  str(v.uploadedAt);
const list = (v: unknown, p: (v: unknown) => boolean) =>
  Array.isArray(v) && v.every(p);
const content = (v: Record<string, unknown>) =>
  list(v.criteria, criterion) &&
  list(v.defects, defect) &&
  list(v.evidence, evidence);
const inspection = (v: unknown): boolean =>
  obj(v) &&
  positive(v.id) &&
  positive(v.branchId) &&
  positive(v.roomId) &&
  positive(v.versionId) &&
  positive(v.inspectorId) &&
  positive(v.versionNumber) &&
  str(v.number) &&
  str(v.roomNumber) &&
  str(v.inspectorName) &&
  str(v.status) &&
  nullableStr(v.result) &&
  str(v.at) &&
  nullableStr(v.summary) &&
  typeof v.reworkCount === "number";
export function decodeExecution(v: unknown): Execution {
  if (
    !obj(v) ||
    !positive(v.id) ||
    !positive(v.branchId) ||
    !positive(v.roomId) ||
    !positive(v.cleaningTaskId) ||
    !positive(v.inspectorId) ||
    !positive(v.versionId) ||
    !str(v.state) ||
    !nullableStr(v.dueAt) ||
    !content(v)
  )
    throw new InspectionError(502, "INVALID_RESPONSE");
  return v as unknown as Execution;
}
export function decodePage(v: unknown): HistoryPage {
  if (
    !obj(v) ||
    !list(v.items, inspection) ||
    !Number.isSafeInteger(v.total) ||
    Number(v.total) < 0 ||
    !obj(v.filters) ||
    !positive(v.filters.page) ||
    !positive(v.filters.pageSize) ||
    Number(v.filters.pageSize) > 100
  )
    throw new InspectionError(502, "INVALID_RESPONSE");
  return v as unknown as HistoryPage;
}
export function decodeDetail(v: unknown): Detail {
  if (
    !obj(v) ||
    !inspection(v.inspection) ||
    !content(v) ||
    !list(v.cycleInspections, inspection) ||
    !list(
      v.housekeepers,
      (x) =>
        obj(x) &&
        positive(x.id) &&
        positive(x.userId) &&
        str(x.name) &&
        str(x.status) &&
        str(x.assignedAt),
    ) ||
    !list(
      v.reworks,
      (x) => obj(x) && positive(x.id) && str(x.reason) && str(x.status),
    )
  )
    throw new InspectionError(502, "INVALID_RESPONSE");
  return v as unknown as Detail;
}
export function historyQuery(f: Filters): string {
  if (
    !positive(f.page) ||
    !positive(f.pageSize) ||
    f.pageSize > 100 ||
    (f.search && f.search.length > 150) ||
    (f.result && !["PASS", "FAIL", "CONDITIONAL"].includes(f.result))
  )
    throw new InspectionError(400, "INVALID_FILTERS");
  for (const k of [
    "branchId",
    "roomId",
    "inspectorId",
    "housekeeperId",
  ] as const)
    if (f[k] !== undefined && !positive(f[k]))
      throw new InspectionError(400, "INVALID_FILTERS");
  for (const k of ["from", "to"] as const)
    if (f[k] && (!f[k]!.endsWith("Z") || !Number.isFinite(Date.parse(f[k]!))))
      throw new InspectionError(400, "UTC_REQUIRED");
  if (f.from && f.to && Date.parse(f.from) > Date.parse(f.to))
    throw new InspectionError(400, "INVALID_DATE_RANGE");
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(f))
    if (v !== undefined && v !== "") q.set(k, String(v));
  return q.toString();
}
export function finishIssues(
  criteria: Criterion[],
  defects: Defect[],
): string[] {
  return criteria.flatMap((c) =>
    c.mandatory && !["PASS", "FAIL"].includes(c.outcome ?? "")
      ? [`Mandatory: ${c.criterion}`]
      : c.outcome === "FAIL" && !defects.some((d) => d.itemId === c.itemId)
        ? [`Defect required: ${c.criterion}`]
        : [],
  );
}
export function createInspectionApi(fetcher: typeof fetch = fetch) {
  const request = async (
    path: string,
    signal: AbortSignal,
    method = "GET",
    body?: unknown,
    tag?: string,
    binary = false,
  ) => {
    const headers: Record<string, string> = {
      Accept: binary ? "application/octet-stream" : "application/json",
    };
    const init: RequestInit = {
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal,
    };
    const send = async (url: string, options: RequestInit) => {
      try {
        return await fetcher(url, options);
      } catch (e) {
        if (signal.aborted) throw e;
        throw new InspectionError(0, "NETWORK");
      }
    };
    const readJson = async (r: Response) => {
      try {
        return await r.json();
      } catch {
        throw new InspectionError(502, "INVALID_JSON");
      }
    };
    const check = async (r: Response) => {
      if (!r.ok) {
        let code = "HTTP_ERROR";
        try {
          const p = await r.json();
          if (obj(p) && typeof p.title === "string")
            code = p.title.slice(0, 120);
        } catch {
          /* No raw server body exposed. */
        }
        throw new InspectionError(r.status, code);
      }
    };
    if (method !== "GET") {
      if (!validTag(tag)) throw new InspectionError(428, "IF_MATCH_REQUIRED");
      const bootstrap = await send("/api/auth/csrf", {
        ...init,
        headers: { Accept: "application/json" },
      });
      await check(bootstrap);
      const csrf: unknown = await readJson(bootstrap);
      if (
        !obj(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
      )
        throw new InspectionError(502, "INVALID_CSRF");
      headers["X-CSRF-TOKEN"] = csrf.csrfToken;
      headers["If-Match"] = tag;
      headers["Content-Type"] = "application/json";
    }
    const response = await send(path, {
      ...init,
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    await check(response);
    return {
      data: binary ? await response.blob() : await readJson(response),
      tag: response.headers.get("ETag"),
    };
  };
  const route = (branch: number, id: number) => {
    if (!positive(branch) || !positive(id))
      throw new InspectionError(400, "INVALID_ID");
    return `/api/branches/${branch}/inspections/${id}`;
  };
  return {
    async execution(branch: number, id: number, signal: AbortSignal) {
      const r = await request(route(branch, id) + "/execution", signal);
      const data = decodeExecution(r.data);
      if (data.id !== id || data.branchId !== branch || !validTag(r.tag))
        throw new InspectionError(502, "SOURCE_MISMATCH");
      return { data, tag: r.tag };
    },
    async write(
      branch: number,
      id: number,
      action: "start" | "results" | "finish" | "defects",
      body: unknown,
      tag: string,
      signal: AbortSignal,
    ) {
      const r = await request(
        route(branch, id) + "/" + action,
        signal,
        action === "results" ? "PUT" : "POST",
        body,
        tag,
      );
      const data = decodeExecution(r.data);
      if (data.id !== id || data.branchId !== branch || !validTag(r.tag))
        throw new InspectionError(502, "SOURCE_MISMATCH");
      return { data, tag: r.tag };
    },
    async history(filters: Filters, signal: AbortSignal) {
      return decodePage(
        (
          await request(
            "/api/inspection-history?" + historyQuery(filters),
            signal,
          )
        ).data,
      );
    },
    async detail(branch: number, id: number, signal: AbortSignal) {
      route(branch, id);
      const d = decodeDetail(
        (
          await request(
            `/api/inspection-history/${id}?branchId=${branch}`,
            signal,
          )
        ).data,
      );
      if (d.inspection.id !== id || d.inspection.branchId !== branch)
        throw new InspectionError(502, "SOURCE_MISMATCH");
      return d;
    },
    async download(
      branch: number,
      id: number,
      attachment: number,
      signal: AbortSignal,
    ) {
      route(branch, id);
      if (!positive(attachment)) throw new InspectionError(400, "INVALID_ID");
      return (
        await request(
          `/api/inspection-history/${id}/evidence/${attachment}?branchId=${branch}`,
          signal,
          "GET",
          undefined,
          undefined,
          true,
        )
      ).data as Blob;
    },
  };
}
export async function uploadFiles(files: FileList | null): Promise<Upload[]> {
  if (!files) return [];
  if (files.length > 5) throw new InspectionError(400, "MAX_FIVE_FILES");
  return Promise.all(
    Array.from(files).map(async (f) => {
      if (
        !f.size ||
        f.size > 10 * 1024 * 1024 ||
        f.name.length > 200 ||
        /[\x00-\x1f/\\:]/.test(f.name) ||
        !["image/png", "image/jpeg", "application/pdf"].includes(f.type)
      )
        throw new InspectionError(400, "INVALID_FILE");
      return new Promise<Upload>((resolve, reject) => {
        const reader = new FileReader();
        reader.onerror = () =>
          reject(new InspectionError(400, "FILE_READ_FAILED"));
        reader.onload = () =>
          resolve({
            name: f.name,
            mimeType: f.type,
            base64: String(reader.result).split(",")[1],
          });
        reader.readAsDataURL(f);
      });
    }),
  );
}
