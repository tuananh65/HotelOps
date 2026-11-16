import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const url = (text) =>
  "data:text/javascript;base64," + Buffer.from(text).toString("base64");
const compile = (name) =>
  ts.transpileModule(
    readFileSync(
      new URL(`../src/operationalTasks/${name}.ts`, import.meta.url),
      "utf8",
    ),
    {
      compilerOptions: {
        module: ts.ModuleKind.ESNext,
        target: ts.ScriptTarget.ES2022,
      },
    },
  ).outputText;
const api = url(compile("api")),
  progress = url(compile("progress"));
const planning = url(
  compile("planning")
    .replace('"./progress"', JSON.stringify(progress))
    .replace('"./api"', JSON.stringify(api)),
);
const { readWorkload } = await import(
  url(
    compile("workload")
      .replace('"./api"', JSON.stringify(api))
      .replace('"./planning"', JSON.stringify(planning)),
  )
);
const signal = new AbortController().signal;
const row = {
  userId: 7,
  name: "Synthetic executor",
  employeeCode: "S-7",
  openTasks: 2,
  overdueTasks: 1,
  closedTasks: 1,
  closedEffortSeconds: 90,
  provisionalSeconds: 15,
};
const snapshot = {
  branchId: 1,
  from: "2026-11-15",
  to: "2026-11-15",
  asOf: "2026-11-15T10:00:00+00:00",
  page: 1,
  pageSize: 20,
};
const page = { ...snapshot, items: [row], totalCount: 1, messageCode: null };
test("explicit UTC calendar is sent and must be confirmed by the response", async (t) => {
  let response = { ...page, calendar: "UTC" };
  t.mock.method(globalThis, "fetch", async (path) => {
    assert.equal(new URL(path, "https://example.invalid").searchParams.get("calendar"), "UTC");
    return Response.json(response);
  });
  await readWorkload(1, { calendar: "UTC" }, 1, null, signal);
  response = { ...page, calendar: "Asia/Ho_Chi_Minh" };
  await assert.rejects(readWorkload(1, { calendar: "UTC" }, 1, null, signal), /snapshot/);
  response = page;
  await assert.rejects(readWorkload(1, { calendar: "UTC" }, 1, null, signal), /snapshot/);
});
test("workload preserves filters, identity and read-only request safety", async (t) => {
  const calls = [];
  t.mock.method(globalThis, "fetch", async (path, init) => {
    calls.push({ path, init });
    return Response.json(page);
  });
  assert.deepEqual(
    await readWorkload(
      1,
      {
        from: "2026-11-15",
        to: "2026-11-15",
        status: "CLOSED",
        search: "task & employee",
        departmentId: 2,
        priorityId: 3,
        overdueOnly: true,
      },
      1,
      null,
      signal,
    ),
    page,
  );
  assert.equal(calls.length, 1);
  const request = calls[0];
  assert.equal(request.init.credentials, "same-origin");
  assert.equal(request.init.cache, "no-store");
  assert.equal(request.init.redirect, "error");
  assert.equal(request.init.method, undefined);
  assert.equal(request.init.signal, signal);
  const parsed = new URL(request.path, "https://example.invalid");
  assert.equal(parsed.searchParams.get("search"), "task & employee");
  assert.equal(parsed.searchParams.get("status"), "CLOSED");
});
test("invalid periods, physical states and identities never issue a request", async (t) => {
  let calls = 0;
  t.mock.method(globalThis, "fetch", async () => {
    calls++;
    return Response.json(page);
  });
  for (const filters of [
    { from: "2026-02-30" },
    { from: "2026-11-16", to: "2026-11-15" },
    { departmentId: 0 },
    { status: "COMPLETED" },
    { search: "x\u0001" },
  ])
    await assert.rejects(readWorkload(1, filters, 1, null, signal));
  await assert.rejects(readWorkload(1, { employeeId: 7 }, 1, 8, signal));
  assert.equal(calls, 0);
});
test("foreign or inconsistent workload aggregates fail closed", async (t) => {
  for (const bad of [
    { ...page, branchId: 2 },
    { ...page, page: 2 },
    { ...page, items: [row, row] },
    { ...page, totalCount: 0 },
    { ...page, items: [{ ...row, overdueTasks: 3 }] },
    { ...page, items: [{ ...row, closedEffortSeconds: -1 }] },
    { ...page, from: "2026-11-16" },
  ]) {
    const mock = t.mock.method(globalThis, "fetch", async () =>
      Response.json(bad),
    );
    await assert.rejects(readWorkload(1, {}, 1, null, signal));
    mock.mock.restore();
  }
});
test("employee intervals separate closed effort, provisional and unavailable shift time", async (t) => {
  const interval = {
    intervalId: 9,
    taskId: 4,
    start: "2026-11-15T09:00:00Z",
    end: "2026-11-15T09:01:00Z",
    closedSeconds: 60,
    provisionalSeconds: 0,
  };
  const detail = {
    ...snapshot,
    userId: 7,
    name: "Synthetic executor",
    tasks: [],
    taskCount: 0,
    intervals: [interval],
    intervalCount: 1,
    shifts: null,
    shiftCount: null,
  };
  let payload = detail;
  t.mock.method(globalThis, "fetch", async () => Response.json(payload));
  assert.deepEqual(await readWorkload(1, {}, 1, 7, signal), detail);
  for (const bad of [
    { ...detail, userId: 8 },
    { ...detail, shifts: null, shiftCount: 1 },
    { ...detail, intervals: [{ ...interval, provisionalSeconds: 10 }] },
    { ...detail, intervals: [{ ...interval, end: null, closedSeconds: 60 }] },
  ]) {
    payload = bad;
    await assert.rejects(readWorkload(1, {}, 1, 7, signal));
  }
});
test("forbidden and aborted reads are not retried", async (t) => {
  let calls = 0;
  const mock = t.mock.method(globalThis, "fetch", async () => {
    calls++;
    return new Response(null, { status: 403 });
  });
  await assert.rejects(
    readWorkload(1, {}, 1, null, signal),
    (e) => e.status === 403,
  );
  assert.equal(calls, 1);
  mock.mock.restore();
  t.mock.method(globalThis, "fetch", async () => {
    calls++;
    throw new DOMException("Aborted", "AbortError");
  });
  await assert.rejects(
    readWorkload(1, {}, 1, null, signal),
    (e) => e.name === "AbortError",
  );
  assert.equal(calls, 2);
});
