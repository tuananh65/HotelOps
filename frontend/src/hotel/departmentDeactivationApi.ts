import { createHttpClient } from "../auth/http";
import {
  departmentDto,
  safeId,
  strongTag,
  type DepartmentDto,
} from "../hotel/contracts";
export type DepartmentDeactivationResult =
  | { outcome: "CONFIRMED"; data: DepartmentDto; etag: string }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      reason:
        | "DISABLED"
        | "BUSY"
        | "INVALID_REQUEST"
        | "STALE"
        | "CSRF"
        | "HTTP"
        | "NETWORK"
        | "INVALID_RESPONSE";
      status?: number;
    };
/** Technical candidate. No production route imports or flag can enable server release. */
export function createDepartmentDeactivationApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  let busy = false;
  const http = createHttpClient(fetcher);
  return {
    async change(
      branchId: number,
      departmentId: number,
      etag: string,
      reason: string,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<DepartmentDeactivationResult> {
      if (busy) return { outcome: "NOT_DISPATCHED", reason: "BUSY" };
      if (!enabled) return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (
        !safeId(branchId) ||
        !safeId(departmentId) ||
        !strongTag(etag) ||
        typeof reason !== "string" ||
        reason.trim().length < 10 ||
        reason.trim().length > 1000 ||
        /[\x00-\x1f\x7f-\x9f]/.test(reason)
      )
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      const body = JSON.stringify({
        reason: reason.trim(),
        confirmation: true,
      });
      if (signal?.aborted || !current())
        return { outcome: "NOT_DISPATCHED", reason: "STALE" };
      busy = true;
      let dispatched = false;
      try {
        const csrf = await http({
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
        if (!csrf.ok)
          return {
            outcome: "NOT_DISPATCHED",
            reason: "CSRF",
            ...(csrf.error.kind === "http"
              ? { status: csrf.error.status }
              : {}),
          };
        if (signal?.aborted || !current())
          return { outcome: "NOT_DISPATCHED", reason: "STALE" };
        dispatched = true;
        const r = await fetcher(
          `/api/branches/${branchId}/departments/${departmentId}/deactivate`,
          {
            method: "POST",
            credentials: "include",
            cache: "no-store",
            redirect: "error",
            signal,
            headers: {
              Accept: "application/json",
              "Content-Type": "application/json",
              "X-CSRF-TOKEN": csrf.data,
              "If-Match": etag,
            },
            body,
          },
        );
        if (signal?.aborted || !current())
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        if (r.status === 200) {
          let value: unknown;
          try {
            if (
              r.headers.get("Content-Type")?.split(";")[0].trim() ===
              "application/json"
            )
              value = await r.json();
          } catch {}
          if (signal?.aborted || !current())
            return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
          const data = departmentDto(value),
            next = r.headers.get("ETag");
          if (
            data &&
            data.id === departmentId &&
            data.branchId === branchId &&
            data.isActive === false &&
            strongTag(next) &&
            next !== etag
          )
            return { outcome: "CONFIRMED", data, etag: next };
          return {
            outcome: "OUTCOME_UNKNOWN",
            reason: "INVALID_RESPONSE",
            status: 200,
          };
        }
        if (r.status === 409) {
          let p: unknown;
          try {
            p = await r.json();
          } catch {}
          if (
            p &&
            typeof p === "object" &&
            "status" in p &&
            p.status === 409 &&
            "code" in p &&
            "title" in p &&
            p.code === p.title &&
            [
              "DEPARTMENT_DEPENDENCIES_EXIST",
              "DEPARTMENT_REFERENCE_CONFLICT",
              "DEPARTMENT_STATE_CONFLICT",
            ].includes(String(p.code))
          )
            return { outcome: "REJECTED", reason: "HTTP", status: 409 };
        }
        if ([400, 401, 403, 404, 412, 428].includes(r.status))
          return { outcome: "REJECTED", reason: "HTTP", status: r.status };
        return { outcome: "OUTCOME_UNKNOWN", reason: "HTTP", status: r.status };
      } catch {
        return {
          outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
          reason: "NETWORK",
        };
      } finally {
        busy = false;
      }
    },
  };
}
