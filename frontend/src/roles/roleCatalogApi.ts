import { createHttpClient } from "../auth/http";
import {
  id,
  obj,
  role,
  permission,
  detail,
  grants,
  page,
  type RoleDto,
  type PermissionDto,
  type Page,
  type DetailDto,
} from "./contracts";
export type CatalogResult<T> =
  { ok: true; data: T } | { ok: false; status?: number; kind: string };
/** GET only. Sanitizes failures, no retries or raw ProblemDetails exposure. */
export function createRoleCatalogApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  // Existing auth transport intentionally rejects query strings; construct only validated query
  // fields here, then delegate credential/cache/JSON behavior through that transport.
  async function get<T>(
    path: string,
    decode: (v: unknown) => T | undefined,
    signal?: AbortSignal,
    query = "",
  ): Promise<CatalogResult<T>> {
    const request = createHttpClient((url, init) =>
      fetcher(String(url) + query, init),
    );
    const r = await request({
      method: "GET",
      url: path,
      expectedStatus: 200,
      decode,
      signal,
    });
    if (signal?.aborted) return { ok: false, kind: "cancelled" };
    if (r.ok) return { ok: true, data: r.data };
    return {
      ok: false,
      kind: r.error.kind,
      ...(r.error.kind === "http" ? { status: r.error.status } : {}),
    };
  }
  return {
    roles(
      p = 1,
      search = "",
      active = "",
      signal?: AbortSignal,
    ): Promise<CatalogResult<Page<RoleDto>>> {
      if (
        !id(p) ||
        p > 1000000 ||
        search.length > 100 ||
        /[\x00-\x1f\x7f]/.test(search) ||
        !["", "true", "false"].includes(active)
      )
        return Promise.resolve({ ok: false, kind: "invalid-request" });
      const q = new URLSearchParams({ page: String(p), pageSize: "25" });
      if (search) q.set("search", search);
      if (active) q.set("isActive", active);
      return get("/api/roles", (v) => page(v, p, role), signal, "?" + q);
    },
    detail(
      roleId: number,
      signal?: AbortSignal,
    ): Promise<CatalogResult<DetailDto>> {
      return id(roleId)
        ? get(`/api/roles/${roleId}`, (v) => detail(v, roleId), signal)
        : Promise.resolve({ ok: false, kind: "invalid-request" });
    },
    grants(roleId: number, signal?: AbortSignal) {
      return id(roleId)
        ? get(
            `/api/roles/${roleId}/permissions`,
            (v) => {
              if (!obj(v) || v.roleId !== roleId) return;
              const g = grants(v.grants);
              return g ? { roleId, grants: g } : undefined;
            },
            signal,
          )
        : Promise.resolve({ ok: false as const, kind: "invalid-request" });
    },
    permissions(
      p = 1,
      signal?: AbortSignal,
    ): Promise<CatalogResult<Page<PermissionDto>>> {
      return id(p) && p <= 1000000
        ? get(
            "/api/permissions/catalog",
            (v) => page(v, p, permission),
            signal,
            `?page=${p}&pageSize=25`,
          )
        : Promise.resolve({ ok: false, kind: "invalid-request" });
    },
  };
}
