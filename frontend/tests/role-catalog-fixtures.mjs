export const role = (id = 1) => ({
  roleId: id,
  roleCode: "ROLE_" + id,
  roleName: "Role " + id,
  description: null,
  isSystemRole: true,
  isActive: true,
  assignedUserCount: 2,
  permissionGrantCount: 1,
});
export const permission = (id = 1) => ({
  permissionId: id,
  permissionCode: "ROOM_VIEW_" + id,
  permissionName: "View " + id,
  moduleCode: "ROOM",
  resourceCode: "room",
  actionCode: "view",
  description: null,
});
export const detail = (id = 1) => ({
  role: role(id),
  grants: [
    { rolePermissionId: 1, permission: permission(), dataScope: "BRANCH" },
  ],
});
export const page = (items) => ({
  items,
  page: 1,
  pageSize: 25,
  total: items.length,
});
