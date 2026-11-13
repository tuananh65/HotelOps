import { createHttpClient } from "../auth/http";
import {
  decodeShifts,
  decodeWorkday,
  safeId,
  type ReadResult,
  type OwnShift,
  type OwnWorkday,
} from "./contracts";

/** GET only. No caller user ID, mutation, retries, storage or raw problem exposure. */
export function createWorkdayApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  // Existing transport expects one success status. Normalize only current's explicit
  // 204 to an internal JSON null; a 404 is never normalized to an empty workday.
  async function get<T>(
    branchId: number,
    operation: "own-shifts" | "current",
    decode: (v: unknown) => T | undefined,
    signal?: AbortSignal,
  ): Promise<ReadResult<T>> {
    if (!safeId(branchId)) return { ok: false, error: "invalid-request" };
    let noContent = false;
    let failureStatus: number | undefined;
    const currentRead = createHttpClient(async (url, init) => {
      const response = await fetchImplementation(url, init);
      if (response.status >= 400 && response.status <= 599)
        failureStatus = response.status;
      noContent = operation === "current" && response.status === 204;
      return noContent
        ? new Response("null", {
            status: 200,
            headers: { "Content-Type": "application/json" },
          })
        : response;
    });
    const result = await currentRead({
      method: "GET",
      url: `/api/branches/${branchId}/workdays/${operation}`,
      expectedStatus: 200,
      decode: (v) => (noContent ? (null as T) : decode(v)),
      signal,
    });
    if (signal?.aborted) return { ok: false, error: "cancelled" };
    if (result.ok) return { ok: true, data: result.data };
    const e = result.error;
    if (e.kind === "cancelled") return { ok: false, error: "cancelled" };
    const status = e.kind === "http" ? e.status : failureStatus;
    if (status !== undefined)
      return {
        ok: false,
        error:
          status === 401
            ? "unauthorized"
            : status === 403
              ? "forbidden"
              : [404, 405, 502, 503, 504].includes(status)
                ? "unavailable"
                : "http",
      };
    return {
      ok: false,
      error: e.kind === "network" ? e.kind : "invalid-response",
    };
  }
  return {
    ownShifts: (
      branchId: number,
      signal?: AbortSignal,
    ): Promise<ReadResult<OwnShift[]>> =>
      get(branchId, "own-shifts", (v) => decodeShifts(v, branchId), signal),
    current: (
      branchId: number,
      signal?: AbortSignal,
    ): Promise<ReadResult<OwnWorkday | null>> =>
      get(
        branchId,
        "current",
        (v) => (v === null ? undefined : decodeWorkday(v, branchId)),
        signal,
      ),
  };
}
export type WorkdayApi = ReturnType<typeof createWorkdayApi>;
