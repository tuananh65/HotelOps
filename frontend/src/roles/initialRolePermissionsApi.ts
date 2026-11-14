import { createHttpClient } from "../auth/http";
import { id, obj } from "./contracts";
export type InitialGrant = Readonly<{
  permissionId: number;
  dataScope: "OWN" | "BRANCH";
}>;
export type InitialGrantsResult =
  | { outcome: "CONFIRMED"; roleId: number; grants: InitialGrant[] }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      reason: string;
      status?: number;
    };
export const initialScope = (code: string): "OWN" | "BRANCH" | null =>
  code === "ROOM_VIEW"
    ? "BRANCH"
    : ["SHIFT_VIEW", "ATTENDANCE_RECORD"].includes(code)
      ? "OWN"
      : null;
export function createInitialRolePermissionsApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  let busy = false;
  return {
    async save(
      roleId: number,
      grants: readonly InitialGrant[],
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<InitialGrantsResult> {
      if (!enabled) return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (busy) return { outcome: "NOT_DISPATCHED", reason: "BUSY" };
      if (
        !id(roleId) ||
        !Array.isArray(grants) ||
        grants.length < 1 ||
        grants.length > 32 ||
        new Set(grants.map((g) => g.permissionId)).size !== grants.length ||
        grants.some(
          (g) =>
            !id(g.permissionId) || !["OWN", "BRANCH"].includes(g.dataScope),
        ) ||
        signal?.aborted ||
        !current()
      )
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      const desired = grants
        .map((g) => ({ permissionId: g.permissionId, dataScope: g.dataScope }))
        .sort((a, b) => a.permissionId - b.permissionId);
      let observedFailureStatus: number | undefined;
      const http = createHttpClient(async (url, options) => {
        const response = await fetcher(url, options);
        if (response.status >= 400 && response.status <= 599)
          observedFailureStatus = response.status;
        return response;
      });
      busy = true;
      try {
        const token = await http({
          method: "GET",
          url: "/api/auth/csrf",
          expectedStatus: 200,
          signal,
          decode(v) {
            if (
              obj(v) &&
              v.headerName === "X-CSRF-TOKEN" &&
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
            ...(observedFailureStatus === undefined
              ? {}
              : { status: observedFailureStatus }),
          };
        if (signal?.aborted || !current())
          return { outcome: "NOT_DISPATCHED", reason: "STALE" };
        observedFailureStatus = undefined;
        const result = await http({
          method: "POST",
          url: `/api/roles/${roleId}/permissions/initial-v1`,
          expectedStatus: 201,
          csrfToken: token.data,
          signal,
          body: { contractVersion: 1, grants: desired },
          decode(v) {
            if (
              !obj(v) ||
              v.contractVersion !== 1 ||
              v.roleId !== roleId ||
              !Array.isArray(v.grants) ||
              v.grants.length !== desired.length
            )
              return;
            const clean: InitialGrant[] = [];
            for (const g of v.grants) {
              if (
                !obj(g) ||
                !id(g.permissionId) ||
                !["OWN", "BRANCH"].includes(String(g.dataScope))
              )
                return;
              clean.push({
                permissionId: g.permissionId,
                dataScope: g.dataScope as InitialGrant["dataScope"],
              });
            }
            clean.sort((a, b) => a.permissionId - b.permissionId);
            if (
              clean.some(
                (g, i) =>
                  g.permissionId !== desired[i].permissionId ||
                  g.dataScope !== desired[i].dataScope,
              )
            )
              return;
            return clean;
          },
        });
        if (!current() || signal?.aborted)
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        if (result.ok)
          return { outcome: "CONFIRMED", roleId, grants: result.data };
        if (result.error.kind === "http")
          return {
            outcome: [400, 401, 403, 404, 409].includes(result.error.status)
              ? "REJECTED"
              : "OUTCOME_UNKNOWN",
            reason: "HTTP",
            status: result.error.status,
          };
        return {
          outcome: "OUTCOME_UNKNOWN",
          reason: "UNCONFIRMED",
          ...(observedFailureStatus === undefined
            ? {}
            : { status: observedFailureStatus }),
        };
      } finally {
        busy = false;
      }
    },
  };
}
