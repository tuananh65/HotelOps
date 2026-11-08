import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: [
    "src/features/inspectionChecklists/api.ts",
    "src/features/inspectionChecklists/collection.ts",
  ],
  outdir: ".test-output/fe05-collection",
  bundle: true,
  platform: "node",
  format: "esm",
  outExtension: { ".js": ".mjs" },
});
const { createChecklistApi, ChecklistApiError } =
  await import("../.test-output/fe05-collection/api.mjs");
const { defaultCollectionQuery, collectionParams, collectionQuery } =
  await import("../.test-output/fe05-collection/collection.mjs");
const version = {
  id: 10,
  number: 1,
  status: "RETIRED",
  effectiveAt: null,
  publishedBy: null,
  usedByInspection: true,
};
const item = {
  id: 42,
  branchId: 7,
  code: "CL1",
  name: "Room",
  active: true,
  roomTypeIds: [11],
  versions: [
    version,
    {
      ...version,
      id: 11,
      number: 2,
      status: "PUBLISHED",
      usedByInspection: false,
    },
  ],
};
const page = (query = defaultCollectionQuery()) => ({
  ...query,
  items: [structuredClone(item)],
  total: 1,
  branchId: 7,
});
const json = (body, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
test("collection GET uses exact route, credentials, defaults, and needs neither CSRF nor ETag", async () => {
  const calls = [];
  const api = createChecklistApi(async (path, options) => {
    calls.push(path);
    assert.equal(options.method, "GET");
    assert.equal(options.credentials, "include");
    assert.equal(options.cache, "no-store");
    assert.equal(options.headers["If-Match"], undefined);
    return json(page());
  });
  const result = await api.list(7, defaultCollectionQuery());
  assert.deepEqual(calls, [
    "/api/branches/7/inspection-checklists?page=1&pageSize=20",
  ]);
  assert.equal(result.items[0].versions[0].usedByInspection, true);
});
test("filter serialization preserves false and all documented parameters", () => {
  assert.deepEqual(
    Object.fromEntries(
      collectionParams({
        page: 2,
        pageSize: 50,
        search: "  A & B  ",
        isActive: false,
        status: "RETIRED",
        roomTypeId: 11,
      }),
    ),
    {
      page: "2",
      pageSize: "50",
      search: "A & B",
      isActive: "false",
      status: "RETIRED",
      roomTypeId: "11",
    },
  );
});
test("history status query keeps every version summary without requiring criteria", async () => {
  const query = { ...defaultCollectionQuery(), status: "RETIRED" };
  const result = await createChecklistApi(async () => json(page(query))).list(
    7,
    query,
  );
  assert.deepEqual(
    result.items[0].versions.map((v) => v.status),
    ["RETIRED", "PUBLISHED"],
  );
  assert.equal(result.items[0].versions[0].criteria, undefined);
});
test("empty and out-of-range collection pages retain filtered total", async () => {
  const query = { ...defaultCollectionQuery(), page: 2 };
  const result = await createChecklistApi(async () =>
    json({ ...page(query), items: [], total: 1 }),
  ).list(7, query);
  assert.equal(result.total, 1);
  assert.deepEqual(result.items, []);
});
for (const status of [400, 401, 403, 404, 409, 503])
  test(`collection ${status} is propagated without retry`, async () => {
    let calls = 0;
    await assert.rejects(
      createChecklistApi(async () => {
        calls++;
        return json({}, status);
      }).list(7, defaultCollectionQuery()),
      (e) =>
        e instanceof ChecklistApiError && e.status === status && !e.uncertain,
    );
    assert.equal(calls, 1);
  });
for (const change of [
  (v) => (v.branchId = 8),
  (v) => (v.items[0].branchId = 8),
  (v) => (v.page = 2),
  (v) => (v.total = -1),
  (v) => (v.items[0].versions[0].usedByInspection = undefined),
  (v) => (v.items[0].versions[0].status = "UNKNOWN"),
  (v) => (v.items[0].id = Number.MAX_SAFE_INTEGER + 1),
  (v) => (v.status = "DRAFT"),
])
  test(`malformed collection is rejected: ${change}`, async () => {
    const value = page();
    change(value);
    await assert.rejects(
      createChecklistApi(async () => json(value)).list(
        7,
        defaultCollectionQuery(),
      ),
      (e) => e.status === 503,
    );
  });
test("invalid filters and overflow are rejected before sending a request", async () => {
  for (const patch of [
    { page: 0 },
    { pageSize: 101 },
    { page: 2147483647, pageSize: 100 },
    { status: "draft" },
    { roomTypeId: 0 },
    { search: "x".repeat(201) },
    { search: "x\0y" },
  ]) {
    let sent = false;
    await assert.rejects(
      createChecklistApi(async () => {
        sent = true;
        return json(page());
      }).list(7, { ...defaultCollectionQuery(), ...patch }),
    );
    assert.equal(sent, false);
  }
  assert.equal(
    collectionQuery({ ...defaultCollectionQuery(), search: "  " }).search,
    null,
  );
});
test("aborted collection request stays an AbortError and uses caller signal", async () => {
  const abort = new AbortController();
  abort.abort();
  await assert.rejects(
    createChecklistApi(async (_path, options) => {
      assert.equal(options.signal, abort.signal);
      throw new DOMException("Aborted", "AbortError");
    }).list(7, defaultCollectionQuery(), abort.signal),
    (e) => e.name === "AbortError",
  );
});
test("HTML routing fallback cannot become collection data", async () => {
  await assert.rejects(
    createChecklistApi(async () => new Response("<html>SPA</html>")).list(
      7,
      defaultCollectionQuery(),
    ),
    (e) => e.status === 503,
  );
});
