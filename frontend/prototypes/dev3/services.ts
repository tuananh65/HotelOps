import {
  actions,
  domains,
  screenById,
  type Action,
  type Domain,
  type RecordData,
} from "./domain";
import { createDatabase } from "./mocks/createDatabase";
import { enrichDatabase } from "./mocks/enrichDatabase";
import { populatePrototype } from "./mocks/prototypeData";
export type Database = { [key: string]: RecordData[] };
export type Session = { userId: string; expires: number; restricted: boolean };
const storageKey = "hotelops-public-prototype-v1";
const now = () => new Date().toISOString();
const id = () => crypto.randomUUID();
const closed = [
  "Closed",
  "Completed",
  "Cancelled",
  "Failed",
  "Reconciled",
  "Received",
  "Fully Issued",
  "Retired",
  "Rejected",
];
const record = (
  key: string,
  name: string,
  extra: Partial<RecordData> = {},
): RecordData => ({
  id: id(),
  name,
  status: "Active",
  branch: "Bui Vien",
  department: "Operations",
  version: 1,
  createdAt: now(),
  code: key,
  ...extra,
});
function load(): Database {
  try {
    const raw = localStorage.getItem(storageKey);
    if (raw) {
      const db = JSON.parse(raw);
      db.occurrences ||= [];
      db.maintenance ||= [];
      db.alertEpisodes ||= [];
      populatePrototype(enrichDatabase(db));
      calculateRooms(db);
      localStorage.setItem(storageKey, JSON.stringify(db));
      return db;
    }
  } catch {}
  const fresh = populatePrototype(
    enrichDatabase(createDatabase(balance, calculateRooms)),
  );
  calculateRooms(fresh);
  localStorage.setItem(storageKey, JSON.stringify(fresh));
  return fresh;
}
let database = load();
const listeners = new Set<() => void>();
export const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};
export const snapshot = () => database;
/** Resolve deterministic demo URLs in databases created before the refactor. */
export function resolveDemoRecordId(key: string, requested: string): string {
  const rows =
    key === "maintenance"
      ? [...database.incidents, ...database.occurrences]
      : database[key] || [];
  if (rows.some((r) => r.id === requested)) return requested;
  const match = /^(BV|CT)-(.+)-(\d+)$/.exec(requested);
  if (!match) return requested;
  const branch = match[1] === "BV" ? "Bui Vien" : "Cao Thang";
  return (
    rows.filter(
      (r) =>
        r.branch === branch &&
        String(r.code)
          .replace(/[^A-Za-z0-9-]/g, "")
          .toUpperCase() === match[2],
    )[Number(match[3]) - 1]?.id || requested
  );
}
function commit(db: Database) {
  localStorage.setItem(storageKey, JSON.stringify(db));
  database = db;
  listeners.forEach((f) => f());
}
export function currentSession(): Session | null {
  return {
    userId: database.users[0].id,
    expires: Number.MAX_SAFE_INTEGER,
    restricted: false,
  };
}
export function currentUser(): RecordData | null {
  return database.users[0];
}
export function can(uc: string, user = currentUser()): boolean {
  return true;
}
export function inScope(r: RecordData, user = currentUser()) {
  return true;
}
export function visible(key: string, user = currentUser()): RecordData[] {
  const records =
    key === "maintenance"
      ? [
          ...database.incidents.filter((r) => r.assignee),
          ...database.occurrences,
        ]
      : database[key] || [];
  const pathname =
    typeof window === "undefined" ? "" : window.location.pathname;
  const listView =
    domains.some((domain) => screenById(domain.list).route === pathname) ||
    [
      "/dashboard",
      "/room-readiness",
      "/dev/screens",
      "/workload",
      "/overdue",
    ].includes(pathname);
  const branch = branchContext();
  // This is an explicit presentation filter. Direct detail URLs remain accessible.
  return listView &&
    branch !== "All branches" &&
    !["items", "units", "roles", "reference", "checklists"].includes(key)
    ? records.filter((row) => row.branch === branch)
    : records;
}
function audit(
  db: Database,
  key: string,
  action: string,
  before: any,
  after: any,
  user = currentUser(),
) {
  const clean = (obj: any) =>
    obj
      ? Object.fromEntries(
          Object.entries(obj).filter(
            ([k]) => !/(password|token|failed|lockedUntil)/i.test(k),
          ),
        )
      : null;
  db.audit.unshift(
    record("AUD", `${action} · ${after?.name || before?.name || key}`, {
      branch: after?.branch || before?.branch || user?.branch || "Bui Vien",
      actor: user?.name || "System",
      actorId: user?.id,
      entity: key,
      recordId: after?.id || before?.id,
      action,
      before: clean(before),
      after: clean(after),
      status: "Recorded",
    }),
  );
}
export function login(username: string, password: string) {
  return currentUser()!;
}
export function logout() {
  return;
}
export function touch() {
  return;
}
export function changePassword(current: string, next: string, confirm: string) {
  if (next !== confirm) throw Error("Passwords must match.");
  return;
}
export function balance(
  db: Database,
  warehouse: string,
  item: string,
  at?: string,
) {
  return db.ledger
    .filter(
      (l) =>
        l.warehouse === warehouse &&
        l.item === item &&
        (!at || l.createdAt <= at),
    )
    .reduce((a, l) => a + Number(l.delta), 0);
}
export function minimumStock(
  item: RecordData,
  warehouse: string,
): number | undefined {
  return item.minimums ? item.minimums[warehouse] : item.minimum;
}
export function calculateRooms(db: Database) {
  for (const r of db.rooms) {
    const old = r.status;
    const cleaning = db.cleaning
      .filter((c) => c.room === r.id && c.status !== "Cancelled")
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0];
    const rework = db.rework.find(
      (w) => w.room === r.id && !closed.includes(w.status),
    );
    r.status =
      r.active === false
        ? "Inactive"
        : db.blocks.some((b) => b.room === r.id && b.status === "Effective")
          ? "Blocked"
          : rework && rework.status !== "Pending Reinspection"
            ? "Rework Required"
            : cleaning?.status === "In Progress"
              ? "Cleaning In Progress"
              : rework?.status === "Pending Reinspection"
                ? "Pending Inspection"
                : cleaning?.status === "Pending Inspection"
                  ? "Pending Inspection"
                  : cleaning?.status === "Rework Required"
                    ? "Rework Required"
                    : cleaning &&
                        ["Assigned", "Pending Assignment", "On Hold"].includes(
                          cleaning.status,
                        )
                      ? "Pending Cleaning"
                      : cleaning?.status === "Completed" || r.passed
                        ? "Ready"
                        : "Pending Cleaning";
    if (old !== r.status) {
      r.history = [
        ...(r.history || []),
        { from: old, to: r.status, time: now() },
      ];
      r.version++;
    }
  }
}
function requirePermission(uc: string) {
  return;
}
function validateReferences(db: Database, input: any) {
  for (const [field, key] of Object.entries({
    room: "rooms",
    floor: "locations",
    roomType: "roomTypes",
    warehouse: "warehouses",
    destination: "warehouses",
    asset: "assets",
    checklist: "checklists",
    assignee: "users",
    receiver: "users",
    parent: "locations",
  })) {
    if (!input[field]) continue;
    const r = db[key].find((r) => r.id === input[field]);
    if (
      !r ||
      !inScope(r) ||
      r.status === "Inactive" ||
      (input.branch && r.branch !== input.branch && field !== "destination")
    )
      throw Error(`${field}: select an active record in the same branch.`);
  }
}
function ensureUnique(db: Database, d: Domain, input: any, exclude?: string) {
  for (const key of d.key === "users"
    ? ["username", "email"]
    : d.key === "rooms"
      ? ["name"]
      : ["code"]) {
    if (
      input[key] &&
      db[d.key].some(
        (r) =>
          r.id !== exclude &&
          String(r[key]).trim().toLowerCase() ===
            String(input[key]).trim().toLowerCase() &&
          (![
            "locations",
            "roomTypes",
            "rooms",
            "departments",
            "warehouses",
          ].includes(d.key) ||
            r.branch === input.branch),
      )
    )
      throw Error(`${key}: already exists.`);
  }
}
function cleanInput(input: any) {
  return Object.fromEntries(
    Object.entries(input).map(([k, v]) => [
      k,
      typeof v === "string" ? v.trim() : v,
    ]),
  );
}
function materialLines(db: Database, lines: any[], original?: any[]) {
  return lines.map((l) => {
    const item = db.items.find((i) => i.id === l.item);
    if (!item) throw Error("Select an active inventory item.");
    const unit = l.unit || item.unit;
    const stored = original?.find((v) => v.item === l.item && v.unit === unit);
    const factor =
      stored?.factor ??
      (unit === item.unit
        ? 1
        : item.conversions?.find((c: any) => c.unit === unit)?.factor);
    if (!(Number(factor) > 0))
      throw Error("Unit has no valid conversion to the item base unit.");
    return { ...l, unit, factor: Number(factor) };
  });
}
export function save(d: Domain, input: any, existing?: RecordData): RecordData {
  requirePermission(existing ? d.updateUC || "" : d.createUC || "");
  const user = currentUser()!;
  const db = structuredClone(database);
  input = cleanInput(input);
  if (input.lines && d.key !== "stocktakes")
    input.lines = materialLines(db, input.lines, existing?.lines);
  for (const f of d.fields) {
    if (
      f.required &&
      (input[f.key] === undefined ||
        input[f.key] === "" ||
        (Array.isArray(input[f.key]) && !input[f.key].length))
    )
      throw Error(`${f.label} is required.`);
  }

  validateReferences(db, input);
  ensureUnique(db, d, input, existing?.id);
  if (
    input.due &&
    input.plannedStart &&
    new Date(input.due) < new Date(input.plannedStart)
  )
    throw Error("Deadline must not precede planned start.");
  for (const k of ["minimum", "minutes", "interval", "factor"])
    if (
      input[k] !== undefined &&
      (Number(input[k]) < 0 ||
        (["interval", "factor"].includes(k) && Number(input[k]) === 0))
    )
      throw Error(`${k}: invalid quantity.`);
  if (
    input.lines &&
    new Set(input.lines.map((l: any) => l.item)).size !== input.lines.length
  )
    throw Error("Each item may appear only once in a document.");
  if (input.lines?.some((l: any) => !l.item || !(Number(l.quantity) > 0)))
    throw Error("Every material line requires an item and positive quantity.");
  if (
    d.key === "cleaning" &&
    !existing &&
    db.cleaning.some(
      (c) =>
        c.room === input.room && !["Completed", "Cancelled"].includes(c.status),
    )
  )
    throw Error("This room already has an open cleaning task.");
  if (d.key === "incidents" && !input.room && !input.area && !input.asset)
    throw Error("Select a room, area or asset.");
  if (
    d.key === "stocktakes" &&
    !existing &&
    db.stocktakes.some(
      (s) => s.warehouse === input.warehouse && !closed.includes(s.status),
    )
  )
    throw Error("An open stocktake already exists for this warehouse.");
  if (d.key === "locations" && input.parent) {
    let p = db.locations.find((p) => p.id === input.parent);
    const visited = new Set([existing?.id]);
    while (p) {
      if (visited.has(p.id)) throw Error("Parent chain creates a cycle.");
      visited.add(p.id);
      p = db.locations.find((r) => r.id === p!.parent);
    }
  }
  if (
    d.key === "items" &&
    existing &&
    input.unit !== existing.unit &&
    db.ledger.some((l) => l.item === existing.id)
  )
    throw Error("Base unit cannot change after a stock movement.");
  if (d.key === "roles" && existing?.system && input.name !== existing.name)
    throw Error("System role identity cannot be renamed.");
  if (d.key === "schedules" && !input.room && !input.asset && !input.area)
    throw Error("Select one asset, room or area for maintenance.");
  if (
    d.key === "schedules" &&
    input.assignee &&
    !db.users
      .find((u) => u.id === input.assignee)
      ?.roles.some((role: string) =>
        ["Maintenance Lead", "Maintenance Staff"].includes(role),
      )
  )
    throw Error("Default assignee must be a maintenance technician.");
  if (d.key === "replenishments") {
    for (const line of input.lines || []) {
      const item = db.items.find((i) => i.id === line.item)!;
      const stock = balance(db, input.warehouse, line.item);
      const shortage = db.materials.some(
        (m) =>
          ["Approved", "Partially Issued"].includes(m.status) &&
          m.lines.some(
            (l: any) =>
              l.item === line.item && l.approved - (l.issued || 0) > stock,
          ),
      );
      if (!(stock < Number(minimumStock(item, input.warehouse)) || shortage))
        throw Error(
          "Replenishment requires a low-stock item or approved material shortage.",
        );
      if (
        db.replenishments.some(
          (p) =>
            p.id !== existing?.id &&
            p.warehouse === input.warehouse &&
            !["Closed", "Fully Received"].includes(p.status) &&
            p.lines.some((l: any) => l.item === line.item),
        )
      )
        throw Error("An open replenishment already covers this shortage.");
    }
  }

  if (
    d.key === "shifts" &&
    input.assignees?.some(
      (id: string) =>
        !db.users.some(
          (u) =>
            u.id === id &&
            u.status === "Active" &&
            u.branch === input.branch &&
            u.department === input.department,
        ),
    )
  )
    throw Error(
      "Shift employees must be active in the selected branch and department.",
    );
  if (
    d.key === "tasks" &&
    input.recurrence === "Weekly" &&
    !input.weekdays?.length
  )
    throw Error("Choose at least one weekday for weekly recurrence.");
  if (
    d.key === "tasks" &&
    input.recurrence === "Monthly" &&
    (!(Number(input.monthDay) >= 1) || Number(input.monthDay) > 31)
  )
    throw Error("Choose a monthly day between 1 and 31.");
  if (d.key === "shifts") {
    if (input.end <= input.start) throw Error("Shift end must follow start.");
    for (const shift of db.shifts.filter(
      (s) => s.id !== existing?.id && s.date === input.date,
    )) {
      if (
        input.assignees?.some((u: string) => shift.assignees?.includes(u)) &&
        input.start < shift.end &&
        input.end > shift.start
      )
        throw Error("Employee has an overlapping shift assignment.");
    }
  }
  let r: RecordData;
  if (existing) {
    r = db[d.key].find((r) => r.id === existing.id)!;
    if (!r || r.version !== existing.version)
      throw Error("Record changed. Reload before saving.");

    if (d.key === "checklists" && r.status !== "Draft") {
      r.checklistVersion = (r.checklistVersion || 1) + 1;
      r.status = "Draft";
    }
    Object.assign(r, input, { version: r.version + 1 });
  } else {
    r = record(input.code || d.key.toUpperCase(), input.name, {
      ...input,
      status: d.initial,
      branch: input.branch || user.branch,
      department: input.department || user.department,
      creator: user.id,
    });
    db[d.key].unshift(r);
  }
  if (d.key === "users" && !existing) {
    r.password = `Dv!${crypto.randomUUID().slice(0, 12)}A9`;
    r.temporary = true;
    r.temporaryExpires = Date.now() + 24 * 3600000;
  }
  if (d.key === "items" && input.warehouse) {
    r.minimums = {
      ...(r.minimums || {}),
      [input.warehouse]: Number(input.minimum),
    };
  }
  if (d.key === "roles" && !existing) r.permissions = [];
  if (d.key === "tasks" && input.recurrence && input.recurrence !== "None") {
    r.seriesActive = true;
    r.seriesNext = nextTaskDue(r, r.due);
  }
  if (d.key === "rooms" && !existing) {
    r.active = true;
    r.passed = false;
  }
  if (d.key === "materials") {
    const approver = db.users.find(
      (u) =>
        u.status === "Active" &&
        u.roles.includes("Department Head") &&
        u.branch === r.branch &&
        u.department ===
          (user.roles.includes("Department Head")
            ? db.warehouses.find((w) => w.id === r.warehouse)?.department
            : user.department) &&
        u.id !== user.id,
    );
    if (!approver) throw Error("No eligible Department Head approver.");
    r.approver = approver.id;
    r.lines = r.lines.map((l: any) => ({
      ...l,
      approved: 0,
      issued: 0,
      returned: 0,
    }));
    r.status = input.submission === "Save draft" ? "Draft" : "Pending Approval";
  }
  if (d.key === "stocktakes" && !existing) {
    r.cutoff = now();
    r.lines = db.items.map((item) => ({
      item: item.id,
      system: balance(db, r.warehouse, item.id, r.cutoff),
      quantity: 0,
    }));
  }
  if (d.key === "inspections") {
    const c = db.cleaning.find((c) => c.id === r.cleaning);
    const cl = db.checklists.find((c) => c.id === r.checklist);
    if (!c || c.status !== "Pending Inspection" || cl?.status !== "Published")
      throw Error("Requires a pending cleaning cycle and published checklist.");

    if (
      db.inspections.some(
        (i) =>
          i.id !== r.id &&
          i.cleaning === r.cleaning &&
          !["Passed", "Failed"].includes(i.status),
      )
    )
      throw Error("An open inspection already exists.");
    r.criteria = cl.criteria;
    r.checklistVersion = cl.checklistVersion || 1;
  }
  if (d.key === "handovers") {
    r.sender = user.id;
    r.snapshot = [
      "tasks",
      "cleaning",
      "rework",
      "incidents",
      "requests",
      "rooms",
    ].flatMap((key) =>
      visible(key)
        .filter((r) => !closed.includes(r.status) && r.status !== "Ready")
        .map((r) => ({
          entity: key,
          id: r.id,
          name: r.name,
          status: r.status,
        })),
    );
  }
  if (d.key === "rework") {
    const insp = db.inspections.find((i) => i.id === r.inspection);
    if (
      !insp ||
      insp.status !== "Failed" ||
      db.rework.some(
        (w) =>
          w.id !== r.id &&
          w.inspection === insp.id &&
          !closed.includes(w.status),
      )
    )
      throw Error("Requires failed inspection without open rework.");
    r.cleaning = insp.cleaning;
    r.room = insp.room;
    r.defects =
      insp.results
        ?.filter((v: any) => v.result === "Fail")
        .map((v: any) => v.defect)
        .join("\n") || r.defects;
  }
  audit(db, d.key, existing ? "Update" : "Create", existing || null, r);
  notify(db, r.assignee || r.approver, r, d.key);
  calculateRooms(db);
  commit(db);
  return r;
}
function notify(
  db: Database,
  recipient: string | undefined,
  r: RecordData,
  key: string,
) {
  if (recipient && recipient !== currentUser()?.id)
    db.notifications.unshift(
      record("NTF", `${r.name} · ${r.status}`, {
        recipient,
        branch: r.branch,
        status: "Unread",
        priority: r.priority || "Normal",
        type: key,
        relatedKey: key,
        relatedId: r.id,
        description: `${currentUser()?.name} updated this record.`,
      }),
    );
}
function workStart(db: Database, r: RecordData, key: string, user: RecordData) {
  db.intervals.push(
    record("INT", r.name, {
      assignee: user.id,
      recordId: r.id,
      entity: key,
      start: now(),
      status: "Open",
      branch: r.branch,
      department: r.department,
    }),
  );
  r.actualStart ||= now();
  r.performedBy = user.id;
}
function workEnd(db: Database, r: RecordData) {
  for (const i of db.intervals.filter((i) => i.recordId === r.id && !i.end)) {
    i.end = now();
    i.status = "Closed";
  }
}
function dependencies(db: Database, key: string, r: RecordData): string[] {
  const refs: Record<string, string[]> = {
    branches: ["rooms", "warehouses", "tasks", "incidents", "requests"],
    locations: ["rooms", "locations", "assets", "tasks", "incidents"],
    roomTypes: ["rooms"],
    rooms: ["cleaning", "rework", "inspections", "incidents", "assets"],
    departments: ["users", "warehouses", "tasks", "requests", "assets"],
    users: ["cleaning", "rework", "tasks", "incidents", "requests"],
    assets: ["incidents", "schedules"],
    warehouses: ["transactions"],
  };
  return (refs[key] || []).flatMap((k) =>
    db[k]
      .filter(
        (v) =>
          v.id !== r.id &&
          !closed.includes(v.status) &&
          v.status !== "Inactive" &&
          (key === "branches"
            ? v.branch === r.name
            : key === "departments"
              ? v.department === r.name
              : key === "users"
                ? v.assignee === r.id
                : [
                    v.room,
                    v.floor,
                    v.parent,
                    v.roomType,
                    v.asset,
                    v.warehouse,
                    v.destination,
                  ].includes(r.id)),
      )
      .map((v) => v.name),
  );
}
function post(
  db: Database,
  r: RecordData,
  warehouse: string,
  item: string,
  delta: number,
) {
  if (balance(db, warehouse, item) + delta < 0)
    throw Error(
      "Insufficient stock: operation would create a negative balance.",
    );
  db.ledger.push(
    record("MOV", r.name, {
      warehouse,
      item,
      delta,
      document: r.id,
      branch: db.warehouses.find((w) => w.id === warehouse)!.branch,
    }),
  );
}
export function perform(
  d: Domain,
  loaded: RecordData,
  a: Action,
  input: any = {},
) {
  if (d.key === "maintenance")
    d = {
      ...d,
      key: database.incidents.some((i) => i.id === loaded.id)
        ? "incidents"
        : "occurrences",
    };
  requirePermission(a.uc);
  const user = currentUser()!;
  const db = structuredClone(database);
  const r = db[d.key].find((r) => r.id === loaded.id);
  if (!r || r.version !== loaded.version)
    throw Error("Record changed. Reload before retrying.");

  if (a.from && !a.from.includes(r.status))
    throw Error("Action is no longer available in the current status.");
  input = cleanInput(input);
  if (input.lines && d.key !== "stocktakes")
    input.lines = materialLines(db, input.lines, r.lines);
  if (a.reason && String(input.reason || "").length < 10)
    throw Error("A reason of at least 10 characters is required.");
  for (const f of a.fields || [])
    if (
      f.required &&
      (input[f.key] === undefined ||
        input[f.key] === "" ||
        (Array.isArray(input[f.key]) && !input[f.key].length))
    )
      throw Error(`${f.label} is required.`);

  validateReferences(db, input);

  if (
    input.progress !== undefined &&
    (Number(input.progress) < 0 || Number(input.progress) > 100)
  )
    throw Error("Progress must be between 0 and 100.");
  const before = structuredClone(r);
  if (a.label === "Assign scheduled work") {
    const u = db.users.find((u) => u.id === input.assignee);
    if (
      !u ||
      u.branch !== r.branch ||
      !u.roles.some((role: string) =>
        ["Maintenance Lead", "Maintenance Staff"].includes(role),
      )
    )
      throw Error("Select a maintenance technician in the same branch.");
  }
  if (a.label === "Stop future repetitions") {
    if (!r.seriesActive)
      throw Error("This task has no active recurring series.");
    r.seriesActive = false;
  }
  if (
    a.label === "Accept and start" ||
    (a.label === "Accept" && d.key === "requests")
  )
    r.acceptedAt = now();
  if (
    a.to === "Pending Verification" ||
    a.to === "Pending Inspection" ||
    a.to === "Pending Reinspection"
  )
    r.actualEnd = now();
  if (a.label === "Reopen") {
    r.cycles = [
      ...(r.cycles || []),
      {
        status: r.status,
        performedBy: r.performedBy,
        submittedBy: r.submittedBy,
        verifiedBy: r.verifiedBy,
        actualStart: r.actualStart,
        actualEnd: r.actualEnd,
        closedAt: r.closedAt,
        results: r.results,
      },
    ];
    r.actualStart = undefined;
    r.actualEnd = undefined;
    r.closedAt = undefined;
    r.performedBy = undefined;
    r.submittedBy = undefined;
    r.verifiedBy = undefined;
  }
  if (
    input.due &&
    input.responseDue &&
    new Date(input.due) < new Date(input.responseDue)
  )
    throw Error("Resolution deadline must follow response deadline.");
  if (["Deactivate", "Retire"].includes(a.label)) {
    if (d.key === "roles" && r.system)
      throw Error("System roles cannot be deactivated.");
    const deps = dependencies(db, d.key, r);
    if (deps.length)
      throw Error("Resolve dependencies first: " + deps.join(", "));
    if (
      d.key === "warehouses" &&
      db.items.some((i) => balance(db, r.id, i.id) !== 0)
    )
      throw Error("Warehouse still has stock.");
    if (
      d.key === "rooms" &&
      db.blocks.some((b) => b.room === r.id && b.status === "Effective")
    )
      throw Error("Room has an effective block.");
    if (
      d.key === "users" &&
      r.roles.includes("Administrator") &&
      db.users.filter(
        (u) => u.status === "Active" && u.roles.includes("Administrator"),
      ).length <= 1
    )
      throw Error("Cannot remove last Administrator.");
  }
  if (a.label === "Assign roles" && d.key === "users") {
    if (
      dependencies(db, "users", r).length &&
      r.roles.some((role: string) => !input.roles.includes(role))
    )
      throw Error("Reassign open work before removing roles.");
  }

  if (a.label === "Reset password") {
    r.password = `Dv!${crypto.randomUUID().slice(0, 12)}A9`;
    r.temporary = true;
    r.temporaryExpires = Date.now() + 24 * 3600000;
  }
  if (
    a.label === "Assign scope" &&
    dependencies(db, "users", r).length &&
    (input.branch !== r.branch || input.department !== r.department)
  )
    throw Error("Reassign open work before moving scope.");
  if (
    a.to === "In Progress" &&
    ["cleaning", "rework", "incidents", "tasks", "occurrences"].includes(
      d.key,
    ) &&
    [
      "Start / resume",
      "Start",
      "Resume",
      "Accept and start",
      "Start scheduled maintenance",
    ].includes(a.label)
  ) {
    workStart(db, r, d.key, user);
    r.blockerOpen = false;
  }
  if (a.label === "Complete scheduled maintenance") {
    if (!input.results?.length || input.results.some((v: any) => !v.result))
      throw Error("Every mandatory check item requires a result.");
    workEnd(db, r);
    r.actualEnd = now();
  }
  if (a.label === "Report discovered problem") {
    const incident = record("INC", input.name, {
      branch: r.branch,
      department: "Maintenance",
      room: r.room,
      asset: r.asset,
      area: r.area,
      description: input.description,
      occurrence: r.id,
      status: "Pending Triage",
      creator: user.id,
      detectedAt: now(),
    });
    db.incidents.push(incident);
    audit(db, "incidents", "Report discovered problem", null, incident);
    delete input.name;
    delete input.description;
  }
  if (a.label === "Assign") workEnd(db, r);
  if (
    [
      "On Hold",
      "Paused",
      "Pending Inspection",
      "Pending Reinspection",
      "Pending Verification",
    ].includes(a.to || "")
  )
    workEnd(db, r);
  if (a.label === "Report blocker") r.blockerOpen = true;
  if (
    a.label === "Submit for inspection" &&
    (r.blockerOpen ||
      r.criteria
        ?.split("\n")
        .some((c: string) => !(r.completedItems || []).includes(c)))
  )
    throw Error(
      "Complete every mandatory cleaning item and resolve blockers first.",
    );
  if (
    a.label === "Submit result" &&
    r.criteria
      ?.split("\n")
      .some((c: string) => !(r.completedItems || []).includes(c))
  )
    throw Error("Every mandatory completion criterion requires a result.");
  if (
    a.label === "Submit rework" &&
    input.remediation.split("\n").filter(Boolean).length <
      (r.defects || "").split("\n").filter(Boolean).length
  )
    throw Error("Every defect requires a remediation note.");
  if (d.key === "inspections") {
    const c = db.cleaning.find((c) => c.id === r.cleaning);

    if (a.label === "Record results" || a.label === "Finish inspection") {
      const results = input.results || r.results;
      if (
        !results?.length ||
        results.some(
          (v: any) => !v.result || (v.result === "Fail" && !v.defect?.trim()),
        )
      )
        throw Error(
          "Every criterion requires Pass/Fail and every Fail needs a defect description.",
        );
    }
    if (a.label === "Approve") {
      if (!r.results?.length || r.results.some((v: any) => v.result !== "Pass"))
        throw Error("All mandatory criteria must pass.");
      if (c) {
        c.status = "Completed";
        c.version++;
      }
      r.concludedAt = now();
    }
    if (a.label === "Reject") {
      if (!r.results?.some((v: any) => v.result === "Fail" && v.defect))
        throw Error("Record defects before rejecting.");
      if (c) {
        c.status = "Rework Required";
        c.version++;
      }
    }
    if (a.label === "Conclude reinspection") {
      const w = db.rework.find((w) => w.id === r.rework);
      if (!w || w.status !== "Pending Reinspection")
        throw Error("Rework must be submitted for reinspection.");
      if (!input.results?.length || input.results.some((v: any) => !v.result))
        throw Error("Check every defect.");
      const passed = input.results.every((v: any) => v.result === "Pass");
      r.status = passed ? "Passed" : "Failed";
      w.status = passed ? "Completed" : "Failed";
      w.version++;
      if (c) {
        c.status = passed ? "Completed" : "Rework Required";
        c.version++;
      }
    }
  }
  if (a.label === "Triage" && (!r.category || !r.severity))
    throw Error("Set category and severity before triage.");
  if (a.label === "Block room") {
    if (
      db.blocks.some(
        (b) =>
          b.incident === r.id &&
          b.room === input.room &&
          b.status === "Effective",
      )
    )
      throw Error("This incident already blocks the room.");
    db.blocks.push(
      record("BLK", input.reason, {
        incident: r.id,
        room: input.room,
        status: "Effective",
        branch: r.branch,
      }),
    );
  }
  if (a.label === "Remove block")
    for (const b of db.blocks.filter(
      (b) => b.incident === r.id && b.status === "Effective",
    ))
      b.status = "Removed";
  if (a.label === "Escalate") {
    const elapsed =
      Date.now() - new Date(r.detectedAt || r.createdAt).getTime();
    const allowed =
      new Date(r.due).getTime() -
      new Date(r.detectedAt || r.createdAt).getTime();
    if (r.severity !== "Critical" && (!r.due || elapsed < allowed * 0.8))
      throw Error(
        "Only critical, at-risk or overdue records may be escalated.",
      );
    if (r.escalated) throw Error("An open escalation already exists.");
    r.escalated = true;
  }

  if (a.label === "Verify and close") {
    for (const b of db.blocks.filter((b) => b.incident === r.id))
      b.status = "Removed";
    r.escalated = false;
  }
  if (
    a.label === "Reopen" &&
    ["incidents", "requests"].includes(d.key) &&
    Date.now() - new Date(r.closedAt).getTime() >
      (d.key === "incidents" ? 30 : 7) * 86400000
  )
    throw Error("The reopen window has expired.");
  if (a.label === "Link existing work") {
    const [key, targetId] = input.linkedWork.split(":");
    const target = db[key]?.find((v) => v.id === targetId);
    if (
      !target ||
      target.branch !== r.branch ||
      closed.includes(target.status) ||
      !visible(key).some((v) => v.id === targetId)
    )
      throw Error(
        "Select an open work record in the same branch and your scope.",
      );
    if (r.linkedRecords?.some((l: any) => l.key === key && l.id === targetId))
      throw Error("Work is already linked.");
    r.linkedRecords = [
      ...(r.linkedRecords || []),
      { key, id: targetId, mandatory: input.mandatory === "Yes" },
    ];
  }
  if (a.label === "Hand over") {
    r.handover = {
      receiver: input.receiver,
      department: input.receivingDepartment,
      status: "Pending Acknowledgment",
      sender: user.id,
      note: input.note,
    };
  }
  if (a.label === "Retire" && r.handover?.status === "Pending Acknowledgment")
    throw Error("A pending handover prevents retirement.");
  if (a.label === "Add conversion") {
    if (
      !(Number(input.factor) > 0) ||
      input.unit === r.unit ||
      r.conversions?.some((c: any) => c.unit === input.unit)
    )
      throw Error("Use a unique additional unit with positive factor.");
    r.conversions = [
      ...(r.conversions || []),
      { unit: input.unit, factor: Number(input.factor) },
    ];
    delete input.unit;
    delete input.factor;
  }
  if (a.label === "Stock minimum") {
    if (Number(input.minimum) < 0)
      throw Error("Minimum stock must be non-negative.");
    r.minimums = {
      ...(r.minimums || {}),
      [input.warehouse]: Number(input.minimum),
    };
    delete input.warehouse;
    delete input.minimum;
  }
  if (a.label === "Acknowledge handover") {
    r.handover.status = "Acknowledged";
    if (d.key === "assets") r.department = r.handover.department;
    else r.assignee = user.id;
  }

  if (a.label === "Complete" && d.key === "requests") {
    if (
      r.handover?.status === "Pending Acknowledgment" ||
      r.linkedRecords?.some(
        (l: any) =>
          l.mandatory &&
          !closed.includes(
            db[l.key].find((v) => v.id === l.id)?.status || "Missing",
          ),
      )
    )
      throw Error(
        "Complete mandatory linked work and pending handovers first.",
      );
  }
  if (d.key === "transactions") {
    if (a.label === "Post receipt" || a.label === "Post return") {
      if (r.type !== (a.label === "Post receipt" ? "Receipt" : "Return"))
        throw Error("Wrong document type.");
      const ls = input.lines;
      if (!ls?.length || ls.some((l: any) => !(Number(l.quantity) > 0)))
        throw Error("At least one positive received quantity is required.");
      if (a.label === "Post return") {
        const issue = db.transactions.find(
          (t) => t.id === r.issue && t.type === "Issue",
        );
        if (!issue) throw Error("Return must reference a posted issue.");
        for (const l of ls) {
          const issued = issue.lines.find((x: any) => x.item === l.item);
          const prior = db.transactions
            .filter(
              (t) =>
                t.type === "Return" &&
                t.issue === issue.id &&
                t.status === "Posted",
            )
            .flatMap((t) => t.lines)
            .filter((x: any) => x.item === l.item)
            .reduce((a: number, x: any) => a + Number(x.quantity), 0);
          if (!issued || prior + Number(l.quantity) > Number(issued.quantity))
            throw Error("Return exceeds issued quantity.");
        }
      }
      for (const l of ls) {
        const expected = r.lines.find((x: any) => x.item === l.item);
        if (!expected) throw Error("Cannot add items while posting.");
        if (
          Number(expected.quantity) !== Number(l.quantity) &&
          String(input.reason || "").length < 10
        )
          throw Error("Quantity discrepancy requires a 10-character reason.");
        post(db, r, r.warehouse, l.item, Number(l.quantity) * (l.factor || 1));
      }
      r.lines = ls;
      const replen = db.replenishments.find((p) => p.id === r.replenishment);
      if (replen) {
        for (const l of ls) {
          const pl = replen.lines.find((x: any) => x.item === l.item);
          if (pl) pl.received = (pl.received || 0) + Number(l.quantity);
        }
        replen.status = replen.lines.every(
          (l: any) => (l.received || 0) >= Number(l.quantity),
        )
          ? "Fully Received"
          : "Partially Received";
        replen.version++;
      }
    }
    if (a.label === "Dispatch transfer") {
      if (r.type !== "Transfer" || r.destination === r.warehouse)
        throw Error("Source and destination warehouses must differ.");
      for (const l of r.lines)
        post(db, r, r.warehouse, l.item, -Number(l.quantity) * (l.factor || 1));
    }
    if (a.label === "Confirm receipt") {
      for (const l of input.lines) {
        const original = r.lines.find((x: any) => x.item === l.item);
        const received = Number(l.quantity),
          damaged = Number(l.damaged || 0),
          missing = Number(l.missing || 0),
          total = received + damaged + missing;
        if (
          !original ||
          received < 0 ||
          damaged < 0 ||
          missing < 0 ||
          total <= 0 ||
          total + (original.confirmed || 0) > Number(original.quantity)
        )
          throw Error("Receipt/loss quantities exceed dispatched remainder.");
        if ((damaged || missing) && String(input.reason || "").length < 10)
          throw Error("Transfer losses require a 10-character reason.");
        if (received)
          post(db, r, r.destination, l.item, received * (original.factor || 1));
        original.received = (original.received || 0) + received;
        original.damaged = (original.damaged || 0) + damaged;
        original.missing = (original.missing || 0) + missing;
        original.confirmed = (original.confirmed || 0) + total;
      }
      r.status = r.lines.every((l: any) => l.confirmed >= Number(l.quantity))
        ? "Received"
        : "Partially Received";
      delete input.lines;
    }
    if (a.label === "Reverse") {
      if (
        db.transactions.some((t) => t.issue === r.id && t.status === "Posted")
      )
        throw Error("Dependent return documents prevent reversal.");
      const reversal = record("REV", `Reversal · ${r.name}`, {
        type: "Reversal",
        status: "Posted",
        branch: r.branch,
        original: r.id,
        lines: r.lines,
      });
      db.transactions.push(reversal);
      for (const l of db.ledger.filter((l) => l.document === r.id))
        post(db, reversal, l.warehouse, l.item, -Number(l.delta));
      if (r.materialRequest) {
        const request = db.materials.find((m) => m.id === r.materialRequest)!;
        for (const l of r.lines) {
          const ml = request.lines.find((v: any) => v.item === l.item);
          ml.issued -= Number(l.quantity);
        }
        request.status = request.lines.some((l: any) => l.issued)
          ? "Partially Issued"
          : "Approved";
        request.version++;
      }
    }
  }
  if (d.key === "stocktakes") {
    if (a.label === "Record counts") {
      if (input.lines.some((l: any) => Number(l.quantity) < 0))
        throw Error("Counted quantities must be non-negative.");
      r.lines = r.lines.map((l: any) => ({
        ...l,
        quantity: Number(
          input.lines.find((v: any) => v.item === l.item)?.quantity ??
            l.quantity,
        ),
      }));
      delete input.lines;
    }
    if (a.label === "Reconcile") {
      r.lines = r.lines.map((l: any) => ({
        ...l,
        discrepancy: Number(l.quantity) - Number(l.system),
        cause: input.note,
      }));
      if (r.lines.every((l: any) => !l.discrepancy)) r.status = "Reconciled";
    }
    if (a.label === "Approve adjustment") {
      for (const l of r.lines) post(db, r, r.warehouse, l.item, l.discrepancy);
    }
  }
  if (d.key === "materials") {
    if (a.label === "Approve") {
      let total = 0;
      for (const l of r.lines) {
        const q = Number(
          input.lines.find((x: any) => x.item === l.item)?.quantity || 0,
        );
        if (q < 0 || q > Number(l.quantity))
          throw Error(
            "Approved quantity must be between zero and requested quantity.",
          );
        l.approved = q;
        total += q;
      }
      if (!total)
        throw Error(
          "Approve at least one positive quantity; otherwise Reject with reason.",
        );
      delete input.lines;
    }
    if (["Issue remaining", "Partial issue"].includes(a.label)) {
      const issue = record("ISS", `Issue · ${r.name}`, {
        type: "Issue",
        warehouse: input.warehouse,
        branch: r.branch,
        status: "Posted",
        materialRequest: r.id,
        lines: [],
      });
      for (const l of r.lines) {
        const remaining = l.approved - (l.issued || 0);
        const quantity =
          a.label === "Issue remaining"
            ? remaining
            : Number(
                input.lines.find((v: any) => v.item === l.item)?.quantity || 0,
              );
        if (quantity < 0 || quantity > remaining)
          throw Error("Issue quantity exceeds approved remainder.");
        if (quantity) {
          post(db, issue, input.warehouse, l.item, -quantity * (l.factor || 1));
          l.issued = (l.issued || 0) + quantity;
          issue.lines.push({
            item: l.item,
            quantity,
            unit: l.unit,
            factor: l.factor || 1,
          });
        }
      }
      if (!issue.lines.length) throw Error("Enter a positive issue quantity.");
      db.transactions.push(issue);
      r.status = r.lines.every((l: any) => l.issued >= l.approved)
        ? "Fully Issued"
        : "Partially Issued";
      delete input.lines;
    }
    if (a.label === "Return unused materials") {
      const issue = db.transactions.find(
        (t) =>
          t.materialRequest === r.id &&
          t.type === "Issue" &&
          t.status === "Posted",
      );
      if (!issue) throw Error("No posted issue to return.");
      for (const l of input.lines) {
        const original = issue.lines.find((v: any) => v.item === l.item);
        if (
          !original ||
          !(Number(l.quantity) > 0) ||
          Number(l.quantity) > Number(original.quantity)
        )
          throw Error("Return exceeds original issue.");
      }
      db.transactions.push(
        record("RET", `Return · ${r.name}`, {
          branch: r.branch,
          type: "Return",
          issue: issue.id,
          warehouse: issue.warehouse,
          status: "Draft",
          lines: input.lines,
        }),
      );
      delete input.lines;
    }
  }
  if (d.key === "notifications") {
    r.readAt ||= now();
    if (a.to === "Acknowledged") r.acknowledgedAt ||= now();
  }
  if (d.key === "rooms") {
    if (a.to === "Inactive") r.active = false;
    if (a.label === "Reactivate") {
      r.active = true;
      r.passed = false;
    }
  }
  Object.assign(r, input);
  if (
    a.to &&
    !(
      d.key === "stocktakes" &&
      a.label === "Reconcile" &&
      r.status === "Reconciled"
    )
  )
    r.status = a.to;
  r.version++;
  if (a.to === "Closed") r.closedAt = now();
  if (a.to === "Pending Verification") r.submittedBy = user.id;
  if (a.to === "Closed" || a.to === "Completed") r.verifiedBy = user.id;
  audit(db, d.key, a.label, before, r);
  notify(db, r.assignee || r.approver, r, d.key);
  calculateRooms(db);
  commit(db);
  return r;
}
export function assign(
  d: Domain,
  loaded: RecordData,
  assignee: string,
  reason: string,
) {
  const uc =
    d.key === "cleaning"
      ? loaded.assignee
        ? "UC-030"
        : "UC-029"
      : d.key === "incidents"
        ? "UC-053"
        : "UC-104";
  const eligible =
    d.key === "cleaning"
      ? ["Housekeeping Staff"]
      : d.key === "incidents"
        ? ["Maintenance Staff", "Maintenance Lead"]
        : ["Housekeeping Staff", "Maintenance Staff", "Storekeeper"];
  const user = database.users.find((u) => u.id === assignee);
  if (
    !user ||
    !user.roles.some((r: string) => eligible.includes(r)) ||
    user.branch !== loaded.branch ||
    user.status !== "Active" ||
    (d.key !== "incidents" && user.department !== loaded.department)
  )
    throw Error("Employee is not eligible for this assignment.");
  if (
    d.key === "incidents" &&
    user.roles.includes("Maintenance Lead") &&
    !database.users.some(
      (u) =>
        u.id !== user.id &&
        u.branch === user.branch &&
        u.status === "Active" &&
        u.roles.includes("Maintenance Lead"),
    )
  )
    throw Error("Another active Maintenance Lead is required to verify.");
  const allowed =
    d.key === "cleaning"
      ? ["Pending Assignment", "Assigned", "In Progress", "On Hold"]
      : d.key === "incidents"
        ? ["Pending Assignment", "Assigned"]
        : [
            "Pending Assignment",
            "Assigned",
            "Accepted",
            "In Progress",
            "Paused",
          ];
  if (assignee === loaded.assignee) throw Error("Choose a different employee.");
  const result = perform(
    d,
    loaded,
    {
      label: "Assign",
      uc,
      from: allowed,
      to: "Assigned",
      reason: !!loaded.assignee,
    },
    { assignee, reason },
  );
  return result;
}
export function updateCriteria(d: Domain, r: RecordData, items: string[]) {
  return perform(
    d,
    r,
    {
      label: "Update criteria",
      uc: d.key === "cleaning" ? "UC-032" : "UC-109",
      from: ["In Progress"],
      own: true,
    },
    { completedItems: items },
  );
}
export function workday(start: boolean, reason: string) {
  requirePermission("UC-139");
  const user = currentUser()!;
  const db = structuredClone(database);
  const active = db.workdays.find(
    (w) => w.assignee === user.id && w.status === "Working",
  );
  if (start) {
    if (active) throw Error("Workday already started.");
    const date = new Date().toLocaleDateString("en-CA", {
      timeZone: "Asia/Ho_Chi_Minh",
    });
    if (
      !db.shifts.some(
        (s) => s.date === date && s.assignees?.includes(user.id),
      ) &&
      reason.trim().length < 10
    )
      throw Error(
        "Starting without a shift assignment requires a 10-character reason.",
      );
    const r = record("WD", "Workday", {
      assignee: user.id,
      branch: user.branch,
      department: user.department,
      status: "Working",
      start: now(),
      reason,
    });
    db.workdays.push(r);
    audit(db, "workdays", "Start workday", null, r);
  } else {
    if (!active) throw Error("No active workday.");
    if (db.intervals.some((i) => i.assignee === user.id && !i.end))
      throw Error("Pause or complete open work before ending your workday.");
    const before = structuredClone(active);
    active.status = "Ended";
    active.end = now();
    audit(db, "workdays", "End workday", before, active);
  }
  commit(db);
}
export function requestCorrection(
  interval: RecordData,
  start: string,
  end: string,
  reason: string,
) {
  requirePermission("UC-111");
  const user = currentUser()!;
  if (
    interval.assignee !== user.id ||
    reason.trim().length < 10 ||
    !start ||
    !end ||
    new Date(end) < new Date(start)
  )
    throw Error(
      "Select your own interval, valid start/end and a 10-character reason.",
    );
  const db = structuredClone(database);
  const r = record("COR", "Time correction", {
    assignee: user.id,
    branch: user.branch,
    department: user.department,
    interval: interval.id,
    start,
    end,
    reason,
    status: "Pending",
  });
  db.corrections.push(r);
  audit(db, "corrections", "Request correction", null, r);
  commit(db);
}
export function applyCorrection(c: RecordData, accept: boolean) {
  requirePermission("UC-111");
  const user = currentUser()!;

  const db = structuredClone(database);
  const correction = db.corrections.find((v) => v.id === c.id);
  if (!correction || correction.status !== "Pending")
    throw Error("Correction already processed.");
  const interval = db.intervals.find((v) => v.id === c.interval)!;
  if (
    accept &&
    db.intervals.some(
      (v) =>
        v.id !== interval.id &&
        v.assignee === interval.assignee &&
        v.start < c.end &&
        (!v.end || v.end > c.start),
    )
  )
    throw Error("Correction creates overlapping intervals.");
  const before = structuredClone(interval);
  if (accept) {
    interval.start = c.start;
    interval.end = c.end;
  }
  correction.status = accept ? "Applied" : "Rejected";
  audit(db, "intervals", correction.status, before, interval);
  commit(db);
}
export function availableActions(d: Domain, r: RecordData) {
  const key =
    d.key === "maintenance"
      ? database.incidents.some((i) => i.id === r.id)
        ? "incidents"
        : "maintenance"
      : d.key;
  return (actions[key] || []).filter(
    (a) =>
      can(a.uc) &&
      (a.label !== "Stop future repetitions" || r.seriesActive) &&
      (!a.from || a.from.includes(r.status)) &&
      (key !== "inspections" ||
        (r.rework
          ? a.label === "Conclude reinspection"
          : a.label !== "Conclude reinspection")) &&
      (key !== "transactions" ||
        (a.label === "Post receipt"
          ? r.type === "Receipt"
          : a.label === "Post return"
            ? r.type === "Return"
            : a.label.includes("transfer") || a.label === "Confirm receipt"
              ? r.type === "Transfer"
              : true)),
  );
}
export function createReinspection(rework: RecordData, inspector: string) {
  requirePermission("UC-047");
  const db = structuredClone(database);
  const w = db.rework.find((r) => r.id === rework.id)!;
  if (w.status !== "Pending Reinspection")
    throw Error("Rework must be submitted first.");

  if (
    db.inspections.some(
      (i) => i.rework === w.id && !["Passed", "Failed"].includes(i.status),
    )
  )
    throw Error("Open reinspection already exists.");
  const original = db.inspections.find((i) => i.id === w.inspection)!;
  const r = record("REINS", `Reinspection · ${w.name}`, {
    branch: w.branch,
    department: w.department,
    status: "Assigned",
    assignee: inspector,
    rework: w.id,
    cleaning: w.cleaning,
    room: w.room,
    criteria: w.defects,
    checklistVersion: original.checklistVersion,
    originalInspection: original.id,
  });
  db.inspections.push(r);
  audit(db, "inspections", "Create reinspection", null, r);
  commit(db);
  return r;
}
export function linkWork(requestId: string, key: string, recordId: string) {
  const db = structuredClone(database);
  const r = db.requests.find((r) => r.id === requestId);
  const target = db[key]?.find((v) => v.id === recordId);
  if (!r || !target) throw Error("Select an existing request and work record.");

  const before = structuredClone(r);
  r.linkedRecords = [
    ...(r.linkedRecords || []),
    { key, id: recordId, mandatory: true },
  ];
  r.version++;
  audit(db, "requests", "Link work", before, r);
  commit(db);
}
export function recordExport(
  type: string,
  format: string,
  snapshotTime: string,
  from: string,
  to: string,
  count: number,
) {
  requirePermission("UC-135");
  const db = structuredClone(database);
  audit(
    db,
    "reports",
    "Export " + format,
    null,
    record("RPT", type, { snapshot: snapshotTime, from, to, count }),
  );
  commit(db);
}
export function branchContext() {
  return sessionStorage.getItem("hotelops-branch") || "All branches";
}
export function setBranchContext(branch: string) {
  sessionStorage.setItem("hotelops-branch", branch);
  commit({ ...database });
}
export function generateDueMaintenance() {
  requirePermission("UC-061");
  const db = structuredClone(database);
  let generated = 0;
  for (const schedule of db.schedules.filter(
    (s) => s.status === "Active" && inScope(s),
  )) {
    schedule.nextDue ||= schedule.due;
    if (new Date(schedule.nextDue).getTime() > Date.now() + 3 * 86400000)
      continue;
    if (
      db.occurrences.some(
        (o) =>
          o.schedule === schedule.id && o.scheduledDue === schedule.nextDue,
      )
    )
      continue;
    const occurrence = record("PM", schedule.name, {
      branch: schedule.branch,
      department: "Maintenance",
      room: schedule.room,
      asset: schedule.asset,
      area: schedule.area,
      schedule: schedule.id,
      scheduledDue: schedule.nextDue,
      due: schedule.nextDue,
      criteria: schedule.criteria,
      assignee: schedule.assignee,
      status: schedule.assignee ? "Assigned" : "Pending Assignment",
    });
    db.occurrences.push(occurrence);
    audit(
      db,
      "occurrences",
      "Generate scheduled maintenance",
      null,
      occurrence,
    );
    const next = new Date(schedule.nextDue);
    const interval = Number(schedule.interval);
    if (schedule.intervalUnit === "Months")
      next.setUTCMonth(next.getUTCMonth() + interval);
    else
      next.setUTCDate(
        next.getUTCDate() +
          interval * (schedule.intervalUnit === "Weeks" ? 7 : 1),
      );
    schedule.nextDue = next.toISOString();
    schedule.version++;
    generated++;
  }
  commit(db);
  return generated;
}
export function scanAlerts() {
  const db = structuredClone(database);
  db.alertEpisodes ||= [];
  const violations: {
    key: string;
    entity: string;
    record: RecordData;
    roles: string[];
    type: string;
  }[] = [];
  for (const key of [
    "cleaning",
    "rework",
    "incidents",
    "tasks",
    "requests",
    "occurrences",
  ])
    for (const r of db[key] || [])
      if (
        r.due &&
        Date.now() > new Date(r.due).getTime() &&
        !closed.includes(r.status)
      )
        violations.push({
          key: key + ":" + r.id,
          entity: key,
          record: r,
          roles: ["cleaning", "rework"].includes(key)
            ? ["Housekeeping Supervisor"]
            : ["incidents", "occurrences"].includes(key)
              ? ["Maintenance Lead"]
              : ["Department Head"],
          type: "Overdue",
        });
  for (const w of db.warehouses.filter((w) => w.status === "Active"))
    for (const item of db.items) {
      const minimum = minimumStock(item, w.id);
      if (minimum !== undefined && balance(db, w.id, item.id) < minimum)
        violations.push({
          key: "stock:" + w.id + ":" + item.id,
          entity: "items",
          record: { ...item, branch: w.branch, warehouse: w.id },
          roles: ["Storekeeper", "Operations Manager"],
          type: "Low stock",
        });
    }
  let changed = false;
  for (const episode of db.alertEpisodes)
    if (
      episode.status === "Open" &&
      !violations.some((v) => v.key === episode.key)
    ) {
      episode.status = "Ended";
      changed = true;
    }
  for (const v of violations) {
    if (db.alertEpisodes.some((e) => e.key === v.key && e.status === "Open"))
      continue;
    db.alertEpisodes.push(
      record("EPISODE", v.record.name, {
        key: v.key,
        status: "Open",
        branch: v.record.branch,
      }),
    );
    for (const u of db.users.filter(
      (u) =>
        u.status === "Active" &&
        (u.branch === v.record.branch ||
          u.branches?.includes(v.record.branch)) &&
        u.roles.some((r: string) => v.roles.includes(r)) &&
        (v.type === "Low stock" ||
          u.department === v.record.department ||
          u.roles.includes("Operations Manager")),
    ))
      db.notifications.unshift(
        record("ALERT", v.type + " · " + v.record.name, {
          recipient: u.id,
          branch: v.record.branch,
          type: v.type,
          priority: "High",
          status: "Unread",
          relatedKey: v.entity === "occurrences" ? "maintenance" : v.entity,
          relatedId: v.record.id,
        }),
      );
    changed = true;
  }
  if (changed) commit(db);
}
function nextTaskDue(template: RecordData, due: string) {
  const d = new Date(due);
  if (template.recurrence === "Daily") d.setUTCDate(d.getUTCDate() + 1);
  else if (template.recurrence === "Weekly") {
    const days = [
      "Sunday",
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday",
    ];
    do {
      d.setUTCDate(d.getUTCDate() + 1);
    } while (
      !(template.weekdays || []).includes(
        days[new Date(d.getTime() + 7 * 3600000).getUTCDay()],
      )
    );
  } else {
    d.setUTCDate(1);
    d.setUTCMonth(d.getUTCMonth() + 1);
    const end = new Date(
      Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 0),
    ).getUTCDate();
    d.setUTCDate(Math.min(Number(template.monthDay || 1), end));
  }
  return d.toISOString();
}
export function generateRecurringTasks() {
  requirePermission("UC-102");
  const db = structuredClone(database);
  let count = 0;
  for (const template of db.tasks.filter(
    (t) =>
      t.seriesActive &&
      inScope(t) &&
      t.department === currentUser()?.department,
  )) {
    if (new Date(template.seriesNext).getTime() > Date.now() + 86400000)
      continue;
    if (
      template.seriesEnd &&
      template.seriesNext.slice(0, 10) > template.seriesEnd
    ) {
      template.seriesActive = false;
      continue;
    }
    if (
      !db.tasks.some(
        (t) => t.series === template.id && t.due === template.seriesNext,
      )
    ) {
      const r = record("REC", template.name, {
        branch: template.branch,
        department: template.department,
        category: template.category,
        room: template.room,
        description: template.description,
        criteria: template.criteria,
        priority: template.priority,
        assignee: template.assignee,
        creator: template.creator,
        series: template.id,
        due: template.seriesNext,
        status: template.assignee ? "Assigned" : "Pending Assignment",
      });
      db.tasks.push(r);
      audit(db, "tasks", "Generate recurring task", null, r);
      notify(db, r.assignee, r, "tasks");
      count++;
    }
    template.seriesNext = nextTaskDue(template, template.seriesNext);
    template.version++;
  }
  commit(db);
  return count;
}
