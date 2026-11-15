import { test } from "node:test";
import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { build } from "esbuild";
await build({
  entryPoints: ["src/api/dev1/inspectionReassignment.ts"],
  bundle: true,
  platform: "node",
  format: "esm",
  outfile: ".test-output/inspection-reassignment.mjs",
  define: { "import.meta.env": "{}" },
});
const { readReassignment, reassignInspection } =
  await import("../.test-output/inspection-reassignment.mjs");
const tag = '"0000000000000001"',
  next = '"0000000000000002"';
const source = () => ({
  id: 3,
  branchId: 1,
  taskId: 2,
  roomId: 4,
  versionId: 5,
  inspectorId: 6,
  state: "INSPECTING",
  dueAt: "2026-11-17T01:00:00Z",
  sourceETag: tag,
  cycleETag: tag,
  inspectors: [{ id: 7, name: "Next inspector" }],
  canReassign: true,
});
const scope = () => ({
  signal: new AbortController().signal,
  isCurrent: () => true,
});
const receipt = (operation) => ({
  ...source(),
  previousInspectorId: 6,
  inspectorId: 7,
  sourceETag: next,
  operationId: operation,
  contract: "FE05_INSPECTION_REASSIGNMENT_V1",
  notification: "PENDING_DELIVERY",
});
test("options reject cross-scope, malformed revisions and self-assignment candidates", async () => {
  for (const patch of [
    { branchId: 99 },
    { id: 99 },
    { sourceETag: "*" },
    { cycleETag: "*" },
    { state: "PASSED" },
    { inspectors: [{ id: 6, name: "Current" }] },
    { inspectors: [] },
    { dueAt: "bad" },
    { versionId: 0 },
  ]) {
    globalThis.fetch = async () =>
      Response.json({ ...source(), ...patch }, { headers: { ETag: tag } });
    await assert.rejects(
      readReassignment("1", 3, scope().signal),
      /INVALID_REASSIGNMENT_RESPONSE/,
    );
  }
  for (const state of ["ASSIGNED", "INSPECTING", "PENDING_CONCLUSION"]) {
    globalThis.fetch = async () =>
      Response.json({ ...source(), state }, { headers: { ETag: tag } });
    assert.equal((await readReassignment("1", 3, scope().signal)).state, state);
  }
});
test("mutation sends CSRF, exact revision and one operation; rejects replaced receipt scope/lifecycle", async () => {
  const operation = randomUUID();
  for (const patch of [
    null,
    { id: 99 },
    { branchId: 99 },
    { taskId: 99 },
    { roomId: 99 },
    { versionId: 99 },
    { previousInspectorId: 99 },
    { inspectorId: 6 },
    { state: "ASSIGNED" },
    { operationId: randomUUID() },
    { sourceETag: "*" },
  ]) {
    let writes = 0;
    globalThis.fetch = async (url, init) => {
      if (url.endsWith("/auth/csrf"))
        return Response.json({
          headerName: "X-CSRF-TOKEN",
          csrfToken: "synthetic",
        });
      ++writes;
      assert.equal(init.headers["If-Match"], tag);
      assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic");
      assert.equal(init.headers["Idempotency-Key"], operation);
      assert.equal(init.credentials, "include");
      assert.deepEqual(JSON.parse(init.body), {
        inspectorId: 7,
        cycleETag: tag,
        reason: "Handoff",
      });
      return Response.json(
        { ...receipt(operation), ...patch },
        { headers: { ETag: next } },
      );
    };
    const call = reassignInspection(
      "1",
      source(),
      7,
      " Handoff ",
      operation,
      scope(),
    );
    if (patch) await assert.rejects(call, /INVALID_REASSIGNMENT_RESPONSE/);
    else assert.equal(await call, next);
    assert.equal(writes, 1);
  }
});
test("authority changing during CSRF prevents POST", async () => {
  let current = true,
    calls = 0;
  globalThis.fetch = async () => {
    ++calls;
    current = false;
    return Response.json({
      headerName: "X-CSRF-TOKEN",
      csrfToken: "synthetic",
    });
  };
  await assert.rejects(
    reassignInspection("1", source(), 7, "Handoff", randomUUID(), {
      ...scope(),
      isCurrent: () => current,
    }),
    { name: "AbortError" },
  );
  assert.equal(calls, 1);
});
test("bad CSRF and same inspector fail without a write; uncertain result is never retried", async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    ++calls;
    return Response.json({ headerName: "Other", csrfToken: "synthetic" });
  };
  await assert.rejects(
    reassignInspection("1", source(), 6, "Handoff", randomUUID(), scope()),
    /INVALID_REASSIGNMENT_COMMAND/,
  );
  assert.equal(calls, 0);
  await assert.rejects(
    reassignInspection("1", source(), 7, "Handoff", randomUUID(), scope()),
    /INVALID_CSRF_RESPONSE/,
  );
  assert.equal(calls, 1);
  let writes = 0;
  globalThis.fetch = async (url) => {
    if (url.endsWith("/auth/csrf"))
      return Response.json({
        headerName: "X-CSRF-TOKEN",
        csrfToken: "synthetic",
      });
    ++writes;
    return Response.json({ title: "Unavailable" }, { status: 503 });
  };
  await assert.rejects(
    reassignInspection("1", source(), 7, "Handoff", randomUUID(), scope()),
    /Unavailable/,
  );
  assert.equal(writes, 1);
});
