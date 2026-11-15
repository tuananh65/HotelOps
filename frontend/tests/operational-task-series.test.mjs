import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";
const url = text => "data:text/javascript;base64," + Buffer.from(text).toString("base64");
const compile = name => ts.transpileModule(readFileSync(new URL(`../src/operationalTasks/${name}.ts`, import.meta.url), "utf8"), {
  compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 },
}).outputText;
const planning = url(compile("planning").replace('"./progress"', JSON.stringify(url(compile("progress")))).replace('"./api"', JSON.stringify(url(compile("api")))));
const { createSeries, readSeries, stopSeries, validateRecurrence } = await import(url(compile("series").replace('"./planning"', JSON.stringify(planning))));
const signal = new AbortController().signal;
const template = { requestKey: "11111111-1111-1111-1111-111111111111", departmentId: 2, categoryId: 3, title: "Routine", description: "One\nTwo",
  requestedResult: "Reviewed\nRecorded", criteria: [{ label: "Review", required: true }], priorityId: 4, plannedStartAt: "2026-11-15T16:00:00Z",
  dueAt: "2026-11-15T18:00:00Z", slaMinutes: 120, roomId: null, areaId: null, internalRequestId: null };
const recurrence = { rule: "WEEKLY", weekdays: [1, 3], monthDay: null, defaultAssigneeId: 5, endDate: "2026-12-31" };
const series = { seriesId: 7, branchId: 1, title: "Routine", ...recurrence, defaultAssigneeName: "Synthetic executor",
  firstDueAt: template.dueAt, nextDueAt: "2026-11-17T18:00:00Z", isActive: true, occurrenceCount: 1, eTag: '"0000000000000001"' };
test("Recurring dates use hotel weekday and strict calendar end dates", () => {
  assert.doesNotThrow(() => validateRecurrence(template, recurrence));
  for (const invalid of [{ ...recurrence, weekdays: [0] }, { ...recurrence, weekdays: [1, 1] }, { ...recurrence, weekdays: [7] },
    { ...recurrence, endDate: "2026-11-15" }, { ...recurrence, endDate: "2026-02-30" }, { ...recurrence, defaultAssigneeId: 0 },
    { ...recurrence, rule: "MONTHLY", weekdays: [], monthDay: 15 }, { ...recurrence, rule: "DAILY", monthDay: 16 }])
    assert.throws(() => validateRecurrence(template, invalid));
  assert.doesNotThrow(() => validateRecurrence(template, { ...recurrence, rule: "MONTHLY", weekdays: [], monthDay: 16 }));
  assert.throws(() => validateRecurrence({ ...template, dueAt: null }, recurrence));
});
test("Create series preserves idempotency template, CSRF and scoped credentials", async t => {
  let writes = 0;
  t.mock.method(globalThis, "fetch", async (path, init) => {
    assert.equal(init.credentials, "same-origin"); assert.equal(init.cache, "no-store"); assert.equal(init.redirect, "error"); assert.equal(init.signal, signal);
    if (path === "/api/auth/csrf") return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
    writes++; assert.equal(path, "/api/branches/1/operational-task-series"); assert.equal(init.method, "POST");
    assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic"); assert.deepEqual(JSON.parse(init.body), { template, recurrence });
    return Response.json({ series, messageCode: "MSG08" }, { status: 201, headers: { ETag: series.eTag } });
  });
  assert.deepEqual(await createSeries(1, template, recurrence, signal), series); assert.equal(writes, 1);
});
test("Series reads reject cross-branch and malformed revisions", async t => {
  for (const bad of [{ ...series, branchId: 2 }, { ...series, eTag: 'W/"0000000000000001"' }, { ...series, occurrenceCount: -1 }]) {
    t.mock.method(globalThis, "fetch", async () => Response.json({ items: [bad], totalCount: 1, page: 1, pageSize: 25 }));
    await assert.rejects(readSeries(1, 1, signal));
  }
});
test("Stop sends only series command with strong ETag and validates inactive receipt", async t => {
  const stopped = { ...series, isActive: false, eTag: '"0000000000000002"' };
  t.mock.method(globalThis, "fetch", async (path, init) => {
    if (path === "/api/auth/csrf") return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
    assert.equal(path, "/api/branches/1/operational-task-series/7/stop"); assert.equal(init.headers["If-Match"], series.eTag);
    assert.deepEqual(JSON.parse(init.body), { reason: "Routine retired" });
    return Response.json({ series: stopped, messageCode: "MSG08" }, { headers: { ETag: stopped.eTag } });
  });
  assert.deepEqual(await stopSeries(1, series, "Routine retired", signal), stopped);
  await assert.rejects(stopSeries(1, stopped, null, signal));
});
test("Stale stop and unavailable creation do not retry or fabricate success", async t => {
  let writes = 0;
  t.mock.method(globalThis, "fetch", async path => {
    if (path === "/api/auth/csrf") return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
    writes++; return new Response("{}", { status: 412 });
  });
  await assert.rejects(stopSeries(1, series, null, signal), e => e.status === 412);
  assert.equal(writes, 1);
});
test("Task-context Stop binds the selected task and scoped series list", async t => {
  const stopped = { ...series, isActive: false, eTag: '"0000000000000002"' };
  t.mock.method(globalThis, "fetch", async (path, init) => {
    if (path === "/api/auth/csrf") return Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" });
    if (init.method === "POST") {
      assert.deepEqual(JSON.parse(init.body), { reason: null, taskId: 9 });
      return Response.json({ series: stopped, messageCode: "MSG08" }, { headers: { ETag: stopped.eTag } });
    }
    assert.equal(path, "/api/branches/1/operational-task-series?page=1&series=7");
    return Response.json({ items: [series], totalCount: 1, page: 1, pageSize: 25 });
  });
  assert.equal((await readSeries(1, 1, signal, 7)).items[0].seriesId, 7);
  assert.deepEqual(await stopSeries(1, series, null, signal, 9), stopped);
  await assert.rejects(stopSeries(1, series, null, signal, -1));
  t.mock.method(globalThis, "fetch", async () => Response.json({ items: [{ ...series, seriesId: 8 }], totalCount: 1, page: 1, pageSize: 25 }));
  await assert.rejects(readSeries(1, 1, signal, 7));
});
