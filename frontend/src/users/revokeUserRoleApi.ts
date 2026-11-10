import { createHttpClient } from "../auth/http";
export type RevokeResult =
  | { outcome: "CONFIRMED"; etag: string }
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
        | "CANCELLED"
        | "INVALID_RESPONSE";
      status?: number;
    };
export const userRoleTag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const id = (v: number) => Number.isSafeInteger(v) && v > 0;
/** Candidate only: callers cannot enable the server release fuse. Never queued/retried. */
export function createRevokeUserRoleApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  dispatchEnabled = false,
) {
  const http = createHttpClient(fetcher);
  let pending = false;
  return {
    async revoke(
      userId: number,
      assignmentId: number,
      etag: string,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<RevokeResult> {
      if (pending) return { outcome: "NOT_DISPATCHED", reason: "BUSY" };
      if (!dispatchEnabled)
        return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (!id(userId) || !id(assignmentId) || !userRoleTag(etag))
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      if (signal?.aborted || !current())
        return { outcome: "NOT_DISPATCHED", reason: "STALE" };
      pending = true;
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
        const r = await fetcher(`/api/users/${userId}/roles/${assignmentId}`, {
          method: "DELETE",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers: {
            Accept: "application/json",
            "X-CSRF-TOKEN": csrf.data,
            "If-Match": etag,
          },
        });
        if (signal?.aborted || !current())
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        if (r.status === 204) {
          const next = r.headers.get("ETag");
          if (userRoleTag(next) && next !== etag)
            return { outcome: "CONFIRMED", etag: next };
          return {
            outcome: "OUTCOME_UNKNOWN",
            reason: "INVALID_RESPONSE",
            status: 204,
          };
        }
        // Non-409 transport rejections are unambiguous; never leak arbitrary ProblemDetails.
        if ([400, 401, 403, 404, 412, 428].includes(r.status))
          return { outcome: "REJECTED", reason: "HTTP", status: r.status };
        if (r.status === 409) {
          let p: unknown;
          try {
            if (
              r.headers.get("Content-Type")?.split(";")[0].trim() ===
              "application/problem+json"
            )
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
            ["ROLE_ASSIGNMENT_CONFLICT", "LAST_DURABLE_ADMIN"].includes(
              String(p.code),
            )
          )
            return { outcome: "REJECTED", reason: "HTTP", status: 409 };
        }
        return { outcome: "OUTCOME_UNKNOWN", reason: "HTTP", status: r.status };
      } catch {
        return {
          outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
          reason: signal?.aborted ? "CANCELLED" : "NETWORK",
        };
      } finally {
        pending = false;
      }
    },
  };
}
