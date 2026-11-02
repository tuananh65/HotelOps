import { test } from "node:test";
import assert from "node:assert/strict";
import { bundle, browserGlobals, json } from "./api-test-helpers.mjs";
let sequence = 0;
async function setup() {
  browserGlobals();
  const m = await bundle(
    "tests/dev1-ui-model-entry.ts",
    "dev1-ui-" + ++sequence,
  );
  globalThis.fetch = async (url) =>
    String(url).endsWith("/csrf")
      ? json({ csrfToken: "csrf", headerName: "X-CSRF-TOKEN" })
      : json({
          user: { id: "7", username: "test", name: "Test" },
          expiresAt: "2099-01-01T00:00:00Z",
          absoluteExpiresAt: "2099-01-02T00:00:00Z",
          mustChangePassword: false,
          csrfToken: "csrf",
        });
  await m.login("test", "test");
  const calls = [];
  globalThis.fetch = async (url, o) => {
    calls.push({ url: String(url), o });
    return new Response(
      JSON.stringify({
        id: 3,
        items: [],
        total: 0,
        departments: { items: [], totalCount: 0 },
        floors: { items: [], totalCount: 0 },
        areas: { items: [], totalCount: 0 },
      }),
      { headers: { "Content-Type": "application/json", ETag: '"v1"' } },
    );
  };
  return { m, calls };
}
test("all six UI modules read native routes without legacy screen context", async () => {
  const { m, calls } = await setup();
  const signal = new AbortController().signal;
  for (const kind of [
    "structure",
    "floors",
    "areas",
    "roomTypes",
    "rooms",
    "departments",
  ])
    await m.load(kind, 2, undefined, 1, signal);
  for (const kind of [
    "checklists",
    "planning",
    "inspection",
    "cleaningProgress",
    "taskProgress",
  ])
    await m.load(kind, 2, 3, 1, signal);
  assert.equal(calls.length, 11);
  assert.ok(calls.every((c) => !c.url.includes("/ui/screens/")));
  assert.ok(calls.some((c) => c.url.includes("/operational-tasks/3/progress")));
  assert.ok(calls.some((c) => c.url.includes("/inspection-checklists/3")));
});
test("UI create and edit uses committed branch and original ETag, not displayed ID fields", async () => {
  const { m, calls } = await setup();
  await m.save("rooms", "create", 2, undefined, undefined, {
    branchId: 99,
    floorId: 4,
    roomTypeId: 5,
    roomNumber: "301",
  });
  assert.equal(JSON.parse(calls[0].o.body).branchId, 2);
  const v = await m.load("rooms", 2, 3, 1, new AbortController().signal);
  await m.save("rooms", "edit", 2, 3, v.version, {
    floorId: 4,
    roomTypeId: 5,
    roomNumber: "302",
    reason: "test",
  });
  assert.equal(calls.at(-1).o.headers.get("If-Match"), '"v1"');
  assert.match(calls.at(-1).url, /rooms\/3\?branchId=2$/);
});
test("UI has no mutation snapshot fallback and surfaces stale-write errors", async () => {
  const { m } = await setup();
  await assert.rejects(m.save("rooms", "edit", 2, 3, undefined, {}));
  const v = await m.load("roomTypes", 2, 3, 1, new AbortController().signal);
  let writes = 0;
  globalThis.fetch = async () => {
    writes++;
    return json({ title: "STALE" }, 412);
  };
  await assert.rejects(
    m.save("roomTypes", "edit", 2, 3, v.version, {
      name: "N",
      standardCleaningMinutes: 30,
      defaultChecklistId: 4,
    }),
    (e) => e.status === 412,
  );
  assert.equal(writes, 1);
});
test("checklist form maps room-type IDs without fabricating list data", async () => {
  const { m, calls } = await setup();
  const list = await m.load(
    "checklists",
    2,
    undefined,
    1,
    new AbortController().signal,
  );
  assert.equal(list.data, null);
  assert.equal(calls.length, 0);
  await m.save("checklists", "create", 2, undefined, undefined, {
    code: "C",
    name: "Checklist",
    roomTypeIds: "3, 4",
    criteria: [{ text: "Clean", group: null, mandatory: true }],
  });
  assert.deepEqual(JSON.parse(calls[0].o.body).roomTypeIds, [3, 4]);
  await assert.rejects(
    m.save("checklists", "create", 2, undefined, undefined, {
      roomTypeIds: "invalid",
    }),
  );
});
test("housekeeping form uses ISO dates, original version and distinct UC032/109 bodies", async () => {
  const { m, calls } = await setup();
  let v = await m.load("planning", 2, 3, 1, new AbortController().signal);
  await m.save("planning", "assign", 2, 3, v.version, {
    assigneeId: 4,
    dueAt: "2026-11-03T10:00",
    reason: "test",
  });
  assert.match(JSON.parse(calls.at(-1).o.body).dueAt, /Z$/);
  for (const kind of ["cleaningProgress", "taskProgress"]) {
    v = await m.load(kind, 2, 3, 1, new AbortController().signal);
    await m.save(kind, "saveProgress", 2, 3, v.version, {
      percent: 50,
      note: null,
      items: [
        {
          id: 8,
          done: kind === "cleaningProgress" ? true : null,
          outcome: kind === "taskProgress" ? "PASS" : null,
          note: null,
        },
      ],
    });
    assert.equal(calls.at(-1).o.method, "PUT");
    assert.equal(calls.at(-1).o.headers.get("If-Match"), '"v1"');
  }
});
test("backend denied access clears the read result rather than returning fake empty content", async () => {
  const { m } = await setup();
  globalThis.fetch = async () => json({ title: "FORBIDDEN" }, 403);
  await assert.rejects(
    m.load("rooms", 2, undefined, 1, new AbortController().signal),
    (e) => e.status === 403,
  );
});
