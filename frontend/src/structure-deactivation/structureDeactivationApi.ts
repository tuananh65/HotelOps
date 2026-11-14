import { createHttpClient } from "../auth/http";
import {
  decodeRecord,
  safeId,
  strongTag,
  validReason,
  type StructureKind,
  type ReadResult,
  type WriteResult,
  type Snapshot,
  type Failure,
} from "./contracts";
const codes = new Set([
  "STRUCTURE_DEPENDENCIES_EXIST",
  "STRUCTURE_NOT_ACTIVE",
  "STALE_STRUCTURE_VERSION",
  "STRUCTURE_WRITER_COORDINATION_REQUIRED",
  "STRUCTURE_WRITE_UNAVAILABLE",
  "SESSION_INVALID",
  "PERMISSION_DENIED",
  "VALIDATION_FAILED",
  "CSRF_VALIDATION_FAILED",
  "IF_MATCH_REQUIRED",
  "INVALID_IF_MATCH",
  "STRUCTURE_NODE_NOT_FOUND",
]);
const kinds = new Set([
  "ACTIVE_ROOM",
  "ACTIVE_CHILD_AREA",
  "ACTIVE_ASSET",
  "OPEN_INCIDENT",
  "OPEN_OPERATIONAL_TASK",
  "TASK_COMPLETION_CONTRACT_UNRESOLVED",
  "OPEN_MAINTENANCE_WORK",
]);
/** Default is physically fail-closed before CSRF or POST. No production router enables dispatch. */
export function createStructureDeactivationApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  dispatchEnabled = false,
) {
  const valid = (kind: StructureKind, b: number, id: number) =>
    (kind === "floor" || kind === "area") && safeId(b) && safeId(id);
  const url = (kind: StructureKind, b: number, id: number) =>
    `/api/branches/${b}/${kind === "floor" ? "floors" : "areas"}/${id}`;
  const options = (signal?: AbortSignal): RequestInit => ({
    credentials: "include",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  async function json(r: Response): Promise<unknown> {
    if (
      !["application/json", "application/problem+json"].includes(
        r.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() ?? "",
      )
    )
      return;
    try {
      return JSON.parse(await r.text());
    } catch {
      return;
    }
  }
  async function snapshot(
    r: Response,
    kind: StructureKind,
    b: number,
    id: number,
  ): Promise<Snapshot | undefined> {
    const record = decodeRecord(await json(r), kind, b, id),
      etag = r.headers.get("ETag");
    return record && strongTag(etag) ? { record, etag } : undefined;
  }
  async function failure(r: Response): Promise<Failure> {
    const v = await json(r);
    const error: Failure = { reason: "HTTP", status: r.status };
    if (!v || typeof v !== "object") return error;
    const x = v as Record<string, unknown>;
    if (x.status !== undefined && x.status !== r.status) return error;
    const present = [x.code, x.title].filter((v) => v !== undefined);
    if (
      present.length &&
      present.every(
        (v) => typeof v === "string" && codes.has(v) && v === present[0],
      )
    )
      error.code = present[0] as string;
    if (
      error.code === "STRUCTURE_DEPENDENCIES_EXIST" &&
      Array.isArray(x.dependencies) &&
      x.dependencies.length <= 7
    ) {
      const ds = x.dependencies;
      if (
        ds.every(
          (d) =>
            d &&
            typeof d === "object" &&
            kinds.has(d.kind) &&
            safeId(d.count) &&
            Array.isArray(d.ids) &&
            d.ids.length <= 100 &&
            d.ids.every(safeId),
        )
      )
        error.dependencies = ds.map((d) => ({ kind: d.kind, count: d.count }));
    }
    return error;
  }
  return {
    async read(
      kind: StructureKind,
      b: number,
      id: number,
      signal?: AbortSignal,
    ): Promise<ReadResult> {
      if (!valid(kind, b, id))
        return { ok: false, error: { reason: "INVALID_REQUEST" } };
      try {
        const r = await fetcher(url(kind, b, id), {
          ...options(signal),
          method: "GET",
        });
        if (signal?.aborted)
          return { ok: false, error: { reason: "CANCELLED" } };
        if (r.status !== 200) return { ok: false, error: await failure(r) };
        const data = await snapshot(r, kind, b, id);
        return signal?.aborted
          ? { ok: false, error: { reason: "CANCELLED" } }
          : data
            ? { ok: true, data }
            : { ok: false, error: { reason: "INVALID_RESPONSE" } };
      } catch {
        return {
          ok: false,
          error: { reason: signal?.aborted ? "CANCELLED" : "NETWORK" },
        };
      }
    },
    async deactivate(
      kind: StructureKind,
      b: number,
      id: number,
      etag: string,
      input: { reason: string; confirmation: boolean },
      stillCurrent: () => boolean,
      signal?: AbortSignal,
    ): Promise<WriteResult> {
      if (!dispatchEnabled)
        return { outcome: "NOT_DISPATCHED", error: { reason: "DISABLED" } };
      if (
        !valid(kind, b, id) ||
        !strongTag(etag) ||
        !validReason(input.reason) ||
        input.confirmation !== true ||
        signal?.aborted ||
        !stillCurrent()
      )
        return {
          outcome: "NOT_DISPATCHED",
          error: { reason: "INVALID_REQUEST" },
        };
      const body = JSON.stringify({ reason: input.reason, confirmation: true });
      let failureStatus: number | undefined;
      const csrf = createHttpClient(async (url, options) => {
        const response = await fetcher(url, options);
        if (response.status >= 400 && response.status <= 599)
          failureStatus = response.status;
        return response;
      });
      const token = await csrf({
        method: "GET",
        url: "/api/auth/csrf",
        expectedStatus: 200,
        signal,
        decode(v) {
          if (
            v &&
            typeof v === "object" &&
            "headerName" in v &&
            v.headerName === "X-CSRF-TOKEN" &&
            "csrfToken" in v &&
            typeof v.csrfToken === "string" &&
            /^[\x21-\x7e]{1,8192}$/.test(v.csrfToken)
          )
            return v.csrfToken;
        },
      });
      if (!token.ok)
        return {
          outcome: "NOT_DISPATCHED",
          error: {
            reason: "INVALID_RESPONSE",
            ...(failureStatus !== undefined ? { status: failureStatus } : {}),
          },
        };
      if (signal?.aborted || !stillCurrent())
        return { outcome: "NOT_DISPATCHED", error: { reason: "STALE" } };
      try {
        const r = await fetcher(url(kind, b, id) + "/deactivate", {
          ...options(signal),
          method: "POST",
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "If-Match": etag,
            "X-CSRF-TOKEN": token.data,
          },
          body,
        });
        if (signal?.aborted || !stillCurrent())
          return { outcome: "OUTCOME_UNKNOWN", error: { reason: "STALE" } };
        if (r.status !== 200) {
          const error = await failure(r);
          return {
            outcome: [400, 401, 403, 404, 409, 412, 428].includes(r.status)
              ? "REJECTED"
              : "OUTCOME_UNKNOWN",
            error,
          };
        }
        const data = await snapshot(r, kind, b, id);
        if (signal?.aborted || !stillCurrent())
          return { outcome: "OUTCOME_UNKNOWN", error: { reason: "STALE" } };
        return data && !data.record.isActive
          ? { outcome: "CONFIRMED", data }
          : {
              outcome: "OUTCOME_UNKNOWN",
              error: { reason: "INVALID_RESPONSE" },
            };
      } catch {
        return {
          outcome: "OUTCOME_UNKNOWN",
          error: { reason: signal?.aborted ? "CANCELLED" : "NETWORK" },
        };
      }
    },
  };
}
