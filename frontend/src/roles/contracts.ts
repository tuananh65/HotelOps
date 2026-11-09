export interface RoleDto {
  roleId: number;
  roleCode: string;
  roleName: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  assignedUserCount: number;
  permissionGrantCount: number;
}
export interface PermissionDto {
  permissionId: number;
  permissionCode: string;
  permissionName: string;
  moduleCode: string;
  resourceCode: string;
  actionCode: string;
  description: string | null;
}
export interface GrantDto {
  rolePermissionId: number;
  permission: PermissionDto;
  dataScope: "ALL" | "BRANCH" | "OWN";
}
export interface DetailDto {
  role: RoleDto;
  grants: GrantDto[];
}
export interface Page<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
export const obj = (x: unknown): x is Record<string, unknown> =>
  !!x && typeof x === "object" && !Array.isArray(x);
export const id = (x: unknown): x is number =>
  typeof x === "number" && Number.isSafeInteger(x) && x > 0;
const text = (x: unknown): x is string =>
  typeof x === "string" && x.length > 0 && x.length <= 4000;
const nullable = (x: unknown): x is string | null =>
  x === null || (typeof x === "string" && x.length <= 10000);
const count = (x: unknown): x is number =>
  typeof x === "number" && Number.isSafeInteger(x) && x >= 0;
export function role(x: unknown): RoleDto | undefined {
  if (
    !obj(x) ||
    !id(x.roleId) ||
    !text(x.roleCode) ||
    !text(x.roleName) ||
    !nullable(x.description) ||
    typeof x.isSystemRole !== "boolean" ||
    typeof x.isActive !== "boolean" ||
    !count(x.assignedUserCount) ||
    !count(x.permissionGrantCount)
  )
    return;
  return {
    roleId: x.roleId,
    roleCode: x.roleCode,
    roleName: x.roleName,
    description: x.description,
    isSystemRole: x.isSystemRole,
    isActive: x.isActive,
    assignedUserCount: x.assignedUserCount,
    permissionGrantCount: x.permissionGrantCount,
  };
}
export function permission(x: unknown): PermissionDto | undefined {
  if (
    !obj(x) ||
    !id(x.permissionId) ||
    !text(x.permissionCode) ||
    !text(x.permissionName) ||
    !text(x.moduleCode) ||
    !text(x.resourceCode) ||
    !text(x.actionCode) ||
    !nullable(x.description)
  )
    return;
  return {
    permissionId: x.permissionId,
    permissionCode: x.permissionCode,
    permissionName: x.permissionName,
    moduleCode: x.moduleCode,
    resourceCode: x.resourceCode,
    actionCode: x.actionCode,
    description: x.description,
  };
}
export function grants(v: unknown): GrantDto[] | undefined {
  if (!Array.isArray(v) || v.length > 10000) return;
  const seen = new Set<number>();
  const out: GrantDto[] = [];
  for (const x of v) {
    if (
      !obj(x) ||
      !id(x.rolePermissionId) ||
      seen.has(x.rolePermissionId) ||
      !["ALL", "BRANCH", "OWN"].includes(x.dataScope as string)
    )
      return;
    const p = permission(x.permission);
    if (!p) return;
    seen.add(x.rolePermissionId);
    out.push({
      rolePermissionId: x.rolePermissionId,
      permission: p,
      dataScope: x.dataScope as GrantDto["dataScope"],
    });
  }
  return out;
}
export function detail(v: unknown, expectedId: number): DetailDto | undefined {
  if (!obj(v)) return;
  const r = role(v.role),
    g = grants(v.grants);
  if (
    !r ||
    r.roleId !== expectedId ||
    !g ||
    r.permissionGrantCount !== g.length
  )
    return;
  return { role: r, grants: g };
}
export function page<T>(
  v: unknown,
  p: number,
  decode: (x: unknown) => T | undefined,
): Page<T> | undefined {
  if (
    !obj(v) ||
    v.page !== p ||
    v.pageSize !== 25 ||
    !count(v.total) ||
    !Array.isArray(v.items) ||
    v.items.length > 25
  )
    return;
  const items: T[] = [];
  for (const x of v.items) {
    const item = decode(x);
    if (!item) return;
    items.push(item);
  }
  if (items.length > v.total) return;
  return { items, page: p, pageSize: 25, total: v.total };
}
