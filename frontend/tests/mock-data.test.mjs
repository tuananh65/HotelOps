import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/services.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/mock-data.mjs",
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
async function fresh(tag) {
  globalThis.localStorage = new Storage();
  globalThis.sessionStorage = new Storage();
  return import(`../.test-output/mock-data.mjs?${tag}`);
}
test("rich mock data meets demo volumes and every operational reference resolves", async () => {
  const s = await fresh("volumes"),
    db = s.snapshot();
  const minimums = {
    users: 30,
    roles: 9,
    branches: 2,
    locations: 10,
    roomTypes: 5,
    rooms: 40,
    departments: 8,
    cleaning: 20,
    rework: 10,
    inspections: 20,
    incidents: 20,
    occurrences: 15,
    schedules: 10,
    assets: 30,
    warehouses: 4,
    items: 30,
    transactions: 30,
    stocktakes: 10,
    materials: 15,
    replenishments: 10,
    tasks: 25,
    shifts: 15,
    handovers: 10,
    requests: 15,
    notifications: 20,
    audit: 30,
  };
  for (const [key, min] of Object.entries(minimums))
    assert.ok(db[key].length >= min, `${key}: ${db[key].length} < ${min}`);
  for (const room of db.rooms) {
    const area = db.locations.find((location) => location.id === room.area);
    assert.equal(
      area?.kind || area?.type,
      "Area",
      `${room.id}.area must describe an area, not a floor`,
    );
    assert.equal(area.branch, room.branch);
  }
  const references = {
    room: "rooms",
    floor: "locations",
    area: "locations",
    roomType: "roomTypes",
    asset: "assets",
    assignee: "users",
    creator: "users",
    cleaning: "cleaning",
    inspection: "inspections",
    warehouse: "warehouses",
    checklist: "checklists",
  };
  for (const records of Object.values(db))
    for (const r of records) {
      for (const [field, key] of Object.entries(references))
        if (r[field] && typeof r[field] === "string")
          assert.ok(
            db[key].some((e) => e.id === r[field]),
            `${r.id}.${field}`,
          );
      for (const line of r.lines || [])
        assert.ok(
          db.items.some((i) => i.id === line.item),
          `${r.id}.lines.item`,
        );
      if (r.relatedId)
        assert.ok(
          db[r.relatedKey].some((e) => e.id === r.relatedId),
          `${r.id}.relatedId`,
        );
    }
});
test("fresh seeds have stable IDs; reload preserves edits and adds no duplicate mock records", async () => {
  const first = await fresh("stable1");
  const ids = Object.fromEntries(
    Object.entries(first.snapshot()).map(([k, v]) => [k, v.map((r) => r.id)]),
  );
  const second = await fresh("stable2");
  assert.deepEqual(
    Object.fromEntries(
      Object.entries(second.snapshot()).map(([k, v]) => [
        k,
        v.map((r) => r.id),
      ]),
    ),
    ids,
  );
  second.login("admin", "HotelOps@2026");
  const stored = JSON.parse(
    localStorage.getItem("hotelops-public-prototype-v1"),
  );
  stored.rooms[0].description = "User edit survives migration";
  stored.rooms[0].area = stored.rooms[0].floor;
  localStorage.setItem("hotelops-public-prototype-v1", JSON.stringify(stored));
  const reloaded = await import("../.test-output/mock-data.mjs?reload");
  assert.equal(
    reloaded.snapshot().rooms[0].description,
    "User edit survives migration",
  );
  assert.equal(reloaded.snapshot().rooms.length, stored.rooms.length);
  const room = reloaded.snapshot().rooms[0];
  assert.equal(
    reloaded.snapshot().locations.find((location) => location.id === room.area)
      ?.kind,
    "Area",
  );
});
test("demo URL aliases resolve pre-refactor IDs without changing linked records", async () => {
  const service = await fresh("legacy-url");
  const original = service.snapshot().rooms[0].id;
  const legacy = "legacy-room-uuid";
  localStorage.setItem(
    "hotelops-public-prototype-v1",
    JSON.stringify(service.snapshot())
      .split(JSON.stringify(original))
      .join(JSON.stringify(legacy)),
  );
  const migrated =
    await import("../.test-output/mock-data.mjs?legacy-reloaded");
  assert.equal(migrated.resolveDemoRecordId("rooms", original), legacy);
  assert.ok(migrated.snapshot().rooms.some((room) => room.id === legacy));
  assert.equal(migrated.snapshot().rooms.length, 64);
});
