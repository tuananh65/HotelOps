import { InspectionError, positive } from "./api";
export interface Severity {
  id: number;
  code: string;
  name: string;
}
export interface Assigned {
  id: number;
  number: string;
  branchId: number;
  roomId: number;
  roomNumber: string;
  cleaningTaskId: number;
  inspectorId: number;
  inspectorName: string;
  versionId: number;
  state: string;
  dueAt: string | null;
}
export interface Task {
  id: number;
  number: string;
  branchId: number;
  roomId: number;
  roomNumber: string;
  state: string | null;
  pendingReinspection: boolean;
}
export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
export interface Planning {
  task: { id: number; branchId: number; workflowState: string | null };
  candidates: {
    id: number;
    name: string;
    openTasks: number;
    openTasksInWindow: number;
  }[];
  recordedShifts: {
    id: number;
    userId: number;
    from: string;
    to: string;
    recordedShiftStatus: string;
    recordedAssignmentStatus: string;
  }[];
  total: number;
  filters: { page: number; pageSize: number };
  checklist: {
    id: number;
    name: string;
    versionId: number;
    versionNumber: number;
    activeCriteria: number;
  } | null;
  capturedAt: string;
  workflowStateVerified: boolean;
}
const object = (x: unknown): x is Record<string, unknown> =>
  x !== null && typeof x === "object" && !Array.isArray(x);
export function decodeSeverities(v: unknown): Severity[] {
  if (
    !Array.isArray(v) ||
    !v.every(
      (x) =>
        object(x) &&
        positive(x.id) &&
        ["MINOR", "MAJOR"].includes(String(x.code)) &&
        typeof x.name === "string",
    ) ||
    new Set(v.map((x) => x.id)).size !== v.length
  )
    throw new InspectionError(502, "INVALID_SEVERITIES");
  return v;
}
export function createInspectionReads(fetcher: typeof fetch = fetch) {
  async function get(path: string, signal: AbortSignal): Promise<unknown> {
    let r: Response;
    try {
      r = await fetcher(path, {
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        headers: { Accept: "application/json" },
        signal,
      });
    } catch (e) {
      if (signal.aborted) throw e;
      throw new InspectionError(0, "NETWORK");
    }
    if (r.status !== 200) {
      let code = "HTTP_ERROR";
      try {
        const p = await r.json();
        if (object(p) && typeof p.title === "string")
          code = p.title.slice(0, 120);
      } catch {}
      throw new InspectionError(r.status, code);
    }
    try {
      return await r.json();
    } catch {
      throw new InspectionError(502, "INVALID_RESPONSE");
    }
  }
  const root = (branch: number) => {
    if (!positive(branch)) throw new InspectionError(400, "INVALID_BRANCH");
    return `/api/branches/${branch}`;
  };
  return {
    async severities(branch: number, s: AbortSignal) {
      return decodeSeverities(
        await get(root(branch) + "/inspection-severities", s),
      );
    },
    async list(
      branch: number,
      kind: "assigned" | "tasks",
      search: string,
      page: number,
      s: AbortSignal,
    ): Promise<Page<Assigned | Task>> {
      if (
        !positive(page) ||
        page > Math.floor(2147483647 / 20) ||
        search.length > 150 ||
        /[\x00-\x1f]/.test(search)
      )
        throw new InspectionError(400, "INVALID_FILTERS");
      const p = new URLSearchParams({
        search,
        page: String(page),
        pageSize: "20",
      });
      const v = await get(
        root(branch) +
          (kind === "assigned"
            ? "/inspections/assigned"
            : "/inspection-assignment-tasks") +
          "?" +
          p,
        s,
      );
      if (
        !object(v) ||
        !Array.isArray(v.items) ||
        !Number.isSafeInteger(v.total) ||
        Number(v.total) < 0 ||
        !positive(v.page) ||
        v.page !== page ||
        v.pageSize !== 20 ||
        !v.items.every(
          (x) =>
            object(x) &&
            positive(x.id) &&
            x.branchId === branch &&
            positive(x.roomId) &&
            typeof x.roomNumber === "string" &&
            typeof x.number === "string" &&
            (typeof x.state === "string" ||
              (kind === "tasks" && x.state === null)) &&
            (kind === "assigned"
              ? positive(x.inspectorId) &&
                positive(x.cleaningTaskId) &&
                positive(x.versionId) &&
                typeof x.inspectorName === "string" &&
                ["ASSIGNED", "INSPECTING", "PENDING_CONCLUSION"].includes(
                  String(x.state),
                )
              : typeof x.pendingReinspection === "boolean"),
        )
      )
        throw new InspectionError(502, "INVALID_COLLECTION");
      return v as unknown as Page<Assigned | Task>;
    },
    async planning(
      branch: number,
      task: number,
      s: AbortSignal,
    ): Promise<Planning> {
      if (!positive(task)) throw new InspectionError(400, "INVALID_TASK");
      const v = await get(
        root(branch) + `/cleaning-tasks/${task}/inspection-options`,
        s,
      );
      if (
        !object(v) ||
        !object(v.task) ||
        v.task.id !== task ||
        v.task.branchId !== branch ||
        !Array.isArray(v.candidates) ||
        !v.candidates.every(
          (x) => object(x) && positive(x.id) && typeof x.name === "string",
        ) ||
        !Array.isArray(v.recordedShifts) ||
        typeof v.workflowStateVerified !== "boolean" ||
        !object(v.filters) ||
        !positive(v.filters.page) ||
        !positive(v.filters.pageSize) ||
        typeof v.total !== "number" ||
        typeof v.capturedAt !== "string" ||
        !(
          v.checklist === null ||
          (object(v.checklist) &&
            positive(v.checklist.id) &&
            positive(v.checklist.versionId) &&
            typeof v.checklist.name === "string")
        )
      )
        throw new InspectionError(502, "INVALID_PLANNING");
      return v as unknown as Planning;
    },
  };
}
export const reads = createInspectionReads();
