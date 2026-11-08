export const tag = '"' + "A".repeat(64) + '"';
export const nextTag = '"' + "B".repeat(64) + '"';
export const secret = "PRIVATE-HOTEL-TOKEN-DETAIL";
export const floor = {
  id: 11,
  branchId: 1,
  code: "F1",
  name: "Server Floor",
  floorNumber: 1,
  description: null,
  isActive: true,
};
export const area = {
  id: 12,
  branchId: 1,
  code: "A1",
  name: "Server Area",
  floorId: 11,
  description: null,
  isActive: true,
};
export const editable = { ...floor, kind: "floor", floorId: null };
export const department = {
  id: 13,
  branchId: 1,
  code: "HK",
  name: "Housekeeping",
  description: null,
  managerUserId: null,
  isActive: true,
};
export const roomType = {
  id: 14,
  branchId: 1,
  code: "DLX",
  name: "Server Deluxe",
  description: null,
  standardCleaningMinutes: 30,
  defaultChecklistId: 91,
  standardCapacity: 2,
  isActive: true,
};
export const typeItem = { details: roomType, activeRooms: 0, canUpdate: true };
export const room = {
  id: 15,
  branchId: 1,
  floorId: 11,
  areaId: 12,
  roomTypeId: 14,
  roomNumber: "101",
  notes: null,
  operationalStatus: "AVAILABLE",
  isActive: true,
};
export const directoryItem = {
  id: 15,
  branchId: 1,
  roomNumber: "101",
  floor: { id: 11, code: "F1", name: "Server Floor", number: 1 },
  area: { id: 12, code: "A1", name: "Server Area" },
  roomType: { id: 14, code: "DLX", name: "Server Deluxe" },
  operationalStatusCode: "AVAILABLE",
  isActive: true,
};
export const rooms = {
  items: [directoryItem],
  page: 1,
  pageSize: 20,
  totalCount: 1,
};
export const roomDetail = {
  room: directoryItem,
  description: null,
  currentStatusEvidence: {
    availability: "RECORDED",
    reason: null,
    recordedAtUtc: null,
    source: null,
  },
  historyUrl: "/api/rooms/15/history?branchId=1",
};
export const roomHistory = {
  items: [
    {
      id: 101,
      fromStatusCode: "DIRTY",
      toStatusCode: "AVAILABLE",
      changedAtUtc: "2026-11-08T01:00:00Z",
      reason: null,
      source: { type: null, id: null, availability: "NOT_RECORDED" },
    },
  ],
  page: 1,
  pageSize: 20,
  totalCount: 1,
  coverage: "RECORDED_STATUS_EVENTS_ONLY",
};
export const roomTypes = {
  items: [typeItem],
  total: 1,
  page: 1,
  pageSize: 20,
  branchId: 1,
  isActive: null,
  search: null,
  sort: "code",
  descending: false,
};
export function structure(collection = "floors") {
  return {
    branch: { id: 1, code: "B1", name: "Server Branch", isActive: true },
    visibility: "FULL",
    nodeType: "branch",
    collection,
    availableCollections: ["floors", "areas", "departments"],
    isActive: null,
    selectedFloor: null,
    selectedArea: null,
    selectedRoom: null,
    selectedDepartment: null,
    floors:
      collection === "floors"
        ? { items: [floor], page: 1, pageSize: 20, totalCount: 1 }
        : null,
    areas:
      collection === "areas"
        ? { items: [area], page: 1, pageSize: 20, totalCount: 1 }
        : null,
    rooms: null,
    departments:
      collection === "departments"
        ? {
            items: [
              {
                id: 13,
                branchId: 1,
                code: "HK",
                name: "Housekeeping",
                isActive: true,
              },
            ],
            page: 1,
            pageSize: 20,
            totalCount: 1,
          }
        : null,
  };
}
export const requests = {
  createFloor: {
    code: "F1",
    name: "Server Floor",
    floorNumber: 1,
    description: null,
  },
  createArea: {
    code: "A1",
    name: "Server Area",
    floorId: 11,
    description: null,
  },
  updateFloor: {
    code: "F1",
    name: "Server Floor",
    description: null,
    reason: "Correct floor name",
  },
  updateArea: {
    code: "A1",
    name: "Server Area",
    floorId: 11,
    description: null,
    reason: "Correct area name",
  },
  createType: {
    branchId: 1,
    code: "DLX",
    name: "Server Deluxe",
    standardCleaningMinutes: 30,
    defaultChecklistId: 91,
    description: null,
    standardCapacity: 2,
  },
  updateType: {
    name: "Server Deluxe",
    standardCleaningMinutes: 30,
    defaultChecklistId: 91,
    description: null,
  },
  deactivateType: { confirmation: true, reason: "No longer used" },
  createRoom: {
    branchId: 1,
    floorId: 11,
    roomTypeId: 14,
    roomNumber: "101",
    areaId: 12,
    notes: null,
  },
  updateRoom: {
    floorId: 11,
    roomTypeId: 14,
    roomNumber: "101",
    areaId: 12,
    notes: null,
    reason: "Correct room information",
  },
  createDepartment: {
    code: "HK",
    name: "Housekeeping",
    description: null,
    managerUserId: null,
  },
  updateDepartment: {
    name: "Housekeeping",
    description: null,
    managerUserId: null,
  },
};
export const mutationCases = [
  ["createFloor", 11, "/api/branches/1/floors", floor],
  ["createArea", 12, "/api/branches/1/areas", area],
  ["updateFloor", 11, "/api/branches/1/floors/11", editable],
  [
    "updateArea",
    12,
    "/api/branches/1/areas/12",
    { ...area, kind: "area", floorNumber: null },
  ],
  ["createType", 14, "/api/room-types", roomType],
  ["updateType", 14, "/api/room-types/14?branchId=1", roomType],
  [
    "deactivateType",
    14,
    "/api/room-types/14/deactivate?branchId=1",
    { ...roomType, isActive: false },
  ],
  ["createRoom", 15, "/api/rooms", { ...room, operationalStatus: "DIRTY" }],
  ["updateRoom", 15, "/api/rooms/15?branchId=1", room],
  ["createDepartment", 13, "/api/branches/1/departments", department],
  ["updateDepartment", 13, "/api/branches/1/departments/13", department],
];
