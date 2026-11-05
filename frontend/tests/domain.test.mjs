import { test, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { mkdir } from "node:fs/promises";
await mkdir(".test-output", { recursive: true });
await build({
  entryPoints: ["src/services.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/services.mjs",
});
class MemoryStorage {
  data = new Map();
  getItem(k) {
    return this.data.get(k) || null;
  }
  setItem(k, v) {
    this.data.set(k, String(v));
  }
  removeItem(k) {
    this.data.delete(k);
  }
  clear() {
    this.data.clear();
  }
}
let service;
beforeEach(async () => {
  globalThis.localStorage = new MemoryStorage();
  globalThis.sessionStorage = new MemoryStorage();
  service = await import(`../.test-output/services.mjs?case=${Math.random()}`);
});
const account = (username) => service.login(username, "HotelOps@2026");
test("prototype browsing never requires credentials or permissions", () => {
  assert.ok(service.currentUser());
  service.login("arbitrary demo name", "arbitrary demo value");
  assert.equal(sessionStorage.getItem("hotelops-session"), null);
  service.logout();
  assert.ok(service.currentUser());
  assert.equal(service.can("UC-004"), true);
  assert.equal(service.visible("rooms").length, 64);
  assert.ok(
    service.visible("rooms").some((room) => room.branch === "Cao Thang"),
  );
});

test("prototype work starts directly; completion updates room readiness and audit", () => {
  account("housekeeper");
  const task = service.visible("cleaning").find((t) => t.status === "Assigned");
  const d = { key: "cleaning" };
  let r = service.perform(
    d,
    task,
    {
      label: "Start / resume",
      uc: "UC-031",
      from: ["Assigned"],
      to: "In Progress",
      own: true,
    },
    { note: "Starting work" },
  );
  assert.equal(
    service.snapshot().rooms.find((v) => v.id === r.room).status,
    "Cleaning In Progress",
  );
  assert.throws(
    () =>
      service.perform(
        d,
        r,
        {
          label: "Submit for inspection",
          uc: "UC-034",
          from: ["In Progress"],
          to: "Pending Inspection",
          own: true,
        },
        { note: "Complete" },
      ),
    /mandatory/,
  );
  r = service.updateCriteria(d, r, r.criteria.split("\n"));
  r = service.perform(
    d,
    r,
    {
      label: "Submit for inspection",
      uc: "UC-034",
      from: ["In Progress"],
      to: "Pending Inspection",
      own: true,
    },
    { note: "All cleaning checks completed" },
  );
  assert.equal(
    service.snapshot().rooms.find((v) => v.id === r.room).status,
    "Pending Inspection",
  );
  assert.equal(service.snapshot().intervals.filter((i) => !i.end).length, 0);
  assert.ok(
    service.snapshot().audit.some((a) => a.action === "Submit for inspection"),
  );
});
test("a failed stock posting rolls back every movement and does not change draft", () => {
  account("storekeeper");
  const db = service.snapshot();
  const d = {
    key: "transactions",
    createUC: "UC-079",
    fields: [],
    initial: "Draft",
  };
  const receipt = service.save(d, {
    name: "Test receipt",
    warehouse: db.warehouses[0].id,
    type: "Receipt",
    lines: [
      { item: db.items[0].id, quantity: 5 },
      { item: db.items[1].id, quantity: 5 },
    ],
  });
  const before = service.balance(
    service.snapshot(),
    receipt.warehouse,
    db.items[0].id,
  );
  assert.throws(
    () =>
      service.perform(
        d,
        receipt,
        { label: "Post receipt", uc: "UC-080", from: ["Draft"], to: "Posted" },
        {
          lines: [
            { item: db.items[0].id, quantity: 5 },
            { item: db.items[1].id, quantity: 4 },
          ],
        },
      ),
    /discrepancy/,
  );
  assert.equal(
    service.balance(service.snapshot(), receipt.warehouse, db.items[0].id),
    before,
  );
  assert.equal(
    service.snapshot().transactions.find((v) => v.id === receipt.id).status,
    "Draft",
  );
});
test("stale row versions are rejected and secrets are excluded from audit", () => {
  account("admin");
  const user = service
    .snapshot()
    .users.find((u) => u.username === "housekeeper");
  const d = { key: "users" };
  service.perform(
    d,
    user,
    { label: "Activate", uc: "UC-006", to: "Active", reason: true },
    { reason: "Account details rechecked" },
  );
  assert.throws(
    () =>
      service.perform(
        d,
        user,
        { label: "Activate", uc: "UC-006", to: "Active", reason: true },
        { reason: "Account details rechecked" },
      ),
    /Record changed/,
  );
  assert.ok(
    service
      .snapshot()
      .audit.every(
        (a) =>
          !JSON.stringify(a.before).includes("HotelOps@2026") &&
          !JSON.stringify(a.after).includes("HotelOps@2026"),
      ),
  );
});
test("ending a workday with an open interval is rejected", () => {
  account("housekeeper");
  service.workday(true, "Covering an operational shift");
  const t = service.visible("cleaning").find((t) => t.status === "Assigned");
  service.perform(
    { key: "cleaning" },
    t,
    { label: "Start / resume", uc: "UC-031", to: "In Progress", own: true },
    { note: "Starting" },
  );
  assert.throws(() => service.workday(false, ""), /Pause or complete/);
});
