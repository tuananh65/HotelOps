import { createHttpClient } from "../auth/http";
import { id, obj, role, permission, type RoleDto, type PermissionDto } from "./contracts";
export interface ConfiguredGrant { permissionId: number; dataScope: string }
export interface PermissionConfiguration { role: RoleDto; grants: ConfiguredGrant[]; catalog: PermissionDto[] }
export type ConfigurationResult = { ok: true; data: PermissionConfiguration; etag: string }
  | { ok: false; status?: number; unknown: boolean; lastAdministrator?: boolean };
function decode(value: unknown, roleId: number): PermissionConfiguration | undefined {
  if (!obj(value) || !Array.isArray(value.grants) || !Array.isArray(value.catalog)
    || value.grants.length > 2000 || value.catalog.length > 2000) return;
  const summary = role(value.role);
  const catalog = value.catalog.map(permission);
  if (!summary || summary.roleId !== roleId || catalog.some(p => !p)
    || new Set(catalog.map(p => p!.permissionId)).size !== catalog.length) return;
  const grants: ConfiguredGrant[] = [];
  for (const grant of value.grants) {
    if (!obj(grant) || !id(grant.permissionId) || typeof grant.dataScope !== "string"
      || !["ALL", "BRANCH", "OWN", "DEPARTMENT"].includes(grant.dataScope)
      || !catalog.some(p => p!.permissionId === grant.permissionId) || grants.some(g => g.permissionId === grant.permissionId)) return;
    grants.push({ permissionId: grant.permissionId, dataScope: grant.dataScope });
  }
  return summary.permissionGrantCount === grants.length ? { role: summary, grants, catalog: catalog as PermissionDto[] } : undefined;
}
export function createRolePermissionConfigurationApi(fetcher: typeof fetch = globalThis.fetch.bind(globalThis)) {
  async function request(roleId: number, signal: AbortSignal, desired?: ConfiguredGrant[], expected?: string,
    current: () => boolean = () => true): Promise<ConfigurationResult> {
    const writing = desired !== undefined;
    if (!id(roleId) || signal.aborted || !current() || (writing && (!expected || !/^"[0-9A-F]{16}"$/.test(expected)
      || desired.length > 2000 || new Set(desired.map(g => g.permissionId)).size !== desired.length
      || desired.some(g => !id(g.permissionId) || !["ALL", "BRANCH", "OWN"].includes(g.dataScope)))))
      return { ok: false, unknown: false, status: 400 };
    let csrfToken: string | undefined;
    let status: number | undefined;
    let tag: string | null = null;
    let lastAdministrator = false;
    if (writing) {
      const bootstrap = createHttpClient(async (url, init) => {
        const response = await fetcher(url, init); status = response.status; return response;
      });
      const token = await bootstrap({ method: "GET", url: "/api/auth/csrf", expectedStatus: 200, signal,
        decode(v) { return obj(v) && v.headerName === "X-CSRF-TOKEN" && typeof v.csrfToken === "string"
          && /^[\x21-\x7e]{1,8192}$/.test(v.csrfToken) ? v.csrfToken : undefined; } });
      if (!token.ok) return { ok: false, status, unknown: false };
      csrfToken = token.data;
    }
    if (signal.aborted || !current()) return { ok: false, unknown: false };
    status = undefined;
    const client = createHttpClient(async (url, init) => {
      const headers = new Headers(init?.headers);
      if (writing) headers.set("If-Match", expected!);
      const response = await fetcher(url, { ...init, headers });
      status = response.status; tag = response.headers.get("ETag");
      if (writing && status === 409) {
        try {
          const problem: unknown = await response.clone().json();
          lastAdministrator = obj(problem) && problem.title === "LAST_DURABLE_ADMIN";
        } catch { /* Unknown responses never authorize another save. */ }
      }
      return response;
    });
    const result = await client({ method: writing ? "PUT" : "GET", url: `/api/roles/${roleId}/permission-configuration`,
      expectedStatus: 200, signal, csrfToken, body: writing ? { grants: desired } : undefined,
      decode(v) {
        const data = decode(v, roleId);
        if (!data || !tag || !/^"[0-9A-F]{16}"$/.test(tag) || (writing && (tag === expected
          || data.grants.length !== desired.length || desired.some(g => !data.grants.some(a => a.permissionId === g.permissionId && a.dataScope === g.dataScope))))) return;
        return { data, etag: tag };
      } });
    if (!current() || signal.aborted) return { ok: false, unknown: writing };
    return result.ok ? { ok: true, ...result.data }
      : { ok: false, status, lastAdministrator, unknown: writing && !(status && [400,401,403,404,409,412,428].includes(status)) };
  }
  return { read: (id: number, signal: AbortSignal) => request(id, signal),
    save: (id: number, grants: ConfiguredGrant[], etag: string, signal: AbortSignal, current: () => boolean) => request(id,signal,grants,etag,current) };
}
