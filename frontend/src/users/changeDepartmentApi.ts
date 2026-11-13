import { createHttpClient } from "../auth/http";
export type DepartmentReview = {
  userId: number;
  branchId: number;
  departmentId: number;
};
export type DepartmentResult =
  | { ok: true; data: DepartmentReview; etag: string }
  | {
      ok: false;
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      status?: number;
    };
const id = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[0-9A-F]{16}"$/.test(v);
const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
export function createChangeDepartmentApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  let pending = false;
  return {
    async read(
      userId: number,
      signal?: AbortSignal,
    ): Promise<DepartmentResult> {
      if (!id(userId)) return { ok: false, outcome: "NOT_DISPATCHED" };
      try {
        const r = await fetcher(`/api/users/${userId}/department`, {
          method: "GET",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers: { Accept: "application/json" },
        });
        if (r.status !== 200)
          return { ok: false, outcome: "NOT_DISPATCHED", status: r.status };
        const etag = r.headers.get("ETag");
        if (
          !tag(etag) ||
          r.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() !==
            "application/json"
        )
          return { ok: false, outcome: "NOT_DISPATCHED" };
        const v: unknown = await r.json();
        if (
          signal?.aborted ||
          !object(v) ||
          v.userId !== userId ||
          !id(v.branchId) ||
          !id(v.departmentId)
        )
          return { ok: false, outcome: "NOT_DISPATCHED" };
        return {
          ok: true,
          data: { userId, branchId: v.branchId, departmentId: v.departmentId },
          etag,
        };
      } catch {
        return { ok: false, outcome: "NOT_DISPATCHED" };
      }
    },
    async change(
      review: DepartmentReview,
      etag: string,
      departmentId: number,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<DepartmentResult> {
      if (
        !enabled ||
        pending ||
        !id(review.userId) ||
        !id(review.branchId) ||
        !id(review.departmentId) ||
        !id(departmentId) ||
        departmentId === review.departmentId ||
        !tag(etag) ||
        !current() ||
        signal?.aborted
      )
        return { ok: false, outcome: "NOT_DISPATCHED" };
      const snapshot = { ...review };
      pending = true;
      let dispatched = false;
      try {
        // Preserve only an observed HTTP failure if reading its body fails.
        // A transport failure or malformed successful bootstrap must not invent status.
        let bootstrapFailureStatus: number | undefined;
        const http = createHttpClient(async (url, options) => {
          const response = await fetcher(url, options);
          if (response.status >= 400 && response.status <= 599)
            bootstrapFailureStatus = response.status;
          return response;
        });
        const csrf = await http({
          method: "GET",
          url: "/api/auth/csrf",
          expectedStatus: 200,
          signal,
          decode(v) {
            if (
              object(v) &&
              v.headerName === "X-CSRF-TOKEN" &&
              typeof v.csrfToken === "string" &&
              /^[\x21-\x7e]{1,8192}$/.test(v.csrfToken)
            )
              return v.csrfToken;
          },
        });
        if (!csrf.ok)
          return {
            ok: false,
            outcome: "NOT_DISPATCHED",
            ...(csrf.error.kind === "http"
              ? { status: csrf.error.status }
              : bootstrapFailureStatus !== undefined
                ? { status: bootstrapFailureStatus }
                : {}),
          };
        if (!current() || signal?.aborted)
          return { ok: false, outcome: "NOT_DISPATCHED" };
        dispatched = true;
        const r = await fetcher(`/api/users/${snapshot.userId}/department`, {
          method: "PUT",
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
          body: JSON.stringify({ departmentId }),
        });
        if (!current() || signal?.aborted)
          return { ok: false, outcome: "OUTCOME_UNKNOWN" };
        let v: unknown;
        try {
          if (
            ["application/json", "application/problem+json"].includes(
              r.headers
                .get("Content-Type")
                ?.split(";")[0]
                .trim()
                .toLowerCase() ?? "",
            )
          )
            v = await r.json();
        } catch {}
        if (!current() || signal?.aborted)
          return { ok: false, outcome: "OUTCOME_UNKNOWN" };
        const next = r.headers.get("ETag");
        if (
          r.status === 200 &&
          object(v) &&
          v.userId === snapshot.userId &&
          v.branchId === snapshot.branchId &&
          v.previousDepartmentId === snapshot.departmentId &&
          v.departmentId === departmentId &&
          tag(next) &&
          next !== etag
        )
          return {
            ok: true,
            data: {
              userId: snapshot.userId,
              branchId: snapshot.branchId,
              departmentId,
            },
            etag: next,
          };
        const conflict =
          r.status === 409 &&
          object(v) &&
          v.status === 409 &&
          typeof v.code === "string" &&
          v.code === v.title &&
          [
            "ROLE_SCOPE_CONFLICT",
            "DEPARTMENT_UNCHANGED",
            "INVALID_DEPARTMENT_SCOPE",
            "OPEN_RESPONSIBILITIES",
            "LAST_DURABLE_ADMIN",
          ].includes(v.code);
        return {
          ok: false,
          outcome:
            [400, 401, 403, 404, 412, 428].includes(r.status) || conflict
              ? "REJECTED"
              : "OUTCOME_UNKNOWN",
          status: r.status,
        };
      } catch {
        return {
          ok: false,
          outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
        };
      } finally {
        pending = false;
      }
    },
  };
}
