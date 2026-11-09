import { createHttpClient } from "../auth/http";
import { role, type RoleDto } from "./contracts";
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
export function createCustomRoleApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  const csrf = createHttpClient(fetcher);
  return {
    async create(
      input: CustomRoleInput,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<CustomRoleResult> {
      if (!enabled) return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (!validCustomRole(input) || !current() || signal?.aborted)
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      const expectedCode = input.roleCode.trim().toUpperCase(),
        expectedName = input.roleName.trim(),
        expectedDescription = input.description?.trim() || null;
      const body = JSON.stringify({
        roleCode: input.roleCode,
        roleName: input.roleName,
        description: input.description,
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
      if (!token.ok) return { outcome: "NOT_DISPATCHED", reason: "CSRF" };
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
        if (r.status !== 201)
          return {
            outcome: [400, 401, 403, 409, 412].includes(r.status)
              ? "REJECTED"
              : "OUTCOME_UNKNOWN",
            reason: "HTTP",
            status: r.status,
          };
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
    async update(): Promise<CustomRoleResult> {
      return {
        outcome: "NOT_DISPATCHED",
        reason: "ROLE_VERSION_CONTRACT_REQUIRED",
      };
    },
  };
}
export function customRoleMessage(
  r: Exclude<CustomRoleResult, { outcome: "CONFIRMED" }>,
) {
  return r.outcome === "OUTCOME_UNKNOWN"
    ? "Outcome unknown. Do not replay; verify with an authorized owner."
    : r.status === 409
      ? "Role code conflict or administrator protection."
      : r.status === 401
        ? "Session invalid. Revalidate."
        : r.status === 403
          ? "Global administration access denied."
          : r.status === 412
            ? "Version conflict. No replay."
            : "Operation not confirmed. Check input and server availability.";
}
