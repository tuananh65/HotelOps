import fs from "node:fs";
import { bundle, entity, session } from "../api-test-helpers.mjs";
export const catalog = JSON.parse(
  fs.readFileSync(new URL("../../src/catalog.json", import.meta.url), "utf8"),
);
export const { resourcePaths, actionContracts } = await bundle(
  "src/api/endpoints.ts",
  "fixture-endpoints",
);
export const grants = Object.keys(catalog.ucs).map((useCase) => ({
  useCase,
  branchIds: ["1"],
  departmentIds: ["2"],
}));
export const validSession = session(grants);
validSession.principal.roleNames = [
  "Administrator",
  "Operations Manager",
  "Owner",
  "Housekeeping Supervisor",
  "Housekeeping Staff",
  "Maintenance Lead",
  "Maintenance Staff",
  "Storekeeper",
  "Department Head",
];
const identifiers = {
  users: "7",
  roles: "role1",
  branches: "1",
  locations: "floor1",
  roomTypes: "rt1",
  rooms: "201",
  departments: "2",
  cleaning: "cl1",
  rework: "rw1",
  checklists: "ch1",
  inspections: "in1",
  incidents: "101",
  schedules: "sched1",
  reference: "ref1",
  assets: "asset1",
  warehouses: "w1",
  items: "item1",
  units: "unit1",
  transactions: "doc1",
  stocktakes: "st1",
  materials: "mr1",
  replenishments: "rep1",
  tasks: "task1",
  shifts: "shift1",
  handovers: "hand1",
  requests: "req1",
  notifications: "n1",
  maintenance: "mw1",
  audit: "aud1",
  ledger: "ledger1",
  intervals: "interval1",
  workdays: "wd1",
  corrections: "corr1",
  occurrences: "occ1",
  blocks: "block1",
};
export const ids = identifiers;
export const collections = Object.fromEntries(
  Object.keys(resourcePaths).map((key) => [
    key,
    [
      entity(ids[key], {
        name:
          key === "rooms"
            ? "101"
            : key === "branches"
              ? "Bui Vien"
              : key === "departments"
                ? "Housekeeping"
                : key === "users"
                  ? "Tester"
                  : key === "roles"
                    ? "Administrator"
                    : key,
        code: key.toUpperCase(),
        statusCode: "ACTIVE",
        allowedActions: [
          ...Object.entries(actionContracts)
            .filter(([name]) => name.startsWith(key + ":"))
            .map(([, contract]) => contract.id),
          key + ".criteria",
          key + ".assign",
          key + ".reassign",
        ],
        attributes: {
          description: "Server fixture description",
          roomId: "201",
          assigneeId: "7",
          requesterId: "7",
          criteria: "Check cleanliness",
          dueAt: "2099-01-01T10:00:00Z",
          roleNames: validSession.principal.roleNames,
          permissions: Object.keys(catalog.ucs),
        },
      }),
    ],
  ]),
);
const set = (key, status, attributes) => {
  collections[key][0].statusCode = status;
  Object.assign(collections[key][0].attributes, attributes);
};
set("locations", "ACTIVE", { kindCode: "FLOOR" });
set("rooms", "PENDING_INSPECTION", {
  floorId: "floor1",
  roomTypeId: "rt1",
  active: true,
  history: [],
});
set("users", "ACTIVE", {
  username: "tester",
  email: "tester@example.test",
  phone: "0000000000",
  temporary: false,
});
set("roles", "ACTIVE", { permissions: Object.keys(catalog.ucs) });
set("checklists", "PUBLISHED", {
  criteria: "Check cleanliness",
  checklistVersion: 1,
});
set("roomTypes", "ACTIVE", { checklistId: "ch1", standardCleaningMinutes: 30 });
set("cleaning", "IN_PROGRESS", {
  cleaningType: "Turnover",
  completedItems: [],
});
set("rework", "PENDING_ACCEPTANCE", {
  cleaningTaskId: "cl1",
  inspectionId: "in1",
  defects: "Dust",
});
set("inspections", "INSPECTING", {
  cleaningTaskId: "cl1",
  checklistId: "ch1",
  checklistVersion: 1,
  results: [{ text: "Check cleanliness", resultCode: "PASS", defect: "" }],
});
set("incidents", "IN_PROGRESS", {
  discoveredAt: "2026-10-16T08:00:00Z",
  severityCode: "HIGH",
});
set("schedules", "ACTIVE", {
  interval: 1,
  intervalUnitCode: "MONTHS",
  assetId: "asset1",
  nextDue: "2099-01-01T00:00:00Z",
});
set("assets", "ACTIVE", {
  conditionCode: "IN_USE",
  category: "Equipment",
  model: "Model",
  serial: "Serial",
  location: "Room 101",
});
set("units", "ACTIVE", { factor: 1 });
set("items", "ACTIVE", {
  unitId: "unit1",
  minimum: 5,
  warehouseId: "w1",
  category: "Cleaning",
  conversions: [],
});
const line = {
  id: "line1",
  itemId: "item1",
  unitId: "unit1",
  quantity: 5,
  approvedQuantity: 5,
  issuedQuantity: 0,
  receivedQuantity: 0,
  systemQuantity: 20,
  conversionFactor: 1,
};
for (const key of ["transactions", "stocktakes", "materials", "replenishments"])
  Object.assign(collections[key][0].attributes, {
    warehouseId: "w1",
    lines: [line],
  });
set("transactions", "DRAFT", {
  transactionTypeCode: "RECEIPT",
  source: "Supplier",
});
set("stocktakes", "IN_PROGRESS", { cutoffAt: "2026-10-16T08:00:00Z" });
set("materials", "PENDING_APPROVAL", {});
set("replenishments", "REQUESTED", {});
set("tasks", "ASSIGNED", { category: "Operations", completedItems: [] });
set("shifts", "SCHEDULED", {
  date: "2026-10-16",
  startAt: "08:00",
  endAt: "17:00",
  assigneeIds: ["7"],
});
set("handovers", "PENDING_ACKNOWLEDGMENT", {
  sender: "7",
  receiverId: "7",
  snapshot: [],
});
set("requests", "IN_PROGRESS", {
  receivingDepartmentId: "2",
  requestType: "Operational support",
  linkedRecords: [],
});
set("notifications", "READ", {
  recipient: "7",
  priorityCode: "NORMAL",
  type: "Assignment",
  relatedKey: "tasks",
  relatedId: "task1",
});
set("maintenance", "ASSIGNED", { schedule: "sched1", assetId: "asset1" });
set("occurrences", "ASSIGNED", { schedule: "sched1" });
set("audit", "RECORDED", {
  actor: "Tester",
  actorId: "7",
  sourceResource: "rooms",
  sourceRecordId: "201",
  action: "CREATE",
  before: null,
  after: { name: "101" },
});
set("ledger", "POSTED", {
  warehouseId: "w1",
  itemId: "item1",
  quantityDelta: 20,
  documentId: "doc1",
});
set("intervals", "CLOSED", {
  startAt: "2026-10-16T08:00:00Z",
  endAt: "2026-10-16T09:00:00Z",
  sourceResource: "tasks",
  sourceRecordId: "task1",
});
set("workdays", "WORKING", { startAt: "2026-10-16T07:00:00Z" });
set("corrections", "PENDING", {
  startAt: "2026-10-16T08:00:00Z",
  endAt: "2026-10-16T09:00:00Z",
  reason: "Correct recorded time",
});
set("blocks", "REMOVED", {});
export const context = {
  snapshot: "2026-10-16T10:00:00Z",
  collections,
  metrics: {
    readyRooms: 0,
    activeRooms: 1,
    openIncidents: 1,
    overdueTasks: 0,
    lowStockItems: 0,
  },
  balances: [{ warehouseId: "w1", itemId: "item1", quantity: 20, minimum: 5 }],
  missingSources: [],
};
export const actualIncident = {
  incidentId: 101,
  incidentNo: "INC-101",
  branchId: 1,
  branchName: "Bui Vien",
  roomId: 201,
  roomNumber: "101",
  areaId: null,
  areaName: null,
  assetId: null,
  assetName: null,
  incidentTitle: "Leaking tap",
  severityId: 1,
  severityName: "High",
  priorityId: 1,
  priorityName: "Normal",
  status: "IN_PROGRESS",
  reportedBy: 7,
  reportedByName: "Tester",
  discoveredAt: "2026-10-16T08:00:00Z",
  reportedAt: "2026-10-16T08:00:00Z",
  resolvedAt: null,
  closedAt: null,
  description: "Leaking tap",
  closureNote: null,
  events: [],
};
export const report = {
  snapshotId: "snapshot1",
  snapshot: context.snapshot,
  key: "rooms",
  from: "2026-09-16",
  to: "2026-10-16",
  columns: ["name", "status"],
  totalCount: 1,
  rows: [
    {
      id: "report1",
      name: "101",
      status: "Pending Inspection",
      branch: "Bui Vien",
      department: "Housekeeping",
      createdAt: context.snapshot,
      version: 0,
    },
  ],
};
export function fixtureResponse(url, method = "GET", body = {}) {
  const path = new URL(url, "https://api.test").pathname;
  if (path === "/api/auth/session" || path === "/api/auth/login")
    return validSession;
  if (path === "/api/auth/logout" || path === "/api/auth/password")
    return undefined;
  if (
    (path === "/api/users" && method === "POST") ||
    (path === "/api/users/7/reset-password" && method === "POST")
  )
    return {
      record: {
        ...collections.users[0],
        id: path === "/api/users" ? "user-created" : "7",
      },
      temporaryPassword: "FixtureOnly!23",
      validForHours: 24,
    };
  if (
    path.startsWith("/api/ui/screens/") ||
    path === "/api/dashboards/operations"
  )
    return context;
  if (path.startsWith("/api/reports/")) return report;
  if (path === "/api/incidents") return [actualIncident];
  if (path === "/api/incidents/101" && method === "GET") return actualIncident;
  for (const [key, base] of Object.entries(resourcePaths)) {
    if (path === base && method === "GET")
      return {
        items: collections[key],
        page: 1,
        pageSize: 100,
        totalCount: collections[key].length,
        snapshot: context.snapshot,
      };
    if (path === base && method === "POST")
      return entity(key + "-created", {
        ...collections[key][0],
        id: key + "-created",
        name: body.name ?? collections[key][0].name,
        attributes: body.attributes ?? {},
      });
    if (path.startsWith(base + "/")) return collections[key][0];
  }
  throw new Error("Unexpected fixture route " + method + " " + path);
}
