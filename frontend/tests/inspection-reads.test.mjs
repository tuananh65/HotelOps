import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  entryPoints: ["src/inspection/reads.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
});
const { createInspectionReads, decodeSeverities } = await import(
  `data:text/javascript;base64,${Buffer.from(b.outputFiles[0].text).toString("base64")}`
);
const ok = (v) =>
  new Response(JSON.stringify(v), {
    headers: { "Content-Type": "application/json" },
  });
const signal = () => new AbortController().signal;
test("severity identities and labels come from server", () =>
  assert.deepEqual(
    decodeSeverities([{ id: 107, code: "MINOR", name: "Minor defect" }]),
    [{ id: 107, code: "MINOR", name: "Minor defect" }],
  ));
test("empty severity catalog is valid", () =>
  assert.deepEqual(decodeSeverities([]), []));
for (const value of [
  [{ id: 1, code: "CRITICAL", name: "Wrong domain" }],
  [{ id: 0, code: "MINOR", name: "Wrong ID" }],
  [
    { id: 1, code: "MAJOR", name: "A" },
    { id: 1, code: "MAJOR", name: "A" },
  ],
  {},
])
  test(`invalid catalog ${JSON.stringify(value)}`, () =>
    assert.throws(() => decodeSeverities(value)));
test("read uses exact scoped route and cookie transport", async () => {
  let captured;
  const c = createInspectionReads(async (u, i) => {
    captured = { u, i };
    return ok([]);
  });
  await c.severities(8, signal());
  assert.equal(captured.u, "/api/branches/8/inspection-severities");
  assert.equal(captured.i.credentials, "include");
  assert.equal(captured.i.cache, "no-store");
  assert.equal(captured.i.redirect, "error");
});
for (const status of [400, 401, 403, 409, 503])
  test(`read propagates ${status} without retry`, async () => {
    let count = 0;
    const c = createInspectionReads(async () => {
      count++;
      return new Response(JSON.stringify({ title: "DENIED" }), { status });
    });
    await assert.rejects(c.severities(8, signal()), (e) => e.status === status);
    assert.equal(count, 1);
  });
test("assigned list encodes server pagination/search without client authority", async () => {
  let path;
  const c = createInspectionReads(async (p) => {
    path = p;
    return ok({ items: [], total: 0, page: 2, pageSize: 20 });
  });
  await c.list(8, "assigned", "Room & desk", 2, signal());
  assert.ok(path.startsWith("/api/branches/8/inspections/assigned?"));
  assert.equal(
    new URL(path, "https://example.test").searchParams.get("search"),
    "Room & desk",
  );
});
test("queue path is read only", async () => {
  let path;
  const c = createInspectionReads(async (p) => {
    path = p;
    return ok({ items: [], total: 0, page: 1, pageSize: 20 });
  });
  await c.list(8, "tasks", "", 1, signal());
  assert.ok(path.includes("/inspection-assignment-tasks?"));
  assert.equal(typeof c.assign, "undefined");
});
test("wrong branch collection is rejected", async () => {
  const c = createInspectionReads(async () =>
    ok({
      items: [
        {
          id: 1,
          branchId: 9,
          roomId: 1,
          roomNumber: "1",
          number: "A",
          state: "ASSIGNED",
          inspectorId: 1,
          cleaningTaskId: 1,
          versionId: 1,
          inspectorName: "A",
        },
      ],
      total: 1,
      page: 1,
      pageSize: 20,
    }),
  );
  await assert.rejects(
    c.list(8, "assigned", "", 1, signal()),
    (e) => e.code === "INVALID_COLLECTION",
  );
});
for (const page of [0, -1, 1.5, 2147483647])
  test(`invalid page ${page} never sends`, async () => {
    let count = 0;
    const c = createInspectionReads(async () => {
      count++;
      return ok({});
    });
    await assert.rejects(c.list(8, "assigned", "", page, signal()));
    assert.equal(count, 0);
  });
test("planning source identity mismatch is rejected", async () => {
  const c = createInspectionReads(async () =>
    ok({
      task: { id: 99, branchId: 8 },
      candidates: [],
      recordedShifts: [],
      workflowStateVerified: true,
    }),
  );
  await assert.rejects(
    c.planning(8, 3, signal()),
    (e) => e.code === "INVALID_PLANNING",
  );
});
test("network read fails without fabricated data", async () => {
  const c = createInspectionReads(async () => {
    throw new TypeError("offline");
  });
  await assert.rejects(c.severities(8, signal()), (e) => e.status === 0);
});
