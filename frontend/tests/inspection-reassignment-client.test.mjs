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
const { readReassignment, reassignInspection, inspectionAssignmentRejection } =
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

const reinspectionSource = () => ({
  ...source(),
  state: "ASSIGNED",
  reworkId: 8,
  reworkETag: tag,
  bindingETag: next,
});
test("reinspection options require the exact rework/binding and Assigned state", async () => {
  for (const patch of [
    null,
    { reworkId: 0 },
    { reworkETag: "*" },
    { bindingETag: undefined },
    { state: "INSPECTING" },
  ]) {
    globalThis.fetch = async (url) => {
      assert.ok(
        url.endsWith("/branches/1/reinspections/3/reassignment-options"),
      );
      return Response.json(
        { ...reinspectionSource(), ...patch },
        { headers: { ETag: tag } },
      );
    };
    const call = readReassignment("1", 3, scope().signal, true);
    if (patch) await assert.rejects(call, /INVALID_REASSIGNMENT_RESPONSE/);
    else assert.equal((await call).reworkId, 8);
  }
});
test("reinspection transfer sends four source revisions and rejects another workflow receipt", async () => {
  const operation = randomUUID();
  for (const patch of [
    null,
    { contract: "FE05_INSPECTION_REASSIGNMENT_V1" },
    { reworkId: 99 },
    { reworkETag: next },
    { bindingETag: tag },
    { state: "PASSED" },
  ]) {
    let writes = 0;
    globalThis.fetch = async (url, init) => {
      if (url.endsWith("/auth/csrf"))
        return Response.json({
          headerName: "X-CSRF-TOKEN",
          csrfToken: "synthetic",
        });
      ++writes;
      assert.ok(url.endsWith("/branches/1/reinspections/3/reassign"));
      assert.equal(init.headers["If-Match"], tag);
      assert.equal(init.headers["Idempotency-Key"], operation);
      assert.equal(init.headers["X-CSRF-TOKEN"], "synthetic");
      assert.deepEqual(JSON.parse(init.body), {
        inspectorId: 7,
        cycleETag: tag,
        reworkETag: tag,
        bindingETag: next,
        reason: "Handoff",
      });
      return Response.json(
        {
          ...receipt(operation),
          state: "ASSIGNED",
          contract: "FE05_REINSPECTION_REASSIGNMENT_V1",
          reworkId: 8,
          reworkETag: tag,
          bindingETag: next,
          ...patch,
        },
        { headers: { ETag: next } },
      );
    };
    const call = reassignInspection(
      "1",
      reinspectionSource(),
      7,
      " Handoff ",
      operation,
      scope(),
      true,
    );
    if (patch) await assert.rejects(call, /INVALID_REASSIGNMENT_RESPONSE/);
    else assert.equal(await call, next);
    assert.equal(writes, 1);
  }
});
test("reinspection stale session or missing binding never sends a mutation", async () => {
  let calls = 0;
  globalThis.fetch = async () => {
    ++calls;
    throw Error("Unexpected request");
  };
  await assert.rejects(
    reassignInspection(
      "1",
      { ...reinspectionSource(), bindingETag: undefined },
      7,
      "Handoff",
      randomUUID(),
      scope(),
      true,
    ),
    /INVALID_REASSIGNMENT_COMMAND/,
  );
  await assert.rejects(
    reassignInspection(
      "1",
      reinspectionSource(),
      7,
      "Handoff",
      randomUUID(),
      { ...scope(), isCurrent: () => false },
      true,
    ),
    { name: "AbortError" },
  );
  assert.equal(calls, 0);
});

test("business denial is presented only for its exact authenticated API contract", async () => {
  for (const [status, code, message] of [
    [400, "INSPECTOR_CLEANER_CONFLICT", "MSG22"],
    [409, "INSPECTION_ALREADY_ASSIGNED", "MSG03"],
    [400, "CHECKLIST_UNAVAILABLE", "MSG03"],
    [503, "ORIGINAL_CHECKLIST_VERSION_UNAVAILABLE", "MSG03"],
    [403, "INSPECTOR_CLEANER_CONFLICT", null],
    [503, "UNAVAILABLE", null],
  ]) {
    globalThis.fetch = async (url) =>
      url.endsWith("/auth/csrf")
        ? Response.json({ headerName: "X-CSRF-TOKEN", csrfToken: "synthetic" })
        : Response.json({ title: code }, { status });
    await assert.rejects(
      reassignInspection(
        "1",
        reinspectionSource(),
        7,
        "Handoff",
        randomUUID(),
        scope(),
        true,
      ),
      (error) => {
        const rendered = inspectionAssignmentRejection(error);
        if (message) assert.ok(rendered.startsWith(message + ":"));
        else assert.equal(rendered, null);
        return true;
      },
    );
  }
  assert.equal(
    inspectionAssignmentRejection(new Error("INSPECTOR_CLEANER_CONFLICT")),
    null,
  );
});
