import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const compile = (n) =>
  ts.transpileModule(
    readFileSync(
      new URL(`../src/operationalTasks/${n}.ts`, import.meta.url),
      "utf8",
    ),
    {
      compilerOptions: {
        module: ts.ModuleKind.ESNext,
        target: ts.ScriptTarget.ES2022,
      },
    },
  ).outputText;
const url = (s) =>
  "data:text/javascript;base64," + Buffer.from(s).toString("base64");
const {
  createTask,
  creationOptions,
  readTaskPlan,
  changeTaskPlan,
  localPlanTime,
  utcPlanTime,
} = await import(
  url(
    compile("planning")
      .replace('"./progress"', JSON.stringify(url(compile("progress"))))
      .replace('"./api"', JSON.stringify(url(compile("api")))),
  )
);
const signal = new AbortController().signal;
const input = {
  requestKey: "2191a880-2566-4e6f-9baa-42e39d1ac980",
  departmentId: 3,
  categoryId: 4,
  title: "Operational review",
  description: null,
  requestedResult: "Reviewed result",
  criteria: [{ label: "Reviewed", required: true }],
  priorityId: 2,
  plannedStartAt: "2026-11-15T01:00:00Z",
  dueAt: "2026-11-15T02:00:00Z",
  slaMinutes: 60,
  roomId: null,
  areaId: null,
  internalRequestId: null,
};
const snapshot = {
  etag: '"0000000000000001"',
  data: {
    taskId: 9,
    branchId: 1,
    state: "ASSIGNED",
    requestedResult: input.requestedResult,
    priorityId: 2,
    plannedStartAt: input.plannedStartAt,
    dueAt: input.dueAt,
    slaMinutes: 60,
    canManage: true,
    canCancel: true,
    history: [],
    historyTotalCount: 0,
    page: 1,
    pageSize: 25,
  },
};
test("Create preserves idempotency key, criteria, explicit nulls and CSRF with no ambient credential leakage", async (t) => {
  let writes = 0;
  t.mock.method(globalThis, "fetch", async (path, init) => {
    assert.equal(init.credentials, "same-origin");
    assert.equal(init.cache, "no-store");
    assert.equal(init.redirect, "error");
    assert.equal(init.signal, signal);
    if (path === "/api/auth/csrf")
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "synthetic",
      });
    assert.equal(path, "/api/branches/1/operational-tasks");
    assert.equal(init.method, "POST");
    assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic");
    assert.deepEqual(JSON.parse(init.body), input);
    writes++;
    return Response.json(
      {
        taskId: 9,
        branchId: 1,
        code: "TASK-X",
        state: "PENDING_ASSIGNMENT",
        messageCode: "MSG08",
      },
      { status: 201, headers: { ETag: snapshot.etag } },
    );
  });
  assert.equal((await createTask(1, input, signal)).taskId, 9);
  assert.equal(writes, 1);
});
test("invalid creation criteria, locations, outcome and timeline never dispatch", async (t) => {
  const spy = t.mock.method(globalThis, "fetch", () => {
    throw Error("Unexpected");
  });
  for (const changes of [
    { criteria: [] },
    {
      criteria: [
        { label: "same", required: true },
        { label: " SAME ", required: false },
      ],
    },
    { criteria: [{ label: "X", required: 1 }] },
    { requestedResult: "  " },
    { roomId: 3, areaId: 4 },
    { internalRequestId: 5 },
    { dueAt: "2026-11-14T00:00:00Z" },
    { slaMinutes: 0 },
    { title: "x\u0001" },
    { requestKey: "no-key" },
  ])
    await assert.rejects(createTask(1, { ...input, ...changes }, signal));
  assert.equal(spy.mock.callCount(), 0);
});
test("linked creation binds source version and explicit optional choice without retrying stale writes", async (t) => {
  const linked = { ...input, internalRequestId: 17, requestLink: { version: "AAAAAAAAAAE=", mandatory: false } };
  let writes = 0;
  t.mock.method(globalThis, "fetch", async (path, init) => {
    if (path === "/api/auth/csrf") return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
    writes++;
    assert.equal(init.headers["If-Match"], '"AAAAAAAAAAE="');
    assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic");
    assert.deepEqual(JSON.parse(init.body), linked);
    return Response.json({ messageCode: "MSG_CONFLICT" }, { status: 412 });
  });
  await assert.rejects(createTask(1, linked, signal));
  assert.equal(writes, 1);
});
test("Create ambiguous outcome never retries automatically or accepts a foreign scope", async (t) => {
  for (const response of [
    Response.json({}, { status: 503 }),
    Response.json(
      {
        taskId: 9,
        branchId: 2,
        code: "TASK-X",
        state: "PENDING_ASSIGNMENT",
        messageCode: "MSG08",
      },
      { headers: { ETag: snapshot.etag } },
    ),
  ]) {
    let writes = 0;
    t.mock.method(globalThis, "fetch", async (path) =>
      path === "/api/auth/csrf"
        ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" })
        : (writes++, response),
    );
    await assert.rejects(createTask(1, input, signal));
    assert.equal(writes, 1);
    t.mock.restoreAll();
  }
});
test("planning time uses approved UTC+7 and preserves original persistence precision when unchanged", () => {
  assert.equal(
    localPlanTime("2026-11-14T22:12:34.1234567Z"),
    "2026-11-15T05:12:34.123",
  );
  assert.equal(
    utcPlanTime("2026-11-15T05:12:34.123", "2026-11-14T22:12:34.1234567Z"),
    "2026-11-14T22:12:34.1234567Z",
  );
  assert.equal(utcPlanTime("2026-11-15T05:12"), "2026-11-14T22:12:00.000Z");
  assert.equal(utcPlanTime(""), null);
  assert.throws(() => utcPlanTime("not a date"));
});
test("Plan and Cancel bind current resource and strong ETag; cancellation requires reason", async (t) => {
  const spy = t.mock.method(globalThis, "fetch", () => {
    throw Error("Unexpected");
  });
  for (const [s, plan, reason] of [
    [snapshot, null, "short"],
    [
      { ...snapshot, data: { ...snapshot.data, canManage: false } },
      input,
      "valid",
    ],
    [
      { ...snapshot, data: { ...snapshot.data, canCancel: false } },
      null,
      "Cancellation reason",
    ],
    [{ ...snapshot, data: { ...snapshot.data, taskId: 10 } }, input, "valid"],
    [{ ...snapshot, etag: 'W/"0000000000000001"' }, input, "valid"],
  ])
    await assert.rejects(changeTaskPlan(1, 9, s, plan, reason, signal));
  assert.equal(spy.mock.callCount(), 0);
  t.mock.restoreAll();
  t.mock.method(globalThis, "fetch", async (path, init) => {
    if (path === "/api/auth/csrf")
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "synthetic",
      });
    assert.equal(path, "/api/branches/1/operational-tasks/9/cancel");
    assert.equal(init.headers["If-Match"], snapshot.etag);
    assert.deepEqual(JSON.parse(init.body), { reason: "Cancellation reason" });
    return Response.json(
      { taskId: 9, branchId: 1, state: "CANCELLED", messageCode: "MSG08" },
      { headers: { ETag: '"0000000000000002"' } },
    );
  });
  await changeTaskPlan(1, 9, snapshot, null, " Cancellation reason ", signal);
});
test("stale Plan never replays and read rejects another task or malformed histories", async (t) => {
  let writes = 0;
  t.mock.method(globalThis, "fetch", async (path) =>
    path === "/api/auth/csrf"
      ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" })
      : (writes++, Response.json({}, { status: 412 })),
  );
  await assert.rejects(
    changeTaskPlan(1, 9, snapshot, input, "Rescheduled", signal),
    (e) => e.status === 412,
  );
  assert.equal(writes, 1);
  t.mock.restoreAll();
  for (const data of [
    { ...snapshot.data, taskId: 10 },
    { ...snapshot.data, historyTotalCount: -1 },
    { ...snapshot.data, history: [{ auditId: 0 }] },
  ]) {
    t.mock.method(globalThis, "fetch", async () =>
      Response.json(data, { headers: { ETag: snapshot.etag } }),
    );
    await assert.rejects(readTaskPlan(1, 9, 1, signal));
    t.mock.restoreAll();
  }
  t.mock.method(globalThis, "fetch", async () =>
    Response.json(
      { ...snapshot.data, priorityId: null },
      { headers: { ETag: snapshot.etag } },
    ),
  );
  assert.equal((await readTaskPlan(1, 9, 1, signal)).data.priorityId, null);
});
test("creation options scope department and reject malformed reference data", async (t) => {
  t.mock.method(globalThis, "fetch", async (path) => {
    assert.equal(
      path,
      "/api/branches/1/operational-tasks/creation-options?department=3",
    );
    return Response.json({
      departments: [{ id: 3, name: "Operations" }],
      categories: [{ id: 0, name: "Invalid" }],
      priorities: [],
    });
  });
  await assert.rejects(
    creationOptions(1, 3, signal),
    /Lựa chọn lập kế hoạch không hợp lệ/,
  );
});
