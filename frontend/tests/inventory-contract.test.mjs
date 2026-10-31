import { test } from "node:test";
import assert from "node:assert/strict";
import { bundle, browserGlobals, json } from "./api-test-helpers.mjs";

const session = (restricted = false) => ({
  user: { id: "7", username: "tester", name: "Tester" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: restricted,
  csrfToken: "session-csrf",
});
const data = () => ({
  units: [
    {
      uomId: 1,
      uomCode: "EA",
      uomName: "Each",
      isActive: true,
      rowVersion: "AQ==",
    },
  ],
  "item-categories": [{ id: 1, code: "SUP", name: "Supplies", isActive: true }],
  items: [
    {
      itemId: 2,
      itemCode: "TOWEL",
      itemName: "Towel",
      categoryId: 1,
      baseUomId: 1,
      isActive: true,
      isConsumable: false,
      rowVersion: "Ag==",
    },
  ],
  warehouses: [
    {
      warehouseId: 3,
      branchId: 4,
      departmentId: null,
      warehouseCode: "WH",
      warehouseName: "Main warehouse",
      isActive: true,
      rowVersion: "Aw==",
    },
  ],
  stock: [
    {
      warehouseId: 3,
      itemId: 2,
      available: 7,
      onHand: 7,
      minimumQty: 10,
      belowMinimum: true,
    },
  ],
  documents: [
    {
      documentId: 5,
      documentNo: "RC-5",
      documentType: "RECEIPT",
      status: "POSTED",
      destinationWarehouseId: 3,
      documentDate: "2026-10-31",
      createdAt: "2026-10-31T08:00:00Z",
      rowVersion: "BA==",
      lines: [
        {
          lineId: 1,
          itemId: 2,
          uomId: 1,
          requestedQty: 10,
          actualQty: 7,
          baseQty: 7,
        },
      ],
    },
  ],
  stocktakes: [
    {
      stocktakeId: 6,
      stocktakeNo: "ST-6",
      warehouseId: 3,
      snapshotAt: "2026-10-31T08:00:00Z",
      status: "DRAFT",
      rowVersion: "BQ==",
      lines: [
        {
          stocktakeLineId: 1,
          itemId: 2,
          systemQty: 7,
          countedQty: null,
          varianceQty: null,
        },
      ],
    },
  ],
  movements: [
    {
      movementId: 8,
      documentId: 5,
      documentNo: "RC-5",
      documentType: "RECEIPT",
      status: "POSTED",
      warehouseId: 3,
      itemId: 2,
      signedBaseQty: 7,
      occurredAt: "2026-10-31T08:00:00Z",
    },
  ],
});
const keys = {
  units: "uomId",
  items: "itemId",
  warehouses: "warehouseId",
  documents: "documentId",
  stocktakes: "stocktakeId",
};
function transport(calls, rows = data(), options = {}) {
  return async (url, init) => {
    const path = new URL(url).pathname;
    calls.push({ path, init });
    if (path === "/api/auth/csrf")
      return json({ csrfToken: "login-csrf", headerName: "X-CSRF-TOKEN" });
    if (
      path === "/api/auth/login" ||
      path === "/api/auth/session" ||
      path === "/api/auth/change-password"
    )
      return json(session(options.restricted));
    if (path === "/api/auth/logout") return new Response(null, { status: 204 });
    assert.equal(init.method, "GET");
    assert.equal(init.credentials, "include");
    assert.ok(path.startsWith("/api/inventory/"), path);
    if (options.status) return json({ title: "Denied" }, options.status);
    const [, , , resource, id] = path.split("/");
    assert.ok(resource in rows, resource);
    if (id) {
      const value = rows[resource].find(
        (x) => String(x[keys[resource]]) === id,
      );
      return value ? json(value) : json({ title: "Missing" }, 404);
    }
    if (resource === "item-categories") return json(rows[resource]);
    return json({
      items: rows[resource],
      totalCount: rows[resource].length,
      page: 1,
      pageSize: 100,
    });
  };
}
let n = 0;
async function setup(options = {}) {
  browserGlobals();
  const service = await bundle("src/services.ts", `inventory-service-${n++}`);
  const calls = [];
  globalThis.fetch = transport(calls, data(), options);
  await service.login("tester", "test-password");
  return { service, calls };
}

test("all eleven inventory screens use real inventory routes without context or fabricated grants", async () => {
  const { service, calls } = await setup();
  const details = { 52: "3", 55: "2", 58: "5", 60: "6" };
  for (let screen = 51; screen <= 61; screen++) {
    assert.equal(service.canOpenScreen(screen), true);
    await service.loadScreen(screen, details[screen]);
    assert.equal(service.visible("warehouses")[0].branchId, "4");
    assert.equal(service.visible("items")[0].unit, "Each");
    assert.equal(service.balance(service.snapshot(), "3", "2"), 7);
    assert.equal(service.minimumStock(service.visible("items")[0], "3"), 10);
  }
  assert.equal(service.canOpenScreen(6), false);
  assert.deepEqual(service.allowedBranches(), []);
  assert.equal(service.can("UC-075"), false);
  assert.equal(service.can("UC-079"), false);
  assert.equal(service.inScope({ branchId: "4" }), false);
  assert.equal(service.serverSnapshot(), "");
  assert.ok(
    calls.every(
      (x) => !x.path.includes("/ui/") && !x.path.includes("/dashboards/"),
    ),
  );
  const login = calls.find((x) => x.path.endsWith("/login"));
  assert.equal(login.init.headers.get("X-CSRF-TOKEN"), "login-csrf");
});

test("403 clears stale inventory data but preserves authentication", async () => {
  const { service, calls } = await setup();
  await service.loadScreen(54);
  globalThis.fetch = transport(calls, data(), { status: 403 });
  await assert.rejects(service.loadScreen(54), (e) => e.status === 403);
  assert.equal(service.visible("items").length, 0);
  assert.ok(service.currentSession());
});

test("401 clears identity and inventory; logout sends current CSRF and clears data", async () => {
  const { service, calls } = await setup();
  await service.loadScreen(54);
  await service.logout();
  assert.equal(calls.at(-1).init.headers.get("X-CSRF-TOKEN"), "session-csrf");
  assert.equal(service.currentSession(), null);
  assert.equal(service.visible("items").length, 0);
  assert.equal(service.canOpenScreen(54), false);
  await service.login("tester", "test-password");
  globalThis.fetch = transport(calls, data(), { status: 401 });
  await assert.rejects(service.loadScreen(54), (e) => e.status === 401);
  assert.equal(service.currentSession(), null);
});

test("restricted session cannot open inventory or issue inventory GETs", async () => {
  const { service, calls } = await setup({ restricted: true });
  assert.equal(service.canOpenScreen(54), false);
  assert.equal(service.canOpenScreen(3), true);
  await assert.rejects(service.loadScreen(54), (e) => e.status === 403);
  assert.equal(
    calls.filter((x) => x.path.startsWith("/api/inventory/")).length,
    0,
  );
});

test("404 detail is surfaced and never falls back to a list record", async () => {
  const { service } = await setup();
  await assert.rejects(service.loadScreen(55, "999"), (e) => e.status === 404);
  assert.equal(service.visible("items").length, 0);
  await assert.rejects(
    service.loadScreen(55, "../items"),
    (e) => e.status === 404,
  );
});

test("empty inventory is valid; absent stock and historical balances are not zero", async () => {
  const { service, calls } = await setup();
  globalThis.fetch = transport(
    calls,
    Object.fromEntries(Object.keys(data()).map((k) => [k, []])),
  );
  await service.loadScreen(53);
  assert.equal(service.visible("items").length, 0);
  assert.ok(Number.isNaN(service.balance(service.snapshot(), "3", "2")));
  globalThis.fetch = transport(calls);
  await service.loadScreen(61);
  assert.equal(service.visible("ledger")[0].delta, 7);
  assert.ok(
    Number.isNaN(
      service.balance(service.snapshot(), "3", "2", "2026-10-31T08:00:00Z"),
    ),
  );
});

test("null stocktake counts remain null and draft documents display requested quantities", async () => {
  const { service, calls } = await setup();
  await service.loadScreen(60, "6");
  assert.equal(service.visible("stocktakes")[0].lines[0].quantity, null);
  assert.equal(service.visible("stocktakes")[0].lines[0].variance, null);
  const rows = data();
  rows.documents[0].status = "DRAFT";
  globalThis.fetch = transport(calls, rows);
  await service.loadScreen(58, "5");
  assert.equal(service.visible("transactions")[0].lines[0].quantity, 10);
  assert.equal(service.visible("transactions")[0].canEdit, false);
  assert.deepEqual(service.visible("transactions")[0].allowedActions, []);
});

test("pagination follows backend pageSize without requiring snapshot", async () => {
  const { readInventoryPages } = await bundle(
    "src/controllers/inventoryController.ts",
    "inventory-pages",
  );
  const calls = [];
  globalThis.fetch = async (url) => {
    calls.push(String(url));
    const page = Number(new URL(url).searchParams.get("page"));
    return json({ items: [{ id: page }], page, pageSize: 1, totalCount: 2 });
  };
  assert.deepEqual(await readInventoryPages("items", (x) => String(x.id)), [
    { id: 1 },
    { id: 2 },
  ]);
  assert.equal(calls.length, 2);
  assert.ok(calls.every((x) => !x.includes("snapshot")));
});

test("pagination rejects changing totals, duplicate keys, incomplete pages and malformed data", async () => {
  const { readInventoryPages } = await bundle(
    "src/controllers/inventoryController.ts",
    "inventory-bad-pages",
  );
  for (const mode of ["total", "duplicate", "incomplete", "malformed"]) {
    globalThis.fetch = async (url) => {
      const page = Number(new URL(url).searchParams.get("page"));
      return json({
        items:
          mode === "malformed"
            ? null
            : mode === "incomplete"
              ? []
              : [{ id: mode === "duplicate" ? 1 : page }],
        page,
        pageSize: 1,
        totalCount: mode === "total" && page === 2 ? 3 : 2,
      });
    };
    await assert.rejects(
      readInventoryPages("items", (x) => String(x.id)),
      (e) => ["server", "conflict"].includes(e.kind),
    );
  }
});

test("unsafe SQL bigint IDs fail visibly", async () => {
  const { service, calls } = await setup();
  const rows = data();
  rows.items[0].itemId = Number.MAX_SAFE_INTEGER + 1;
  globalThis.fetch = transport(calls, rows);
  await assert.rejects(service.loadScreen(54), /unsafe identifier/);
  assert.equal(service.visible("items").length, 0);
});

test("late inventory responses after logout cannot refill the cache", async () => {
  const { service, calls } = await setup();
  const normal = transport(calls);
  let release;
  const blocked = new Promise((resolve) => {
    release = resolve;
  });
  globalThis.fetch = async (url, init) => {
    if (new URL(url).pathname === "/api/inventory/items") await blocked;
    return normal(url, init);
  };
  const pending = service.loadScreen(54);
  await service.logout();
  release();
  await assert.rejects(pending, (e) => e.status === 401);
  assert.equal(service.currentSession(), null);
  assert.equal(service.snapshot().items.length, 0);
});
