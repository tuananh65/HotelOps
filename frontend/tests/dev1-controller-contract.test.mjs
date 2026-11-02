import { test } from "node:test";
import assert from "node:assert/strict";
import { bundle, browserGlobals, json } from "./api-test-helpers.mjs";

const session = {
  user: { id: "7", username: "tester", name: "Tester" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "existing-session-csrf",
};
let sequence = 0;
async function setup() {
  browserGlobals();
  const m = await bundle(
    "tests/dev1-controller-entry.ts",
    `dev1-${++sequence}`,
  );
  globalThis.fetch = async (url) =>
    String(url).endsWith("/csrf")
      ? json({ csrfToken: "bootstrap", headerName: "X-CSRF-TOKEN" })
      : json(session);
  await m.login("tester", "test-only");
  const calls = [];
  globalThis.fetch = async (url, opts) => {
    calls.push({ url: String(url), opts });
    return new Response(JSON.stringify({ id: 2 }), {
      status: 200,
      headers: { "Content-Type": "application/json", ETag: '"rev-1"' },
    });
  };
  return { m, calls };
}
function checkWrite(call, method, path, body, etag) {
  assert.equal(call.url, `https://api.test${path}`);
  assert.equal(call.opts.method, method);
  assert.equal(call.opts.credentials, "include");
  assert.equal(call.opts.headers.get("X-CSRF-TOKEN"), "existing-session-csrf");
  assert.equal(call.opts.headers.get("If-Match"), etag ?? null);
  assert.deepEqual(JSON.parse(call.opts.body), body);
}
test("structure writes preserve auth and exact ETag; UI extras are omitted", async () => {
  const { m, calls } = await setup();
  const v = await m.hotelStructureController.floor(1, 2);
  assert.equal(v.etag, '"rev-1"');
  await m.hotelStructureController.updateFloor(1, 2, v, {
    code: "F2",
    name: "Second",
    reason: "rename",
    floorNumber: 99,
    allowedActions: ["ADMIN"],
  });
  checkWrite(
    calls[1],
    "PUT",
    "/api/branches/1/floors/2",
    { code: "F2", name: "Second", reason: "rename" },
    '"rev-1"',
  );
  await m.hotelStructureController.createArea(1, {
    code: "A",
    name: "Area",
    floorId: 2,
  });
  checkWrite(calls[2], "POST", "/api/branches/1/areas", {
    code: "A",
    name: "Area",
    floorId: 2,
  });
});
test("wrong-branch, missing and weak ETags never send writes", async () => {
  const { m, calls } = await setup();
  const v = await m.hotelStructureController.area(1, 2);
  const input = { code: "A", name: "Area", reason: "change" };
  assert.throws(
    () => m.hotelStructureController.updateArea(2, 2, v, input),
    (e) => e.status === 412,
  );
  for (const etag of [null, 'W/"rev-1"'])
    assert.throws(
      () => m.hotelStructureController.updateArea(1, 2, { ...v, etag }, input),
      (e) => e.status === 428,
    );
  assert.equal(calls.length, 1);
});
test("room-type list/detail/update/deactivate use scoped native contract", async () => {
  const { m, calls } = await setup();
  await m.roomTypeController.list({
    branchId: 1,
    isActive: false,
    search: "A & B",
  });
  assert.match(calls[0].url, /branchId=1&isActive=false&search=A\+%26\+B$/);
  const v = await m.roomTypeController.detail(1, 2);
  await m.roomTypeController.update(1, 2, v, {
    name: "Deluxe",
    standardCleaningMinutes: 30,
    defaultChecklistId: 4,
    standardCapacity: 5,
  });
  checkWrite(
    calls[2],
    "PUT",
    "/api/room-types/2?branchId=1",
    { name: "Deluxe", standardCleaningMinutes: 30, defaultChecklistId: 4 },
    '"rev-1"',
  );
  await m.roomTypeController.deactivate(1, 2, v, {
    confirmation: true,
    reason: "retire",
  });
  checkWrite(
    calls[3],
    "POST",
    "/api/room-types/2/deactivate?branchId=1",
    { confirmation: true, reason: "retire" },
    '"rev-1"',
  );
});
test("room update uses edit ETag and list requires explicit scope", async () => {
  const { m, calls } = await setup();
  assert.throws(
    () => m.roomController.list({}),
    (e) => e.status === 400,
  );
  assert.throws(
    () => m.roomController.list({ allBranches: true, branchId: 1 }),
    (e) => e.status === 400,
  );
  await m.roomController.list({ allBranches: true });
  const v = await m.roomController.edit(1, 2);
  const input = {
    floorId: 3,
    roomTypeId: 4,
    roomNumber: "301",
    reason: "correct",
    areaId: null,
  };
  await m.roomController.update(1, 2, v, input);
  checkWrite(calls[2], "PUT", "/api/rooms/2?branchId=1", input, '"rev-1"');
});
test("department list uses structure and update cannot rename department code", async () => {
  const { m, calls } = await setup();
  await m.departmentController.list(1);
  assert.equal(
    calls[0].url,
    "https://api.test/api/branches/1/structure?nodeType=branch&collection=departments",
  );
  const v = await m.departmentController.detail(1, 2);
  await m.departmentController.update(1, 2, v, {
    name: "HK",
    managerUserId: null,
    code: "IGNORED",
  });
  checkWrite(
    calls[2],
    "PUT",
    "/api/branches/1/departments/2",
    { name: "HK", managerUserId: null },
    '"rev-1"',
  );
});
test("checklist criteria and transitions use DEV1 versioned DTOs", async () => {
  const { m, calls } = await setup();
  const v = await m.checklistController.detail(1, 2);
  const criteria = [
    { text: "Clean", group: null, mandatory: true, allowedActions: [] },
  ];
  await m.checklistController.update(1, 2, v, { criteria, reason: "change" });
  checkWrite(
    calls[1],
    "PUT",
    "/api/branches/1/inspection-checklists/2",
    {
      criteria: [{ text: "Clean", group: null, mandatory: true }],
      reason: "change",
    },
    '"rev-1"',
  );
  await m.checklistController.publish(1, 2, v, {
    reason: "approved",
    confirmation: true,
  });
  checkWrite(
    calls[2],
    "POST",
    "/api/branches/1/inspection-checklists/2/publish",
    { reason: "approved", confirmation: true },
    '"rev-1"',
  );
  assert.equal(m.checklistController.list, undefined);
});
test("assignment forwards original planning ETag and notification remains native", async () => {
  const { m, calls } = await setup();
  const q = { page: 2, search: "A" };
  const v = await m.housekeepingController.assignmentOptions(1, 2, q);
  const input = {
    assigneeId: 3,
    dueAt: "2026-11-03T10:00:00Z",
    reason: "assign",
  };
  await m.housekeepingController.assignCleaning(1, 2, v, input, q);
  checkWrite(
    calls[1],
    "POST",
    "/api/branches/1/cleaning-tasks/2/assign",
    input,
    '"rev-1"',
  );
  await m.housekeepingController.assignInspection(1, 2, {
    inspectorId: 4,
    dueAt: input.dueAt,
    reason: "check",
  });
  assert.equal(calls[2].opts.headers.get("If-Match"), null);
});
test("inspection results, evidence and finish do not fabricate approval", async () => {
  const { m, calls } = await setup();
  const v = await m.housekeepingController.execution(1, 2);
  await m.housekeepingController.saveInspection(1, 2, v, {
    reason: "check",
    results: [
      {
        itemId: 3,
        outcome: "PASS",
        note: null,
        photos: [
          {
            name: "x.png",
            mimeType: "image/png",
            base64: "AA==",
            url: "ignored",
          },
        ],
      },
    ],
  });
  assert.deepEqual(JSON.parse(calls[1].opts.body).results[0].photos, [
    { name: "x.png", mimeType: "image/png", base64: "AA==" },
  ]);
  await m.housekeepingController.finishInspection(1, 2, v, "done");
  checkWrite(
    calls[2],
    "POST",
    "/api/branches/1/inspections/2/finish",
    { reason: "done" },
    '"rev-1"',
  );
});
test("UC032 and UC109 preserve distinct routes and real progress bodies", async () => {
  const { m, calls } = await setup();
  await m.progressController.initializeCleaning(1, 2, "initialize");
  checkWrite(
    calls[0],
    "POST",
    "/api/branches/1/cleaning-tasks/2/progress-initialization",
    { items: null, reason: "initialize" },
  );
  const input = {
    percent: 100,
    items: [{ id: 5, done: true, outcome: null, note: null }],
    note: "done",
  };
  for (const kind of ["cleaning", "task"]) {
    const v = await m.progressController.detail(1, 2, kind);
    await m.progressController.save(1, 2, kind, v, input);
    checkWrite(
      calls.at(-1),
      "PUT",
      `/api/branches/1/${kind === "cleaning" ? "cleaning-tasks" : "operational-tasks"}/2/progress`,
      input,
      '"rev-1"',
    );
  }
  assert.equal(m.progressController.complete, undefined);
});
test("412/403/503 are surfaced, never retried or converted to empty success", async () => {
  const { m } = await setup();
  const v = await m.roomTypeController.detail(1, 2);
  for (const status of [412, 403, 503]) {
    let count = 0;
    globalThis.fetch = async () => {
      count++;
      return json({ title: "BACKEND_REJECTED", status }, status);
    };
    await assert.rejects(
      m.roomTypeController.update(1, 2, v, {
        name: "N",
        standardCleaningMinutes: 30,
        defaultChecklistId: 3,
      }),
      (e) => e.status === status,
    );
    assert.equal(count, 1);
  }
});
test("logout discards in-flight versioned result and blocks subsequent DEV1 reads", async () => {
  const { m } = await setup();
  let release;
  globalThis.fetch = async (url) =>
    String(url).endsWith("/logout")
      ? json({})
      : new Promise((resolve) => {
          release = resolve;
        });
  const pending = m.roomTypeController.detail(1, 2);
  await m.logout();
  release(json({ id: 2 }));
  await assert.rejects(pending, (e) => e.status === 401);
  await assert.rejects(
    m.roomTypeController.detail(1, 2),
    (e) => e.status === 401,
  );
});
