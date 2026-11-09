import { createHttpClient } from "../auth/http";
import { safeId } from "./contracts";
import {
  decodeScheduledResponse,
  strongWorkdayTag,
  type ScheduledResult,
} from "./scheduledContracts";
/** In-memory, no retries. Enabled only in explicitly approved integration/test callers. */
export function createScheduledWorkdayApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  dispatchEnabled = false,
) {
  const csrf = createHttpClient(fetcher);
  let pending = false;
  async function mutate(
    ...args: Parameters<typeof dispatch>
  ): Promise<ScheduledResult> {
    if (pending)
      return { outcome: "NOT_DISPATCHED", error: { reason: "BUSY" } };
    pending = true;
    try {
      return await dispatch(...args);
    } finally {
      pending = false;
    }
  }
  async function dispatch(
    branch: number,
    assignmentId: number,
    workdayId: number | undefined,
    etag: string | undefined,
    current: () => boolean,
    signal?: AbortSignal,
  ): Promise<ScheduledResult> {
    if (!dispatchEnabled)
      return { outcome: "NOT_DISPATCHED", error: { reason: "DISABLED" } };
    if (
      !safeId(branch) ||
      !safeId(assignmentId) ||
      (workdayId !== undefined &&
        (!safeId(workdayId) || !strongWorkdayTag(etag))) ||
      signal?.aborted ||
      !current()
    )
      return {
        outcome: "NOT_DISPATCHED",
        error: { reason: "INVALID_REQUEST" },
      };
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
          reason: "CSRF",
          ...(token.error.kind === "http"
            ? { status: token.error.status }
            : {}),
        },
      };
    if (signal?.aborted || !current())
      return { outcome: "NOT_DISPATCHED", error: { reason: "STALE" } };
    try {
      const headers = new Headers({
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": token.data,
      });
      if (etag !== undefined) headers.set("If-Match", etag);
      const r = await fetcher(
        `/api/branches/${branch}/workdays${workdayId === undefined ? "" : `/${workdayId}/end`}`,
        {
          method: "POST",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers,
          body: JSON.stringify(
            workdayId === undefined
              ? { shiftAssignmentId: assignmentId }
              : { pauseAll: false },
          ),
        },
      );
      if (signal?.aborted || !current())
        return { outcome: "OUTCOME_UNKNOWN", error: { reason: "STALE" } };
      if (r.status !== (workdayId === undefined ? 201 : 200))
        return {
          outcome: [400, 401, 403, 409, 412].includes(r.status)
            ? "REJECTED"
            : "OUTCOME_UNKNOWN",
          error: { reason: "HTTP", status: r.status },
        };
      if (
        r.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() !==
        "application/json"
      )
        return {
          outcome: "OUTCOME_UNKNOWN",
          error: { reason: "INVALID_RESPONSE" },
        };
      let v: unknown;
      try {
        v = await r.json();
      } catch {
        return {
          outcome: "OUTCOME_UNKNOWN",
          error: { reason: "INVALID_RESPONSE" },
        };
      }
      if (signal?.aborted || !current())
        return { outcome: "OUTCOME_UNKNOWN", error: { reason: "STALE" } };
      const data = decodeScheduledResponse(
        v,
        branch,
        {
          assignmentId,
          workdayId,
          status: workdayId === undefined ? "WORKING" : "ENDED",
        },
        r.headers.get("ETag"),
      );
      return data
        ? { outcome: "CONFIRMED", data }
        : { outcome: "OUTCOME_UNKNOWN", error: { reason: "INVALID_RESPONSE" } };
    } catch {
      return {
        outcome: "OUTCOME_UNKNOWN",
        error: { reason: signal?.aborted ? "CANCELLED" : "NETWORK" },
      };
    }
  }
  return {
    start: (
      branch: number,
      assignmentId: number,
      current: () => boolean,
      signal?: AbortSignal,
    ) => mutate(branch, assignmentId, undefined, undefined, current, signal),
    end: (
      branch: number,
      workdayId: number,
      assignmentId: number,
      etag: string,
      current: () => boolean,
      signal?: AbortSignal,
    ) => mutate(branch, assignmentId, workdayId, etag, current, signal),
  };
}
