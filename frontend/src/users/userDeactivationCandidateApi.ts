import { createHttpClient } from "../auth/http";
export type DeactivationResult =
  | {
      outcome: "CONFIRMED";
      etag: string;
      userId: number;
      accountStatus: "INACTIVE";
    }
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
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
/** Unmounted technical candidate. Backend release gates cannot be enabled by this client. */
export function createUserDeactivationCandidateApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  let busy = false;
  const http = createHttpClient(fetcher);
  return {
    async deactivate(
      userId: number,
      etag: string,
      reason: string,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<DeactivationResult> {
      if (busy) return { outcome: "NOT_DISPATCHED", reason: "BUSY" };
      if (!enabled) return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (
        !Number.isSafeInteger(userId) ||
        userId <= 0 ||
        !tag(etag) ||
        typeof reason !== "string" ||
        reason.trim().length < 10
      )
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      const body = JSON.stringify({ reason: reason.trim() });
      if (new TextEncoder().encode(body).length > 16384)
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
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
        const r = await fetcher(`/api/users/${userId}/deactivate`, {
          method: "PATCH",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "If-Match": etag,
            "X-CSRF-TOKEN": csrf.data,
          },
          body,
        });
        if (signal?.aborted || !current())
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        if (r.status === 200) {
          let v: unknown;
          try {
            if (
              r.headers.get("Content-Type")?.split(";")[0].trim() ===
              "application/json"
            )
              v = await r.json();
          } catch {}
          const next = r.headers.get("ETag");
          if (signal?.aborted || !current())
            return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
          if (
            v &&
            typeof v === "object" &&
            "userId" in v &&
            v.userId === userId &&
            "accountStatus" in v &&
            v.accountStatus === "INACTIVE" &&
            tag(next) &&
            next !== etag
          )
            return {
              outcome: "CONFIRMED",
              userId,
              accountStatus: "INACTIVE",
              etag: next,
            };
          return {
            outcome: "OUTCOME_UNKNOWN",
            reason: "INVALID_RESPONSE",
            status: 200,
          };
        }
        if ([400, 401, 403, 404, 412, 413, 415, 428].includes(r.status))
          return { outcome: "REJECTED", reason: "HTTP", status: r.status };
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
              "INVALID_STATUS_TRANSITION",
              "OPEN_RESPONSIBILITIES",
              "LAST_DURABLE_ADMIN",
            ].includes(String(p.code))
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
        busy = false;
      }
    },
  };
}
