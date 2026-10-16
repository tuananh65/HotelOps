import { test, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/services.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/workflows.mjs",
});
class Storage {
  map = new Map();
  getItem(k) {
    return this.map.get(k) || null;
  }
  setItem(k, v) {
    this.map.set(k, String(v));
  }
  removeItem(k) {
    this.map.delete(k);
  }
}
let s;
beforeEach(async () => {
  globalThis.localStorage = new Storage();
  globalThis.sessionStorage = new Storage();
  s = await import("../.test-output/workflows.mjs?" + Math.random());
});
const login = (user) => s.login(user, "HotelOps@2026");
test("inspection approves cleaning and makes the room ready", () => {
  login("hksupervisor");
  let r = s.visible("inspections")[0];
  const d = { key: "inspections" };
  r = s.perform(d, r, {
    label: "Start inspection",
    uc: "UC-042",
    from: ["Assigned"],
    to: "Inspecting",
    own: true,
  });
  const results = r.criteria
    .split("\n")
    .map((text) => ({ text, result: "Pass", defect: "" }));
  r = s.perform(
    d,
    r,
    { label: "Record results", uc: "UC-042", from: ["Inspecting"], own: true },
    { results },
  );
  r = s.perform(d, r, {
    label: "Finish inspection",
    uc: "UC-042",
    from: ["Inspecting"],
    to: "Pending Conclusion",
    own: true,
  });
  r = s.perform(
    d,
    r,
    {
      label: "Approve",
      uc: "UC-044",
      from: ["Pending Conclusion"],
      to: "Passed",
      own: true,
    },
    { note: "Every mandatory criterion passed" },
  );
  assert.equal(
    s.snapshot().rooms.find((room) => room.id === r.room).status,
    "Ready",
  );
  assert.equal(
    s.snapshot().cleaning.find((c) => c.id === r.cleaning).status,
    "Completed",
  );
});
test("room block takes precedence over ready and a notification acknowledgement leaves source untouched", () => {
  login("maintenancelead");
  const incident = s.visible("incidents")[0];
  const room = s.snapshot().rooms.find((r) => r.status === "Ready");
  s.perform(
    { key: "incidents" },
    incident,
    { label: "Block room", uc: "UC-056", reason: true },
    { room: room.id, reason: "Safety inspection required" },
  );
  assert.equal(
    s.snapshot().rooms.find((r) => r.id === room.id).status,
    "Blocked",
  );
  const original = JSON.stringify(s.snapshot().incidents);
  const n = s.visible("notifications")[0];
  s.perform({ key: "notifications" }, n, {
    label: "Acknowledge",
    uc: "UC-130",
    to: "Acknowledged",
  });
  assert.equal(JSON.stringify(s.snapshot().incidents), original);
});
test("receipt posting changes stock once, repeated submission rejected", () => {
  login("storekeeper");
  const db = s.snapshot();
  const d = {
    key: "transactions",
    createUC: "UC-079",
    fields: [],
    initial: "Draft",
  };
  let r = s.save(d, {
    name: "Receipt",
    type: "Receipt",
    warehouse: db.warehouses[0].id,
    lines: [{ item: db.items[0].id, quantity: 5 }],
  });
  const before = s.balance(s.snapshot(), r.warehouse, db.items[0].id);
  r = s.perform(
    d,
    r,
    { label: "Post receipt", uc: "UC-080", from: ["Draft"], to: "Posted" },
    { lines: r.lines },
  );
  assert.equal(
    s.balance(s.snapshot(), r.warehouse, db.items[0].id),
    before + 5,
  );
  assert.throws(
    () =>
      s.perform(
        d,
        r,
        { label: "Post receipt", uc: "UC-080", from: ["Draft"], to: "Posted" },
        { lines: r.lines },
      ),
    /no longer available/,
  );
});
test("material approval and partial issue preserve approved remainder and reject negative stock", () => {
  login("headhousekeeping");
  let r = s
    .visible("materials")
    .find((r) => r.name === "Replenish room amenities");
  const d = { key: "materials" };
  r = s.perform(
    d,
    r,
    {
      label: "Approve",
      uc: "UC-095",
      from: ["Pending Approval"],
      to: "Approved",
    },
    { lines: r.lines },
  );
  s.logout();
  login("storekeeper");
  r = s.visible("materials").find((m) => m.id === r.id);
  const item = r.lines[0].item,
    warehouse = r.warehouse;
  const before = s.balance(s.snapshot(), warehouse, item);
  r = s.perform(
    d,
    r,
    {
      label: "Partial issue",
      uc: "UC-098",
      from: ["Approved", "Partially Issued"],
    },
    { warehouse, receiver: s.currentUser().id, lines: [{ item, quantity: 5 }] },
  );
  assert.equal(r.status, "Partially Issued");
  assert.equal(r.lines[0].issued, 5);
  assert.equal(s.balance(s.snapshot(), warehouse, item), before - 5);
  assert.throws(
    () =>
      s.perform(
        d,
        r,
        {
          label: "Partial issue",
          uc: "UC-098",
          from: ["Approved", "Partially Issued"],
        },
        {
          warehouse,
          receiver: s.currentUser().id,
          lines: [{ item, quantity: 100 }],
        },
      ),
    /approved remainder/,
  );
});
test("maintenance occurrences are generated once and next due follows schedule", () => {
  login("maintenancelead");
  const db = s.snapshot();
  const d = {
    key: "schedules",
    createUC: "UC-061",
    fields: [],
    initial: "Active",
  };
  const due = new Date(Date.now() + 86400000).toISOString();
  const r = s.save(d, {
    name: "Monthly HVAC",
    room: db.rooms[0].id,
    criteria: "Filter checked",
    interval: 1,
    intervalUnit: "Months",
    due,
    assignee: db.users.find((u) => u.username === "technician").id,
  });
  assert.equal(s.generateDueMaintenance(), 1);
  assert.equal(s.generateDueMaintenance(), 0);
  const occurrence = s.snapshot().occurrences.find((o) => o.schedule === r.id);
  assert.equal(occurrence.scheduledDue, due);
  assert.equal(occurrence.status, "Assigned");
});
