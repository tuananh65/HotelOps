import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import React from "react";
import { renderToString } from "react-dom/server";
await build({
  entryPoints: [
    "src/features/inspectionHistory/model.ts",
    "src/features/inspectionHistory/api.ts",
    "src/features/inspectionHistory/HistoryRoute.tsx",
  ],
  outdir: ".test-output/history",
  bundle: true,
  platform: "node",
  format: "esm",
  external: ["react", "react-dom"],
  outExtension: { ".js": ".mjs" },
});
const m = await import("../.test-output/history/model.mjs");
const { createHistoryApi, HistoryError } =
  await import("../.test-output/history/api.mjs");
const { HistoryRoute, historyScope } =
  await import("../.test-output/history/HistoryRoute.mjs");
const host = {
  authorization: "granted",
  sessionKey: "s1",
  principalId: "u1",
  branchId: 7,
  inspectionView: true,
};
const row = () => ({
  id: 42,
  number: "INSP-42",
  roomId: 3,
  roomNumber: "301",
  branchId: 7,
  cleaningTaskId: 12,
  versionId: 4,
  versionNumber: 2,
  inspectorId: 8,
  inspectorName: "Inspector",
  status: "COMPLETED",
  result: "FAIL",
  at: "2026-11-13T10:00:00",
  summary: null,
  reworkCount: 1,
});
const evidence = () => ({
  id: 9,
  entityType: "INSPECTION",
  entityId: 42,
  name: "photo.png",
  mimeType: "image/png",
  bytes: 8,
  uploadedAt: "2026-11-13T10:00:00Z",
  downloadPath: "https://untrusted.invalid/private",
});
const detail = () => ({
  inspection: row(),
  housekeepers: [
    {
      id: 1,
      userId: 2,
      name: "Cleaner",
      status: "ENDED",
      assignedAt: "2026-11-13T09:00:00",
      endedAt: null,
      reason: null,
    },
  ],
  criteria: [
    {
      id: 5,
      itemId: 6,
      criterion: "Clean",
      description: null,
      mandatory: true,
      passCondition: null,
      outcome: "FAIL",
      note: null,
      at: null,
      group: null,
      evidenceIds: [9],
    },
  ],
  defects: [
    {
      id: 10,
      itemId: 6,
      description: "Dust",
      status: "OPEN",
      severityId: 1,
      correctiveAction: null,
      resolvedAt: null,
      resolutionNote: null,
      location: null,
      evidenceIds: [9],
    },
  ],
  reworks: [
    {
      id: 11,
      cleaningTaskId: 12,
      inspectionId: 42,
      assigneeId: 2,
      reason: "Clean again",
      status: "PENDING",
      requestedAt: "2026-11-13T10:00:00",
      completedAt: null,
      note: null,
    },
  ],
  cycleInspections: [row()],
  evidence: [evidence()],
});
const q = { page: 1, pageSize: 20 };
const page = () => ({
  items: [row()],
  total: 1,
  filters: { branchId: 7, ...m.filters(q) },
});
const json = (v) =>
  new Response(JSON.stringify(v), {
    headers: { "Content-Type": "application/json" },
  });
const png = new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10]);
const client = (send) =>
  createHistoryApi(7, new AbortController().signal, send);

for (const [name, context] of [
  ["absent", undefined],
  ["unresolved", { ...host, authorization: "unresolved" }],
  ["denied", { ...host, authorization: "denied" }],
  ["permission", { ...host, inspectionView: false }],
  ["session", { ...host, sessionKey: "" }],
  ["principal", { ...host, principalId: "" }],
  ...[0, -1, "", "abc", "1e2", "9007199254740992"].map((branchId) => [
    "branch " + branchId,
    { ...host, branchId },
  ]),
])
  test(`direct route denies ${name}`, () => {
    assert.equal(historyScope(context), null);
    assert.match(
      renderToString(
        React.createElement(HistoryRoute, { context, initialId: "42" }),
      ),
      /role="alert"/,
    );
  });
test("scope identity includes session generation, principal and trusted branch", () => {
  for (const change of [
    { sessionKey: "s2" },
    { principalId: "u2" },
    { branchId: 8 },
  ])
    assert.notEqual(
      historyScope(host).key,
      historyScope({ ...host, ...change }).key,
    );
});
test("invalid direct inspection identity fails closed", () =>
  assert.match(
    renderToString(
      React.createElement(HistoryRoute, { context: host, initialId: "-1" }),
    ),
    /role="alert"/,
  ));
test("filters map only backend-supported parameters, with trusted branch", () => {
  const query = new URLSearchParams(
    m.historyQuery(7, {
      page: 2,
      pageSize: 10,
      roomId: 3,
      inspectorId: 8,
      housekeeperId: 2,
      result: "fail",
      from: "2026-11-12T00:00:00Z",
      to: "2026-11-13T00:00:00Z",
      search: " dust ",
    }),
  );
  assert.equal(query.get("branchId"), "7");
  assert.equal(query.get("result"), "FAIL");
  assert.equal(query.get("search"), "dust");
  assert.equal(query.get("housekeeperId"), "2");
  assert.equal(query.get("page"), "2");
});
for (const change of [
  { page: 0 },
  { page: 1.5 },
  { page: Number.MAX_SAFE_INTEGER },
  { pageSize: 0 },
  { pageSize: 101 },
  { roomId: 0 },
  { inspectorId: -1 },
  { housekeeperId: Number.MAX_SAFE_INTEGER + 1 },
  { result: "READY" },
  { result: "PASSED" },
  { search: "x".repeat(151) },
  { search: "a\nb" },
  { from: "2026-11-13" },
  { from: "2026-02-30T00:00:00Z" },
  { from: "2026-11-13T00:00:00+07:00" },
  { from: "2026-11-13T00:00:00Z", to: "2026-11-12T00:00:00Z" },
])
  test(`reject invalid filter before GET ${JSON.stringify(change)}`, async () => {
    let calls = 0;
    await assert.rejects(
      client(async () => {
        calls++;
        return json(page());
      }).list({ ...q, ...change }),
    );
    assert.equal(calls, 0);
  });
test("accept UTC fractions and supported result boundaries", () => {
  assert.equal(
    m.filters({
      ...q,
      result: "CONDITIONAL",
      from: "2026-11-13T00:00:00.1234567Z",
    }).result,
    "CONDITIONAL",
  );
});
test("list and detail preserve historical version, defects and reworks separately", async () => {
  const api = client(async (path) =>
    json(path.includes("/42?") ? detail() : page()),
  );
  assert.equal((await api.list(q)).items[0].versionNumber, 2);
  const d = await api.detail(42);
  assert.equal(d.defects[0].description, "Dust");
  assert.equal(d.reworks[0].id, 11);
  assert.equal(d.inspection.reworkCount, 1);
  assert.equal(d.reinspectionCount, undefined);
  assert.equal(d.defectCount, undefined);
});
test("empty and out-of-range pages preserve filters and total", () => {
  const p = page();
  p.items = [];
  p.total = 0;
  assert.equal(m.decodePage(p, 7, q).total, 0);
  p.total = 1;
  p.filters.page = 4;
  assert.equal(m.decodePage(p, 7, { ...q, page: 4 }).items.length, 0);
});
for (const mutate of [
  (p) => (p.items[0].branchId = 8),
  (p) => (p.filters.branchId = 8),
  (p) => (p.filters.page = 2),
  (p) => (p.total = -1),
  (p) => (p.items[0].id = Number.MAX_SAFE_INTEGER + 1),
  (p) => delete p.items[0].versionId,
  (p) => (p.items[0].at = "invalid"),
  (p) => p.items.push(row()),
])
  test(`malformed/cross-branch list rejected: ${mutate}`, () => {
    const p = page();
    mutate(p);
    assert.throws(() => m.decodePage(p, 7, q));
  });
for (const mutate of [
  (d) => (d.inspection.branchId = 8),
  (d) => (d.inspection.id = 43),
  (d) => (d.cycleInspections[0].roomId = 99),
  (d) => (d.reworks[0].cleaningTaskId = 99),
  (d) => (d.evidence[0].entityId = 99),
  (d) => (d.criteria[0].mandatory = "yes"),
  (d) => delete d.defects,
  (d) => (d.housekeepers[0].userId = -1),
])
  test(`malformed/cross-scope detail rejected: ${mutate}`, () => {
    const d = detail();
    mutate(d);
    assert.throws(() => m.decodeDetail(d, 7, 42));
  });
test("all operations are GET with cookies, no-store and no redirect; evidence URL is reconstructed", async () => {
  const calls = [];
  const api = client(async (path, init) => {
    calls.push({ path, init });
    return path.includes("/evidence/")
      ? new Response(png, {
          headers: { "Content-Type": "image/png", "Content-Length": "8" },
        })
      : json(path.includes("/42?") ? detail() : page());
  });
  await api.list(q);
  await api.detail(42);
  const file = await api.download(42, evidence());
  assert.equal(file.name, "inspection-42-evidence-9.png");
  assert.equal(file.blob.size, 8);
  assert.equal(
    calls[2].path,
    "/api/inspection-history/42/evidence/9?branchId=7",
  );
  for (const { init } of calls) {
    assert.equal(init.method, "GET");
    assert.equal(init.credentials, "include");
    assert.equal(init.cache, "no-store");
    assert.equal(init.redirect, "error");
    assert.equal(init.body, undefined);
  }
});
test("reading metadata does not fetch evidence bytes", async () => {
  const calls = [];
  await client(async (path) => {
    calls.push(path);
    return json(detail());
  }).detail(42);
  assert.equal(calls.length, 1);
  assert.ok(!calls[0].includes("/evidence/"));
});
for (const status of [400, 401, 403, 404, 409, 503])
  test(`HTTP ${status} propagated without retry`, async () => {
    let calls = 0;
    await assert.rejects(
      client(async () => {
        calls++;
        return new Response("{}", { status });
      }).list(q),
      (e) => e instanceof HistoryError && e.status === status,
    );
    assert.equal(calls, 1);
  });
test("HTML routing fallback cannot become protected data", async () => {
  await assert.rejects(
    client(
      async () =>
        new Response("<html/>", { headers: { "Content-Type": "text/html" } }),
    ).list(q),
  );
});
test("aborted scope sends no request", async () => {
  const scope = new AbortController();
  scope.abort();
  let calls = 0;
  await assert.rejects(
    createHistoryApi(7, scope.signal, async () => {
      calls++;
      return json(page());
    }).list(q),
    { name: "AbortError" },
  );
  assert.equal(calls, 0);
});
test("late JSON after scope replacement is rejected", async () => {
  const scope = new AbortController();
  const response = json(page());
  response.json = async () => {
    scope.abort();
    return page();
  };
  await assert.rejects(
    createHistoryApi(7, scope.signal, async () => response).list(q),
    { name: "AbortError" },
  );
});
for (const change of [
  { mimeType: null },
  { mimeType: "text/html" },
  { bytes: null },
  { bytes: 0 },
  { bytes: m.MAX_EVIDENCE_BYTES + 1 },
  { id: -1 },
])
  test(`unsafe evidence metadata fails before GET ${JSON.stringify(change)}`, async () => {
    let calls = 0;
    await assert.rejects(
      client(async () => {
        calls++;
        return new Response(png);
      }).download(42, { ...evidence(), ...change }),
    );
    assert.equal(calls, 0);
  });
for (const response of [
  () => new Response(png, { headers: { "Content-Type": "text/html" } }),
  () =>
    new Response(png, {
      headers: { "Content-Type": "image/png", "Content-Length": "9" },
    }),
  () => new Response("badbytes", { headers: { "Content-Type": "image/png" } }),
  () =>
    new Response(png.slice(0, 4), { headers: { "Content-Type": "image/png" } }),
])
  test(`invalid evidence bytes/headers rejected: ${response}`, async () =>
    assert.rejects(client(async () => response()).download(42, evidence())));
test("oversized streamed evidence is cancelled", async () => {
  let cancelled = false;
  const stream = new ReadableStream({
    start(c) {
      c.enqueue(new Uint8Array(m.MAX_EVIDENCE_BYTES + 1));
    },
    cancel() {
      cancelled = true;
    },
  });
  await assert.rejects(
    client(
      async () =>
        new Response(stream, { headers: { "Content-Type": "image/png" } }),
    ).download(42, evidence()),
  );
  assert.equal(cancelled, true);
});
test("scope cancellation cancels pending evidence stream", async () => {
  const scope = new AbortController();
  let started;
  const ready = new Promise((resolve) => (started = resolve));
  let cancelled = false;
  const stream = new ReadableStream({
    pull() {
      started();
    },
    cancel() {
      cancelled = true;
    },
  });
  const promise = createHistoryApi(
    7,
    scope.signal,
    async () =>
      new Response(stream, { headers: { "Content-Type": "image/png" } }),
  ).download(42, evidence());
  await ready;
  scope.abort();
  await assert.rejects(promise, { name: "AbortError" });
  assert.equal(cancelled, true);
});
for (const [mime, bytes] of [
  ["image/jpeg", new Uint8Array([255, 216, 255])],
  ["application/pdf", new TextEncoder().encode("%PDF-1.7")],
])
  test(`supported explicit ${mime} download`, async () => {
    const file = await client(
      async () => new Response(bytes, { headers: { "Content-Type": mime } }),
    ).download(42, { ...evidence(), mimeType: mime, bytes: bytes.length });
    assert.equal(file.blob.type, mime);
  });
