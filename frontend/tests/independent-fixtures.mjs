export const secret = "PRIVATE-INDEPENDENT-FIXTURE";
export const session = (id = "7", restricted = false) => ({
  user: { id, username: "fixture", name: "Confirmed fixture" },
  expiresAt: "2026-12-01T12:00:00Z",
  absoluteExpiresAt: "2026-12-01T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: secret,
});
export function structureReply(url, management = true) {
  const u = new URL(url, "https://offline.test"),
    q = u.searchParams,
    branchId = Number(u.pathname.split("/")[3]),
    nodeType = q.get("nodeType") ?? "branch",
    nodeId = Number(q.get("nodeId")),
    collection =
      q.get("collection") ??
      (nodeType === "branch"
        ? "floors"
        : nodeType === "floor"
          ? "areas"
          : nodeType === "area"
            ? "rooms"
            : null),
    page = Number(q.get("page") ?? 1),
    pageSize = Number(q.get("pageSize") ?? 20),
    isActive = q.has("isActive") ? q.get("isActive") === "true" : null;
  const floor = (id) => ({
    id,
    branchId,
    code: "F" + id,
    name: "Floor " + id,
    floorNumber: id,
    description: null,
    isActive: isActive ?? true,
  });
  const area = (id) => ({
    id,
    branchId,
    code: "A" + id,
    name: "Area " + id,
    floorId: nodeType === "floor" ? nodeId : 11,
    description: null,
    isActive: isActive ?? true,
  });
  const room = {
    id: 15,
    branchId,
    floorId: 11,
    areaId: nodeType === "area" ? nodeId : 12,
    roomNumber: "101",
    isActive: isActive ?? true,
  };
  const department = {
    id: 13,
    branchId,
    code: "HK",
    name: "Server Department",
    isActive: true,
  };
  const availableCollections =
    nodeType === "branch"
      ? management
        ? ["floors", "areas", "departments"]
        : ["floors", "areas"]
      : nodeType === "floor"
        ? ["areas", "rooms"]
        : nodeType === "area"
          ? ["rooms"]
          : [];
  const data = {
    branch: {
      id: branchId,
      code: "B" + branchId,
      name: "Branch " + branchId,
      isActive: true,
    },
    visibility: management ? "MANAGEMENT" : "ROOM_VIEW",
    nodeType,
    collection,
    availableCollections,
    isActive,
    selectedFloor: nodeType === "floor" ? floor(nodeId) : null,
    selectedArea: nodeType === "area" ? area(nodeId) : null,
    selectedRoom: nodeType === "room" ? room : null,
    selectedDepartment: nodeType === "department" ? department : null,
    floors: null,
    areas: null,
    rooms: null,
    departments: null,
  };
  if (collection) {
    const items =
      collection === "floors"
        ? Array.from({ length: page === 1 ? 20 : 1 }, (_, i) =>
            floor(page === 1 ? 11 + i : 31),
          )
        : collection === "areas"
          ? Array.from({ length: page === 1 ? 20 : 1 }, (_, i) =>
              area(page === 1 ? 12 + i : 32),
            )
          : collection === "rooms"
            ? [room]
            : [department];
    data[collection] = {
      items,
      page,
      pageSize,
      totalCount: ["floors", "areas"].includes(collection) ? 21 : 1,
    };
  }
  return data;
}
