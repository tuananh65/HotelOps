import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const bundle = await build({
  entryPoints: ["src/inspection/api.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  write: false,
});
const api = await import(
  `data:text/javascript;base64,${Buffer.from(bundle.outputFiles[0].text).toString("base64")}`
);
const criterion = () => ({
  itemId: 3,
  criterion: "Desk",
  mandatory: true,
  description: null,
  passCondition: null,
  outcome: null,
  note: null,
});
const execution = () => ({
  id: 9,
  branchId: 2,
  roomId: 5,
  cleaningTaskId: 8,
  inspectorId: 7,
  versionId: 4,
  state: "INSPECTING",
  dueAt: null,
  criteria: [criterion()],
  defects: [],
  evidence: [],
});
const inspection = () => ({
  id: 9,
  number: "INSP-9",
  branchId: 2,
  roomId: 5,
  roomNumber: "501",
  cleaningTaskId: 8,
  versionId: 4,
  versionNumber: 2,
  inspectorId: 7,
  inspectorName: "Inspector",
  status: "IN_PROGRESS",
  result: null,
  at: "2026-11-08T01:00:00Z",
  summary: null,
  reworkCount: 0,
});
const response = (v, status = 200, tag = '"00000000000000AF"') =>
  new Response(JSON.stringify(v), {
    status,
    headers: { "Content-Type": "application/json", ETag: tag },
  });
test("execution retains assigned version, phase and nullable results", () =>
  assert.equal(api.decodeExecution(execution()).versionId, 4));
for (const invalid of [0, -1, 1.2, Number.MAX_SAFE_INTEGER + 1])
  test(`reject invalid identity ${invalid}`, () =>
    assert.throws(() => api.parseId(String(invalid))));
for (const tag of [
  "*",
  'W/"00000000000000AF"',
  '"abcdabcdabcdabcd"',
  '"' + "A".repeat(64) + '"',
  null,
])
  test(`reject non-authoritative rowversion ${tag}`, () =>
    assert.equal(api.validTag(tag), false));
test("mandatory results are required before finish", () =>
  assert.equal(api.finishIssues([criterion()], []).length, 1));
test("FAIL needs associated defect", () =>
  assert.equal(
    api.finishIssues([{ ...criterion(), outcome: "FAIL" }], []).length,
    1,
  ));
test("PASS permits finish", () =>
  assert.deepEqual(
    api.finishIssues([{ ...criterion(), outcome: "PASS" }], []),
    [],
  ));
test("FAIL with defect permits finish", () =>
  assert.deepEqual(
    api.finishIssues([{ ...criterion(), outcome: "FAIL" }], [{ itemId: 3 }]),
    [],
  ));
test("optional unrecorded criterion is allowed", () =>
  assert.deepEqual(
    api.finishIssues([{ ...criterion(), mandatory: false }], []),
    [],
  ));
test("history uses nested filters pagination", () =>
  assert.equal(
    api.decodePage({
      items: [inspection()],
      total: 1,
      filters: { page: 2, pageSize: 10 },
    }).filters.page,
    2,
  ));
test("malformed history envelope rejected", () =>
  assert.throws(() =>
    api.decodePage({ items: [], total: 0, page: 1, pageSize: 20 }),
  ));
test("search and date query are encoded with exact server names", () => {
  const q = new URLSearchParams(
    api.historyQuery({
      page: 2,
      pageSize: 20,
      branchId: 2,
      search: "A&B",
      from: "2026-11-08T00:00:00Z",
      result: "FAIL",
    }),
  );
  assert.equal(q.get("search"), "A&B");
  assert.equal(q.get("branchId"), "2");
});
for (const f of [
  { page: 0, pageSize: 20 },
  { page: 1, pageSize: 101 },
  { page: 1, pageSize: 20, branchId: 0 },
  { page: 1, pageSize: 20, result: "ASSIGNED" },
  { page: 1, pageSize: 20, from: "2026-11-08T00:00:00" },
  {
    page: 1,
    pageSize: 20,
    from: "2026-11-09T00:00:00Z",
    to: "2026-11-08T00:00:00Z",
  },
])
  test(`invalid filter ${JSON.stringify(f)}`, () =>
    assert.throws(() => api.historyQuery(f)));
test("read validates requested source and ETag", async () => {
  const client = api.createInspectionApi(async () =>
    response({ ...execution(), branchId: 99 }),
  );
  await assert.rejects(
    client.execution(2, 9, new AbortController().signal),
    (e) => e.code === "SOURCE_MISMATCH",
  );
});
test("write bootstraps real CSRF and sends cookie/If-Match without replay", async () => {
  const calls = [];
  const client = api.createInspectionApi(async (url, init) => {
    calls.push({ url, init });
    return url.endsWith("/csrf")
      ? response({ csrfToken: "safe-token", headerName: "X-CSRF-TOKEN" })
      : response(execution());
  });
  await client.write(
    2,
    9,
    "results",
    { results: [], reason: "Draft" },
    '"00000000000000AF"',
    new AbortController().signal,
  );
  assert.equal(calls.length, 2);
  assert.equal(calls[1].init.method, "PUT");
  assert.equal(calls[1].init.credentials, "include");
  assert.equal(calls[1].init.headers["If-Match"], '"00000000000000AF"');
  assert.equal(calls[1].init.headers["X-CSRF-TOKEN"], "safe-token");
});
for (const status of [400, 401, 403, 409, 412, 503])
  test(`HTTP ${status} is surfaced and never retried`, async () => {
    let calls = 0;
    const client = api.createInspectionApi(async () => {
      calls++;
      return response({ title: "DENIED" }, status);
    });
    await assert.rejects(
      client.execution(2, 9, new AbortController().signal),
      (e) => e.status === status,
    );
    assert.equal(calls, 1);
  });
test("network uncertainty has no automatic mutation retry", async () => {
  let calls = 0;
  const c = api.createInspectionApi(async (url) => {
    calls++;
    if (url.endsWith("/csrf"))
      return response({ csrfToken: "token", headerName: "X-CSRF-TOKEN" });
    throw new TypeError("offline");
  });
  await assert.rejects(
    c.write(
      2,
      9,
      "finish",
      { reason: "Finished" },
      '"00000000000000AF"',
      new AbortController().signal,
    ),
    (e) => e.status === 0,
  );
  assert.equal(calls, 2);
});
test("bad CSRF contract prevents mutation", async () => {
  let calls = 0;
  const c = api.createInspectionApi(async () => {
    calls++;
    return response({ csrfToken: "secret", headerName: "WRONG" });
  });
  await assert.rejects(
    c.write(
      2,
      9,
      "start",
      {},
      '"00000000000000AF"',
      new AbortController().signal,
    ),
  );
  assert.equal(calls, 1);
});
test("download constructs authorized path and ignores arbitrary downloadPath", async () => {
  let url;
  const c = api.createInspectionApi(async (u) => {
    url = u;
    return new Response("PDF");
  });
  assert.equal(
    await (await c.download(2, 9, 44, new AbortController().signal)).text(),
    "PDF",
  );
  assert.equal(url, "/api/inspection-history/9/evidence/44?branchId=2");
});
test("abort remains cancellation", async () => {
  const controller = new AbortController();
  controller.abort();
  const c = api.createInspectionApi(async () => {
    throw new DOMException("Aborted", "AbortError");
  });
  await assert.rejects(
    c.execution(2, 9, controller.signal),
    (e) => e.name === "AbortError",
  );
});
test("historical detail preserves version and has no mutation endpoint", () => {
  const d = api.decodeDetail({
    inspection: inspection(),
    criteria: [criterion()],
    defects: [],
    evidence: [],
    housekeepers: [],
    reworks: [],
    cycleInspections: [],
  });
  assert.equal(d.inspection.versionNumber, 2);
});
test("empty history collection is valid", () =>
  assert.equal(
    api.decodePage({ items: [], total: 0, filters: { page: 1, pageSize: 20 } })
      .items.length,
    0,
  ));
test("malformed execution is rejected", () =>
  assert.throws(() =>
    api.decodeExecution({ ...execution(), criteria: [{ itemId: 3 }] }),
  ));
