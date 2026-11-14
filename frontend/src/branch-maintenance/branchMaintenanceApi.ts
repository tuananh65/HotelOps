import { createHttpClient } from "../auth/http";
import {
  decodeBranch,
  safeId,
  strongBranchTag,
  validUpdate,
  type BranchReadResult,
  type BranchWriteResult,
  type BranchUpdate,
} from "./contracts";
/** UC012 normal flow only. Targets the active BranchMaintenanceController / BranchAdministrationService SQL RowVersion contract.
 * No backend activation is performed by this client.
 */
export function createBranchMaintenanceApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  const csrf = createHttpClient(fetchImplementation);
  const options = (signal?: AbortSignal) =>
    ({
      method: "GET",
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal,
      headers: new Headers({ Accept: "application/json" }),
    }) as const;
  async function snapshot(response: Response, id: number) {
    const type = response.headers
      .get("Content-Type")
      ?.split(";")[0]
      .trim()
      .toLowerCase();
    if (type !== "application/json") return;
    let value: unknown;
    try {
      value = JSON.parse(await response.text());
    } catch {
      return;
    }
    const branch = decodeBranch(value, id),
      etag = response.headers.get("ETag");
    return branch && strongBranchTag(etag) ? { branch, etag } : undefined;
  }
  return {
    async read(id: number, signal?: AbortSignal): Promise<BranchReadResult> {
      if (!safeId(id)) return { ok: false, error: { kind: "invalid-request" } };
      try {
        const response = await fetchImplementation(
          `/api/branches/${id}/edit`,
          options(signal),
        );
        if (signal?.aborted) return { ok: false, error: { kind: "cancelled" } };
        if (response.status !== 200)
          return {
            ok: false,
            error: { kind: "http", status: response.status },
          };
        const data = await snapshot(response, id);
        if (signal?.aborted) return { ok: false, error: { kind: "cancelled" } };
        return data
          ? { ok: true, data }
          : { ok: false, error: { kind: "invalid-response" } };
      } catch {
        return {
          ok: false,
          error: { kind: signal?.aborted ? "cancelled" : "network" },
        };
      }
    },
    async update(
      id: number,
      etag: string,
      input: BranchUpdate,
      stillCurrent: () => boolean,
      signal?: AbortSignal,
    ): Promise<BranchWriteResult> {
      if (
        !safeId(id) ||
        !strongBranchTag(etag) ||
        !validUpdate(input) ||
        signal?.aborted ||
        !stillCurrent()
      )
        return {
          outcome: "NOT_DISPATCHED",
          error: { kind: "invalid-request" },
        };
      // Detach the exact reviewed allowlist before awaiting CSRF. No role/status/code writes.
      const body = JSON.stringify({
        name: input.name,
        address: input.address,
        phone: input.phone,
        email: input.email,
        reason: input.reason,
      });
      const bootstrap = await csrf({
        method: "GET",
        url: "/api/auth/csrf",
        expectedStatus: 200,
        signal,
        decode(v) {
          if (
            typeof v !== "object" ||
            v === null ||
            !("csrfToken" in v) ||
            !("headerName" in v) ||
            v.headerName !== "X-CSRF-TOKEN" ||
            typeof v.csrfToken !== "string" ||
            !/^[\x21-\x7e]{1,8192}$/.test(v.csrfToken)
          )
            return;
          return v.csrfToken;
        },
      });
      if (!bootstrap.ok)
        return {
          outcome: "NOT_DISPATCHED",
          error:
            bootstrap.error.kind === "http"
              ? { kind: "http", status: bootstrap.error.status }
              : { kind: "invalid-response" },
        };
      if (signal?.aborted || !stillCurrent())
        return { outcome: "NOT_DISPATCHED", error: { kind: "stale" } };
      try {
        const headers = new Headers({
          Accept: "application/json",
          "Content-Type": "application/json",
          "If-Match": etag,
          "X-CSRF-TOKEN": bootstrap.data,
        });
        const response = await fetchImplementation(`/api/branches/${id}`, {
          ...options(signal),
          method: "PUT",
          headers,
          body,
        });
        if (signal?.aborted || !stillCurrent())
          return { outcome: "OUTCOME_UNKNOWN", error: { kind: "stale" } };
        if (response.status !== 200)
          return {
            outcome: [400, 401, 403, 404, 409, 412, 428].includes(
              response.status,
            )
              ? "REJECTED"
              : "OUTCOME_UNKNOWN",
            error: { kind: "http", status: response.status },
          };
        const data = await snapshot(response, id);
        if (signal?.aborted || !stillCurrent())
          return { outcome: "OUTCOME_UNKNOWN", error: { kind: "stale" } };
        return data
          ? { outcome: "CONFIRMED", data }
          : { outcome: "OUTCOME_UNKNOWN", error: { kind: "invalid-response" } };
      } catch {
        return { outcome: "OUTCOME_UNKNOWN", error: { kind: "network" } };
      }
    },
  };
}
