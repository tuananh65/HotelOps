import { createHttpClient } from "../auth/http";
import { id, role, type RoleDto } from "./contracts";
export interface CustomRoleInput {
  roleCode: string;
  roleName: string;
  description: string | null;
}
export type CustomRoleResult =
  | { outcome: "CONFIRMED"; data: RoleDto }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      status?: number;
      reason: string;
    };
export function validMetadata(name: string, d: string | null) {
  return (
    !!name.trim() &&
    name.trim().length <= 150 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(name) &&
    (d === null ||
      (d.trim().length <= 4000 &&
        !/[\x00-\x08\x0b\x0c\x0e-\x1f\x7f-\x9f]/.test(d)))
  );
}
export function validCustomRole(v: CustomRoleInput) {
  return (
    !!v.roleCode.trim() &&
    v.roleCode.trim().length <= 60 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(v.roleCode) &&
    validMetadata(v.roleName, v.description)
  );
}
// .NET invariant casing does not expand a code point into multiple characters.
// Preserve those characters instead of predicting JavaScript full-uppercase codes.
function expectedRoleCode(value: string): string {
  return Array.from(value.trim(), (character) => {
    const upper = character.toUpperCase();
    return Array.from(upper).length === 1 ? upper : character;
  }).join("");
}
export function createCustomRoleApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  return {
    async metadata(roleId: number, signal?: AbortSignal) {
      let etag: string | null = null;
      let status: number | undefined;
      const client = createHttpClient(async (url, init) => {
        const response = await fetcher(url, init);
        etag = response.headers.get("ETag");
        status = response.status;
        return response;
      });
      if (!id(roleId)) return { ok: false as const, status: 400 };
      const result = await client({ method: "GET", url: `/api/roles/${roleId}/metadata`, expectedStatus: 200, signal,
        decode(value) {
          const data = role(value);
          return data?.roleId === roleId && etag && /^"[0-9A-F]{16}"$/.test(etag) ? { role: data, etag } : undefined;
        },
      });
      return result.ok ? { ok: true as const, data: result.data } : { ok: false as const, status };
    },
    async create(
      input: CustomRoleInput,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<CustomRoleResult> {
      if (!enabled) return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (!validCustomRole(input) || !current() || signal?.aborted)
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      const expectedCode = expectedRoleCode(input.roleCode),
        expectedName = input.roleName.trim(),
        expectedDescription = input.description?.trim() || null;
      const body = JSON.stringify({
        roleCode: input.roleCode,
        roleName: input.roleName,
        description: input.description,
      });
      let bootstrapFailureStatus: number | undefined;
      const csrf = createHttpClient(async (url, options) => {
        const response = await fetcher(url, options);
        if (response.status >= 400 && response.status <= 599)
          bootstrapFailureStatus = response.status;
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
          reason: "CSRF",
          ...(bootstrapFailureStatus === undefined
            ? {}
            : { status: bootstrapFailureStatus }),
        };
      if (!current() || signal?.aborted)
        return { outcome: "NOT_DISPATCHED", reason: "STALE" };
      try {
        const r = await fetcher("/api/roles", {
          method: "POST",
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": token.data,
          },
          body,
        });
        if (!current() || signal?.aborted)
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        if (r.status !== 201) {
          let reason = "HTTP";
          try {
            const problem: unknown = await r.json();
            if (problem && typeof problem === "object" && "title" in problem
              && (problem.title === "ROLE_CODE_EXISTS" || problem.title === "ROLE_VALIDATION_FAILED"
                || problem.title === "ROLE_CODE_STORAGE_INVALID")) reason = problem.title;
          } catch { /* No raw error body is exposed. */ }
          return {
            outcome: [400, 401, 403, 409, 412].includes(r.status)
              ? "REJECTED"
              : "OUTCOME_UNKNOWN",
            reason,
            status: r.status,
          };
        }
        if (
          r.headers.get("Content-Type")?.split(";")[0].trim().toLowerCase() !==
          "application/json"
        )
          return { outcome: "OUTCOME_UNKNOWN", reason: "INVALID_RESPONSE" };
        let v: unknown;
        try {
          v = await r.json();
        } catch {
          return { outcome: "OUTCOME_UNKNOWN", reason: "INVALID_RESPONSE" };
        }
        if (!current() || signal?.aborted)
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        const data = role(v);
        return data &&
          data.roleCode === expectedCode &&
          data.roleName === expectedName &&
          data.description === expectedDescription &&
          !data.isSystemRole &&
          data.isActive &&
          data.assignedUserCount === 0 &&
          data.permissionGrantCount === 0 &&
          r.headers.get("Location") === `/api/roles/${data.roleId}`
          ? { outcome: "CONFIRMED", data }
          : { outcome: "OUTCOME_UNKNOWN", reason: "INVALID_RESPONSE" };
      } catch {
        return {
          outcome: "OUTCOME_UNKNOWN",
          reason: signal?.aborted ? "CANCELLED" : "NETWORK",
        };
      }
    },
    async update(roleId: number, input: Pick<CustomRoleInput, "roleName" | "description">,
      etag: string, current: () => boolean, signal?: AbortSignal, deactivate = false): Promise<CustomRoleResult> {
      if (!enabled || !id(roleId) || (!deactivate && !validMetadata(input.roleName, input.description))
        || !/^"[0-9A-F]{16}"$/.test(etag) || !current() || signal?.aborted)
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      let status: number | undefined;
      const bootstrap = createHttpClient(async (url, init) => {
        const response = await fetcher(url, init);
        status = response.status;
        return response;
      });
      const token = await bootstrap({ method: "GET", url: "/api/auth/csrf", expectedStatus: 200, signal,
        decode(value) {
          if (value && typeof value === "object" && "headerName" in value && value.headerName === "X-CSRF-TOKEN"
            && "csrfToken" in value && typeof value.csrfToken === "string" && /^[\x21-\x7e]{1,8192}$/.test(value.csrfToken)) return value.csrfToken;
        },
      });
      if (!token.ok) return { outcome: "NOT_DISPATCHED", reason: "CSRF", status };
      if (!current() || signal?.aborted) return { outcome: "NOT_DISPATCHED", reason: "STALE" };
      status = undefined;
      let nextVersion: string | null = null;
      const client = createHttpClient(async (url, init) => {
        const headers = new Headers(init?.headers);
        headers.set("If-Match", etag);
        const response = await fetcher(url, { ...init, headers });
        status = response.status;
        nextVersion = response.headers.get("ETag");
        return response;
      });
      const result = await client({ method: deactivate ? "POST" : "PATCH",
        url: `/api/roles/${roleId}${deactivate ? "/deactivate" : ""}`, expectedStatus: 200,
        body: deactivate ? undefined : input, csrfToken: token.data, signal,
        decode(value) {
          const data = role(value);
          return data?.roleId === roleId && !data.isSystemRole
            && (deactivate ? !data.isActive : data.roleName === input.roleName.trim() && data.description === (input.description?.trim() || null))
            && nextVersion && /^"[0-9A-F]{16}"$/.test(nextVersion) && nextVersion !== etag ? data : undefined;
        },
      });
      if (!current() || signal?.aborted) return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
      if (result.ok) return { outcome: "CONFIRMED", data: result.data };
      return { outcome: status && [400, 401, 403, 404, 409, 412, 428].includes(status) ? "REJECTED" : "OUTCOME_UNKNOWN",
        reason: "HTTP", status };
    },
  };
}
export function customRoleMessage(
  r: Exclude<CustomRoleResult, { outcome: "CONFIRMED" }>,
) {
  return r.outcome === "OUTCOME_UNKNOWN"
    ? "Chưa xác định kết quả. Không gửi lại; xác minh với người có trách nhiệm."
    : r.reason === "ROLE_CODE_EXISTS"
      ? "MSG35: Mã vai trò đã tồn tại. Chọn mã khác."
    : r.status === 409
      ? "Xung đột mã vai trò hoặc bảo vệ quản trị viên."
      : r.status === 401
        ? "Phiên đăng nhập không hợp lệ. Vui lòng xác minh lại."
        : r.status === 403
          ? "Không có quyền quản trị toàn hệ thống."
          : r.status === 412
            ? "Xung đột phiên bản. Không gửi lại yêu cầu cũ."
            : "Chưa xác nhận thao tác. Kiểm tra dữ liệu nhập và kết nối máy chủ.";
}
