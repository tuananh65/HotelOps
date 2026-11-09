import { test } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
const b = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "ts",
    contents:
      "export * from './src/structure-deactivation/deactivationPreviewApi';",
  },
  bundle: true,
  write: false,
  format: "esm",
});
const { createDeactivationPreviewApi } = await import(
  "data:text/javascript;base64," +
    Buffer.from(b.outputFiles[0].text).toString("base64")
);
const tag = '"' + "A".repeat(64) + '"';
const body = () => ({
  record: {
    kind: "floor",
    id: 11,
    branchId: 42,
    code: "F11",
    name: "Floor",
    description: null,
    floorId: null,
    floorNumber: 1,
    isActive: true,
  },
  dependencies: [{ kind: "ACTIVE_ROOM", count: 2, ids: [3, 4] }],
  writerCoordinationRequired: true,
});
test("exact read-only preview request and DTO whitelist", async () => {
  const calls = [];
  const api = createDeactivationPreviewApi(async (u, o) => {
    calls.push([u, o]);
    return Response.json(
      { ...body(), secret: "DO_NOT_EXPOSE" },
      { headers: { ETag: tag } },
    );
  });
  const r = await api.read("floor", 42, 11);
  assert.equal(r.ok, true);
  assert.equal(calls.length, 1);
  assert.equal(
    calls[0][0],
    "/api/branches/42/floors/11/deactivation-dependencies",
  );
  assert.equal(calls[0][1].method, "GET");
  assert.equal(calls[0][1].credentials, "include");
  assert.equal(calls[0][1].cache, "no-store");
  assert.deepEqual(r.data.dependencies, [{ kind: "ACTIVE_ROOM", count: 2 }]);
  assert.ok(!JSON.stringify(r).includes("DO_NOT_EXPOSE"));
  assert.equal(r.data.writerCoordinationRequired, true);
});
for (const [name, modify] of [
  ["wrong branch", (x) => (x.record.branchId = 43)],
  ["unknown dependency", (x) => (x.dependencies[0].kind = "SECRET")],
  ["bad IDs", (x) => (x.dependencies[0].ids = [0])],
  [
    "unsafe count",
    (x) => (x.dependencies[0].count = Number.MAX_SAFE_INTEGER + 1),
  ],
  ["false readiness", (x) => (x.writerCoordinationRequired = false)],
  ["duplicate kind", (x) => x.dependencies.push(x.dependencies[0])],
])
  test("reject preview " + name, async () => {
    const v = body();
    modify(v);
    const r = await createDeactivationPreviewApi(async () =>
      Response.json(v, { headers: { ETag: tag } }),
    ).read("floor", 42, 11);
    assert.equal(r.ok, false);
  });
for (const status of [401, 403, 404, 503])
  test("preview denial " + status, async () => {
    const r = await createDeactivationPreviewApi(async () =>
      Response.json({ detail: "SECRET" }, { status }),
    ).read("floor", 42, 11);
    assert.equal(r.ok, false);
    assert.equal(r.error.status, status);
    assert.ok(!JSON.stringify(r).includes("SECRET"));
  });
test("empty blockers remain coordination-required", async () => {
  const v = body();
  v.dependencies = [];
  const r = await createDeactivationPreviewApi(async () =>
    Response.json(v, { headers: { ETag: tag } }),
  ).read("floor", 42, 11);
  assert.equal(r.ok, true);
  assert.deepEqual(r.data.dependencies, []);
  assert.equal(r.data.writerCoordinationRequired, true);
});
test("aborted late response discarded", async () => {
  const c = new AbortController();
  const r = await createDeactivationPreviewApi(async () => {
    c.abort();
    return Response.json(body(), { headers: { ETag: tag } });
  }).read("floor", 42, 11, c.signal);
  assert.equal(r.ok, false);
  assert.equal(r.error.reason, "CANCELLED");
});
test("network and malformed JSON remain distinct", async () => {
  const network = await createDeactivationPreviewApi(async () => {
    throw Error("SECRET");
  }).read("floor", 42, 11);
  assert.equal(network.error.reason, "NETWORK");
  const malformed = await createDeactivationPreviewApi(
    async () =>
      new Response("{", {
        headers: { "Content-Type": "application/json", ETag: tag },
      }),
  ).read("floor", 42, 11);
  assert.equal(malformed.error.reason, "INVALID_RESPONSE");
});
