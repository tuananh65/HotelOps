import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/turnoverHistory.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/fs017/history.mjs",
  define: { "import.meta.env": "{}" },
});
const { listTurnovers, readTurnover } =
  await import("../.test-output/fs017/history.mjs");
const coverage = {
  cleaning: "PERSISTED_SOURCE",
  inspection: "NOT_ACCEPTED",
  rework: "PERSISTED_SOURCE_NOT_SHARED_ACCEPTED",
  readiness: "PERSISTED_EVENTS_NOT_SHARED_ACCEPTED",
  roomStatusHistory: "UNAVAILABLE_CYCLE_LINK_CONTRACT",
};
const cycle = {
  id: 3,
  roomId: 4,
  branchId: 2,
  roomNumber: "101",
  number: "CL-3",
  cleaningType: "Checkout",
  recordedStatus: "IN_PROGRESS",
  workflowState: "PENDING_INSPECTION",
  createdAtUtc: "2026-11-07T00:00:00Z",
  startUtc: null,
  endUtc: null,
  isCurrentCycle: true,
  result: null,
  reworkCount: 0,
  cancellationReason: null,
  assignees: [],
};
const page = {
  roomId: 4,
  branchId: 2,
  items: [cycle],
  total: 1,
  filters: { page: 1, pageSize: 20 },
  coverage,
};
const detail = {
  cycle,
  timeline: [],
  timelineTotal: 0,
  timelinePage: 1,
  timelinePageSize: 50,
  inspections: [],
  reworks: [],
  evidence: [],
  coverage,
};
test("turnover uses cookie GET, allowlisted UTC filters and cancellation with no mutation", async () => {
  const abort = new AbortController();
  globalThis.fetch = async (url, o) => {
    assert.match(url, /\/rooms\/4\/turnover-history\?/);
    const q = new URL(url, "https://localhost").searchParams;
    assert.equal(q.get("branchId"), "2");
    assert.equal(q.has("actorId"), false);
    assert.equal(o.credentials, "include");
    assert.equal(o.method, "GET");
    assert.equal(o.signal, abort.signal);
    return Response.json(page);
  };
  assert.equal(
    (
      await listTurnovers(
        "4",
        new URLSearchParams({
          branchId: "2",
          actorId: "999",
          from: "2026-11-01T00:00:00Z",
        }),
        abort.signal,
      )
    ).items[0].id,
    3,
  );
});
test("turnover rejects wrong room, branch, invented accepted outcome and pagination", async () => {
  for (const bad of [
    { ...page, roomId: 9 },
    { ...page, branchId: 9 },
    { ...page, items: [{ ...cycle, result: "PASS" }] },
    { ...page, filters: { page: 2, pageSize: 20 } },
    { ...page, items: [{ ...cycle, createdAtUtc: "2026-11-07T00:00:00" }] },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(
      listTurnovers("4", new URLSearchParams({ branchId: "2" })),
      /INVALID_TURNOVER_RESPONSE/,
    );
  }
});
test("turnover rejects unsafe query before network and propagates unavailable result filter", async () => {
  globalThis.fetch = () => {
    throw Error("must not fetch");
  };
  for (const q of [
    { pageSize: "101" },
    { from: "2026-11-01" },
    { from: "2026-11-07T00:00:00Z", to: "2026-11-01T00:00:00Z" },
    { result: "READY" },
  ])
    await assert.rejects(listTurnovers("4", new URLSearchParams(q)));
  globalThis.fetch = async () =>
    Response.json({ title: "UNAVAILABLE" }, { status: 503 });
  await assert.rejects(
    listTurnovers("4", new URLSearchParams({ result: "PASS" })),
    (e) => e.status === 503,
  );
});
test("detail enforces cycle/source/evidence identity and does not expose denied Inspection subset", async () => {
  globalThis.fetch = async () => Response.json(detail);
  assert.equal(
    (await readTurnover("4", "3", new URLSearchParams({ branchId: "2" }))).cycle
      .id,
    3,
  );
  for (const bad of [
    { ...detail, cycle: { ...cycle, id: 8 } },
    { ...detail, inspections: [{ id: 5, cleaningTaskId: 9 }] },
    { ...detail, evidence: [{ id: 1, downloadPath: "https://evil.invalid" }] },
    { ...detail, coverage: { ...coverage, inspection: "ACCEPTED" } },
    {
      ...detail,
      timeline: [
        { sourceType: "TASK", sourceId: 1, event: "LOGGED", atUtc: "bad" },
      ],
    },
  ]) {
    globalThis.fetch = async () => Response.json(bad);
    await assert.rejects(
      readTurnover("4", "3", new URLSearchParams()),
      /INVALID_TURNOVER_RESPONSE/,
    );
  }
});
test("history 401/403/404/503 errors never trigger retry or prototype fallback", async () => {
  for (const status of [401, 403, 404, 503]) {
    let calls = 0;
    globalThis.fetch = async () => {
      calls++;
      return Response.json({ title: "DENIED" }, { status });
    };
    await assert.rejects(
      listTurnovers("4", new URLSearchParams()),
      (e) => e.status === status,
    );
    assert.equal(calls, 1);
  }
});
