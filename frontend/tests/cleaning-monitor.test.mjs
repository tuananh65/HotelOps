import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/cleaningMonitor.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs005/monitor.mjs",
  define: { "import.meta.env": "{}" },
});
const { listCleaning, readCleaning } =
  await import("../.test-output/fs005/monitor.mjs");
const item = {
  kind: "cleaning",
  id: 12,
  cleaningTaskId: 12,
  taskNumber: "API-CL-12",
  roomId: 3,
  roomNumber: "101",
  floorId: 2,
  floorName: null,
  recordedStatus: "IN_PROGRESS",
  workflowState: null,
  priorityId: null,
  priorityCode: null,
  assigneeId: 5,
  assigneeName: "API Staff",
  dueAtUtc: null,
  isOverdue: null,
  progressPercent: 40,
  blockerCode: null,
  blockerNote: null,
  description: null,
};
const page = {
  items: [item],
  total: 1,
  page: 1,
  pageSize: 20,
  fromDate: "2026-11-06",
  toDate: "2026-11-06",
  timezone: "Asia/Ho_Chi_Minh",
  asOfUtc: "2026-11-06T00:00:00Z",
};
test("monitor uses existing cookie transport, scoped route, allowlisted query and cancellation", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, options) => {
    assert.match(url, /\/api\/branches\/7\/cleaning-monitor\?/);
    const q = new URL(url, "https://localhost").searchParams;
    assert.equal(q.get("search"), "101 & 102");
    assert.equal(q.has("actorId"), false);
    assert.equal(options.credentials, "include");
    assert.equal(options.signal, abort.signal);
    return Response.json(page);
  };
  assert.equal(
    (
      await listCleaning(
        "7",
        new URLSearchParams({ search: "101 & 102", actorId: "999" }),
        abort.signal,
      )
    ).items[0].progressPercent,
    40,
  );
});
test("monitor rejects malformed rows and wrong pagination instead of mock fallback", async () => {
  for (const value of [
    { ...page, items: [{ ...item, id: Number.MAX_SAFE_INTEGER + 1 }] },
    { ...page, page: 2 },
    { ...page, items: [{ ...item, progressPercent: 101 }] },
  ]) {
    globalThis.fetch = async () => Response.json(value);
    await assert.rejects(
      listCleaning("7", new URLSearchParams()),
      /INVALID_MONITOR_RESPONSE/,
    );
  }
});
test("monitor propagates 401/403/404/503 once without fallback", async () => {
  for (const status of [401, 403, 404, 503]) {
    let calls = 0;
    globalThis.fetch = async () => {
      calls++;
      return Response.json({ title: "DENIED" }, { status });
    };
    await assert.rejects(
      listCleaning("7", new URLSearchParams()),
      (e) => e.status === status,
    );
    assert.equal(calls, 1);
  }
});
test("monitor rejects unsafe inputs before fetch", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  await assert.rejects(listCleaning("demo", new URLSearchParams()));
  await assert.rejects(
    listCleaning("7", new URLSearchParams({ pageSize: "101" })),
  );
  await assert.rejects(readCleaning("7", "other", "12"));
});
test("detail validates exact identity, timeline and nullable fields", async () => {
  const value = {
    item,
    assignments: [],
    assignmentCount: 0,
    progress: [],
    progressCount: 0,
    audit: [],
    auditCount: 0,
    timelineLimit: 20,
    asOfUtc: page.asOfUtc,
    timelinePage: 1,
  };
  globalThis.fetch = async () => Response.json(value);
  assert.equal((await readCleaning("7", "cleaning", "12")).item.id, 12);
  await assert.rejects(
    readCleaning("7", "cleaning", "13"),
    /INVALID_MONITOR_RESPONSE/,
  );
  globalThis.fetch = async () =>
    Response.json({
      ...value,
      progress: [{ id: 1, percent: 40 }],
      progressCount: 1,
    });
  await assert.rejects(
    readCleaning("7", "cleaning", "12"),
    /INVALID_MONITOR_RESPONSE/,
  );
});
